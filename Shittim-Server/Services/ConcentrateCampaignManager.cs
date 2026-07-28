using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCommand;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCondition;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;
using Shittim.Services;

namespace Shittim_Server.Services;

public class ConcentrateCampaignManager
{
    /// <summary>
    /// Par time for a tactic, in milliseconds — ConstCommonExcel.TacticRankClearTime on the client.
    /// Beating it is worth one rank. Our ConstCommonExcel model carries no fields, so the value is
    /// pinned here alongside the identical threshold CalcAllEnemiesDefeatedInTime uses.
    /// </summary>
    private const long TacticRankClearTimeMsec = 120 * 1000;

    /// <summary>
    /// Undamaged, on the 0..10000 scale HpInfos is denominated in — these are rates, not absolute HP.
    /// </summary>
    private const long FullHpRate = 10000L;

    /// <summary>
    /// The rank CalcTacticRank gives a flawless, in-par win — an S. The third star on a campaign
    /// stage is scored against how many tactics finished at this rank, not against the battle result.
    /// </summary>
    private const long TacticRankS = 3L;

    /// <summary>
    /// The parcel id official stamps on the account-exp entry in ParcelForMission. AccountExp has no
    /// meaningful id of its own — the sub-stage paths pass 0 — but the wire capture says 1, and the
    /// id is what the client keys the entry by.
    /// </summary>
    private const long AccountExpParcelId = 1L;

    private readonly ExcelTableService _excelService;
    private readonly HexaMapService _hexaMapService;
    private readonly ParcelHandler _parcelHandler;
    private readonly MissionService _missionService;
    private readonly IMapper _mapper;

    public ConcentrateCampaignManager(
        ExcelTableService excelService,
        HexaMapService hexaMapService,
        ParcelHandler parcelHandler,
        MissionService missionService,
        IMapper mapper)
    {
        _excelService = excelService;
        _hexaMapService = hexaMapService;
        _parcelHandler = parcelHandler;
        _missionService = missionService;
        _mapper = mapper;
    }

    public async Task<CampaignMainStageSaveDBServer?> GetConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        return await context.CampaignMainStageSaves
            .Where(x => x.AccountServerId == account.ServerId && x.StageUniqueId == stageUniqueId)
            .OrderByDescending(x => x.ServerId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// The run ContentSave_Get should answer with, if there is one. The request carries no stage id —
    /// the client is asking "am I in the middle of something?", so the newest still-open save across
    /// every stage is the answer.
    ///
    /// A save sitting at BeforeStart does not count: Campaign_EnterMainStage creates the row but the
    /// player is still on echelon select until Campaign_ConfirmMainStage moves it to PlayerPhase, and
    /// there is no battle to resume into before that.
    /// </summary>
    public async Task<CampaignMainStageSaveDBServer?> GetOpenConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account)
    {
        return await context.CampaignMainStageSaves
            .Where(x => x.AccountServerId == account.ServerId
                     && x.IsOpen
                     && x.CampaignState != CampaignState.BeforeStart)
            .OrderByDescending(x => x.ServerId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Retires the account's open runs so ContentSave_Get stops offering them. Pass a stage id to
    /// close only that stage's run (the client names one on ContentSave_Discard), or nothing to close
    /// them all. Returns how many rows were closed.
    /// </summary>
    public async Task<int> CloseConcentrateCampaigns(
        SchaleDataContext context,
        AccountDBServer account,
        long? stageUniqueId = null)
    {
        var open = await context.CampaignMainStageSaves
            .Where(x => x.AccountServerId == account.ServerId && x.IsOpen)
            .Where(x => stageUniqueId == null || x.StageUniqueId == stageUniqueId)
            .ToListAsync();

        if (open.Count == 0)
            return 0;

        foreach (var save in open)
            save.IsOpen = false;

        context.CampaignMainStageSaves.UpdateRange(open);
        await context.SaveChangesAsync();

        return open.Count;
    }

    public async Task<CampaignMainStageSaveDBServer> CreateConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        var hexaData = await _hexaMapService.LoadState(stageUniqueId);

        // Entering a stage abandons whatever the player was in before it — only one campaign run can
        // be open at a time, and the new one is the answer ContentSave_Get owes from here on.
        await CloseConcentrateCampaigns(context, account);

        // Official's save carries the stage's enter cost (e.g. 10 AP) as a parcel list; it drives
        // the fee display and the retreat refund.
        var stageExcel = _excelService.GetTable<CampaignStageExcelT>()
            .FirstOrDefault(x => x.Id == stageUniqueId);
        List<ParcelInfo> entranceFee = stageExcel is null
            ? []
            : ParcelInfo.CreateParcelInfo(
                stageExcel.StageEnterCostType, stageExcel.StageEnterCostId, stageExcel.StageEnterCostAmount);

        // The DB row keeps every collection non-null (the columns are NOT NULL); the wire copy is
        // trimmed to official's key set by ShapeForWire at the handler.
        var stageSave = new CampaignMainStageSaveDBServer
        {
            ContentType = Schale.FlatData.ContentType.CampaignMainStage,
            LastEnemyEntityId = hexaData.LastEntityId,
            EnemyInfos = HexaMapService.AddHexaUnitList(hexaData.HexaUnitList),
            EchelonInfos = new Dictionary<long, HexaUnit>(),
            WithdrawInfos = new Dictionary<long, List<long>>(),
            StrategyObjects = HexaMapService.AddHexaStrategyList(hexaData.HexaStrageyList),
            StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
            StrategyObjectHistory = new List<long>(),
            ActivatedHexaEventsAndConditions = BuildStartEventActivations(hexaData),
            HexaEventDelayedExecutions = new Dictionary<long, List<long>>(),
            TileMapStates = HexaMapService.AddHexaTileList(hexaData),
            DisplayInfos = new List<HexaDisplayInfo>(),
            DeployedEchelonInfos = new List<HexaUnit>(),
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = stageUniqueId,
            StageEntranceFee = entranceFee,
            EnemyKillCountByUniqueId = new(),
            IsOpen = true
        };
        stageSave.AccountServerId = account.ServerId;

        context.CampaignMainStageSaves.Add(stageSave);
        await context.SaveChangesAsync();

        return stageSave;
    }

    // Official serializes these six save collections only once they have content: EchelonInfos
    // first appears at DeployEchelon, DeployedEchelonInfos at ConfirmMainStage, DisplayInfos at
    // MapMove, and WithdrawInfos/StrategyObjectRewards/StrategyObjectHistory never in the captured
    // flow. Sending them non-null-but-empty at map entry mislabels a fresh run as an in-progress
    // one. The DB entity keeps them non-null (NOT NULL columns), so the trimming happens on the
    // mapped wire copy — apply this to every SaveDataDB a campaign response carries.
    internal static CampaignMainStageSaveDB ShapeForWire(CampaignMainStageSaveDB save)
    {
        if (save.EchelonInfos is { Count: 0 }) save.EchelonInfos = null;
        if (save.WithdrawInfos is { Count: 0 }) save.WithdrawInfos = null;
        if (save.StrategyObjectRewards is { Count: 0 }) save.StrategyObjectRewards = null;
        if (save.StrategyObjectHistory is { Count: 0 }) save.StrategyObjectHistory = null;
        if (save.DisplayInfos is { Count: 0 }) save.DisplayInfos = null;
        if (save.DeployedEchelonInfos is { Count: 0 }) save.DeployedEchelonInfos = null;
        // EnemyInfos is the odd one out: it is populated from map entry onwards and only empties when
        // the last enemy dies. Official drops the key entirely on that clearing TacticResult.
        if (save.EnemyInfos is { Count: 0 }) save.EnemyInfos = null;
        return save;
    }

    // Official's fresh-save 6001 already carries {"0":[0]}: every event whose conditions include
    // HexaConditionStartCampaign is marked as fired ({EventId: [ConditionIds]}) because the save's
    // EnemyInfos/StrategyObjects already contain the entities those events' spawn commands create.
    // Sending {} instead makes the client re-evaluate the start event against the pre-populated map
    // while constructing the strategy scene, and the map never opens (the 6001 callback never
    // completes — no Echelon_List follows, and the client replays 6001 on reconnect).
    internal static Dictionary<long, List<long>> BuildStartEventActivations(HexaTileMap hexaData)
    {
        var activations = new Dictionary<long, List<long>>();

        if (hexaData.Events == null)
            return activations;

        foreach (var hexaEvent in hexaData.Events)
        {
            if (hexaEvent.HexaConditions == null)
                continue;

            var startConditionIds = hexaEvent.HexaConditions
                .Where(c => c is HexaConditionStartCampaign)
                .Select(c => c.ConditionId)
                .ToList();

            if (startConditionIds.Count > 0)
                activations[hexaEvent.EventId] = startConditionIds;
        }

        return activations;
    }

    public async Task<CampaignMainStageSaveDBServer> DeployEchelon(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignDeployEchelonRequest deployReq)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, deployReq.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {deployReq.StageUniqueId}");

        stageSaveData.EchelonInfos ??= new Dictionary<long, HexaUnit>();

        stageSaveData.EchelonInfos = await DeployConcentratedEchelon(
            context, account, stageSaveData.EchelonInfos, deployReq.DeployedEchelons);

        context.Entry(stageSaveData).Property(x => x.EchelonInfos).IsModified = true;
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    private async Task<Dictionary<long, HexaUnit>> DeployConcentratedEchelon(
        SchaleDataContext context,
        AccountDBServer account,
        Dictionary<long, HexaUnit> existHexaUnitData,
        List<HexaUnit> hexaUnitData)
    {
        foreach (var unit in hexaUnitData)
        {
            existHexaUnitData.Remove(unit.EntityId);
        }

        var movementOrder = existHexaUnitData.Count;

        foreach (var hexaUnit in hexaUnitData)
        {
            movementOrder++;
            var echelonData = await EchelonService.GetConcentratedCampaignEchelon(
                context, account.ServerId, hexaUnit.EntityId);

            if (echelonData == null)
                continue;

            var modified = new HexaUnit
            {
                EntityId = hexaUnit.EntityId,
                HpInfos = CreateHpInfos(echelonData.MainSlotServerIds, echelonData.SupportSlotServerIds),
                DyingInfos = new Dictionary<long, long>(),
                BuffInfos = new Dictionary<long, int>(),
                ActionCountMax = 1,
                Mobility = 1,
                StrategySightRange = 1,
                Id = hexaUnit.Id,
                Location = hexaUnit.Location,
                Rotate = hexaUnit.Rotate,
                IsPlayer = hexaUnit.IsPlayer,
                MovementOrder = movementOrder
            };

            existHexaUnitData.Add(hexaUnit.EntityId, modified);
        }

        return existHexaUnitData;
    }

    private Dictionary<long, long> CreateHpInfos(List<long> mainSlotServerIds, List<long> supportSlotServerIds)
    {
        var hpInfos = new Dictionary<long, long>();

        foreach (var mainSlotServerId in mainSlotServerIds)
        {
            hpInfos[mainSlotServerId] = FullHpRate;
        }

        foreach (var supportSlotServerId in supportSlotServerIds)
        {
            hpInfos[supportSlotServerId] = FullHpRate;
        }

        return hpInfos;
    }

    public async Task<CampaignMainStageSaveDBServer> StartConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignConfirmMainStageRequest stageReq)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, stageReq.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {stageReq.StageUniqueId}");

        var deployedEchelonInfos = stageSaveData.EchelonInfos?.Values.ToList() ?? new List<HexaUnit>();

        stageSaveData.CampaignState = CampaignState.PlayerPhase;
        stageSaveData.CurrentTurn = 1;
        stageSaveData.DeployedEchelonInfos = HexaMapService.DeployHexaUnitList(deployedEchelonInfos);
        
        if (stageSaveData.EchelonInfos != null)
        {
            foreach (var echelon in stageSaveData.EchelonInfos.Values)
            {
                echelon.ActionCount = 1;
            }
        }

        // The start-event activations are seeded at save creation now; Confirm must not clobber
        // whatever has accumulated since (MapMove/ArriveTile events append here on official).
        stageSaveData.ActivatedHexaEventsAndConditions ??= new Dictionary<long, List<long>> { { 0, new List<long> { 0 } } };

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    /// <summary>
    /// Walks an echelon to the tile the player picked. Returns the save plus a snapshot of the moving
    /// echelon as it stood *before* the step — see <see cref="RewindMovedEchelonForWire"/> for why the
    /// response has to show the old position even though the new one is what gets persisted.
    ///
    /// The move is three pieces of state, and this used to write none of them: the unit's Location,
    /// its slot in the global MovementOrder queue, and the ActionCount that stops it moving twice in a
    /// turn. Only a DisplayInfos entry went out, which is just the animation — so the client played the
    /// walk, then the next response re-asserted EchelonInfos with the original tile and the unit
    /// snapped back. Nothing about the move was ever recorded.
    /// </summary>
    public async Task<(CampaignMainStageSaveDBServer Save, HexaUnit? PreMove)> MoveTarget(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignMapMoveRequest moveReq)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, moveReq.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {moveReq.StageUniqueId}");

        stageSaveData.CampaignState = CampaignState.PlayerPhase;

        HexaUnit? preMove = null;
        if (stageSaveData.EchelonInfos != null
            && stageSaveData.EchelonInfos.TryGetValue(moveReq.EchelonEntityId, out var echelon))
        {
            preMove = new HexaUnit
            {
                Location = echelon.Location,
                MovementOrder = echelon.MovementOrder,
                ActionCount = echelon.ActionCount,
            };

            // A fresh HexLocation2D rather than a write through the existing one: the snapshot above
            // holds that reference, and the mapped wire copy shares it too.
            echelon.Location = new HexLocation2D
            {
                x = moveReq.DestPosition.x,
                y = moveReq.DestPosition.y,
                z = moveReq.DestPosition.z,
            };
            echelon.MovementOrder = NextMovementOrder(stageSaveData);
            echelon.ActionCount = Math.Max(0, echelon.ActionCount - 1);
        }

        // Replaced, not appended. DisplayInfos is the "play this now" list, and official's MapMove
        // carries exactly the current step; appending made every response replay the whole run's
        // movement history, growing by one entry per move.
        stageSaveData.DisplayInfos = new List<HexaDisplayInfo>
        {
            HexaMapService.AddHexaDisplayInfo(moveReq.EchelonEntityId, moveReq.DestPosition),
        };

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return (stageSaveData, preMove);
    }

    /// <summary>
    /// The next slot in the movement queue. MovementOrder is one counter shared by the whole force,
    /// not a per-echelon step count: deploy hands out 1 and 2, then every move takes the next value in
    /// turn, so a two-echelon run reads 3, 4, 5, 6... A tactic does not consume a slot.
    /// </summary>
    private static int NextMovementOrder(CampaignMainStageSaveDBServer save)
    {
        var highest = save.EchelonInfos is { Count: > 0 }
            ? save.EchelonInfos.Values.Max(x => x.MovementOrder)
            : 0;

        return highest + 1;
    }

    /// <summary>
    /// Puts the moving echelon back to its pre-step position, order and action count *in the wire copy
    /// only*. Official's MapMove response reports the mover exactly as it stood before the request:
    /// the DisplayInfos entry is what walks it to the new tile, and EchelonInfos catches up in the
    /// following response. Sending the destination in both would have the client sync the unit onto
    /// the tile and then animate it walking from there to itself.
    ///
    /// The mapped copy shares its HexaUnit references with the tracked entity, so the rewind swaps in
    /// a fresh unit through a fresh dictionary rather than writing through the shared one — otherwise
    /// this would quietly undo the move that was just saved.
    /// </summary>
    internal static CampaignMainStageSaveDB RewindMovedEchelonForWire(
        CampaignMainStageSaveDB save,
        long entityId,
        HexaUnit? preMove)
    {
        if (preMove == null
            || save.EchelonInfos == null
            || !save.EchelonInfos.TryGetValue(entityId, out var moved))
            return save;

        save.EchelonInfos = new Dictionary<long, HexaUnit>(save.EchelonInfos)
        {
            [entityId] = new HexaUnit
            {
                EntityId = moved.EntityId,
                HpInfos = moved.HpInfos,
                DyingInfos = moved.DyingInfos,
                BuffInfos = moved.BuffInfos,
                ActionCountMax = moved.ActionCountMax,
                Mobility = moved.Mobility,
                StrategySightRange = moved.StrategySightRange,
                Id = moved.Id,
                Rotate = moved.Rotate,
                IsPlayer = moved.IsPlayer,
                SkillCardHand = moved.SkillCardHand,
                RewardParcelInfosWithDropTacticEntityType = moved.RewardParcelInfosWithDropTacticEntityType,

                Location = preMove.Location,
                MovementOrder = preMove.MovementOrder,
                ActionCount = preMove.ActionCount,
            },
        };

        return save;
    }

    /// <summary>
    /// Remembers which enemy the player just engaged. The response itself is empty — official's
    /// Campaign_EnterTactic reply carries nothing beyond the protocol header — but the request's
    /// EnemyIndex is the only place the engaged unit is ever named, and Campaign_TacticResult needs
    /// it to take that unit off the map.
    /// </summary>
    public async Task<CampaignMainStageSaveDBServer> EnterTactic(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignEnterTacticRequest req)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, req.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.StageUniqueId}");

        stageSaveData.EngagedEnemyEntityId = req.EnemyIndex;

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    public async Task<(CampaignMainStageSaveDBServer Save, CampaignStageHistoryDB History, long TacticRank,
        StrategyClearRewardInfo? ClearReward, CampaignEndBattle EndBattleType,
        ParcelResultDB ParcelResult, List<MissionProgressDB> MissionProgresses)> TacticResult(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignTacticResultRequest req)
    {
        if (req.Summary == null)
            throw new InvalidOperationException("Campaign_TacticResult carried no battle summary");

        var dateTime = account.GameSettings.ServerDateTime();
        var stageSaveData = await GetConcentrateCampaign(context, account, req.Summary.StageId);

        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.Summary.StageId}");

        var campaignChapterExcels = _excelService.GetTable<CampaignChapterExcelT>();
        var chapterUniqueId = campaignChapterExcels.GetChapterIdFromStageId(req.Summary.StageId);
        var stageExcel = _excelService.GetTable<CampaignStageExcelT>()
            .FirstOrDefault(x => x.Id == req.Summary.StageId);
        var tacticRank = CalcTacticRank(req.Summary);

        var historyDb = new CampaignStageHistoryDBServer
        {
            AccountServerId = req.AccountId,
            StageUniqueId = req.Summary.StageId,
            ChapterUniqueId = chapterUniqueId,
            LastPlay = dateTime,
            TodayPlayCount = 0,
            ClearTurnRecord = 0,
            Star1Flag = false,
            Star2Flag = false,
            Star3Flag = false
        };

        stageSaveData.TacticClearTimeMscSum += (long)Math.Floor(req.Summary.EndFrame / 30f) * 1000;
        stageSaveData.EchelonInfos = ChangeConcentratedEchelon(stageSaveData.EchelonInfos, req.Summary, req.Hand);

        var isStageClear = false;
        var endBattleType = CampaignEndBattle.Win;
        var tacticWon = CheckIfCleared(req.Summary);

        if (!tacticWon)
        {
            if (stageSaveData.EchelonInfos != null)
                stageSaveData.EchelonInfos.Remove(req.Summary.Group01Summary.TeamId);
        }
        else
        {
            // Clear the engaged enemy off the hex map. The unit is identified by the EnemyIndex the
            // client sent with Campaign_EnterTactic, not by anything in the battle summary: the
            // summary's Group02 heroes carry character ids (7020201, ...) while EnemyInfos is keyed
            // by hex entity id and holds campaign-unit ids (111110201, ...). Matching Id against
            // CharacterId — the old behaviour — could never be true, so no enemy was ever removed,
            // EnemyClearCount stayed at zero and the stage could not be completed.
            if (stageSaveData.EnemyInfos != null &&
                stageSaveData.EnemyInfos.Remove(stageSaveData.EngagedEnemyEntityId))
            {
                stageSaveData.EnemyClearCount++;
            }

            stageSaveData.EngagedEnemyEntityId = 0;

            // The third star is scored against how many tactics finished at S, so the counter tracks
            // the rank of each battle. Gating it on "the stage is already three-starred" — the old
            // behaviour — made it unreachable: the star it feeds can never be lit before it counts.
            if (tacticRank >= TacticRankS)
                stageSaveData.TacticRankSCount++;

            // The stage clear is the map's own EndBattle event, fired by a HexaConditionUnitDead
            // naming one designated boss — not "the map is empty". On strategymap_1011104 the boss is
            // 10013 while 10017 and 10018 are still standing when official ends the mission, and on
            // the reported failure (stage 1111102) it is 10044 out of 10040-10044. Requiring an empty
            // map made the player mop up units official never asks for, and on the maps where a
            // TileHide deletes an enemy for you it could never be satisfied at all.
            var hexaData = await _hexaMapService.LoadState(stageSaveData.StageUniqueId);
            var endBattle = HexaMapService.FindSatisfiedEndBattle(
                hexaData, stageSaveData.EnemyInfos, stageSaveData.ActivatedHexaEventsAndConditions);

            if (endBattle is { } fired)
            {
                // Official appends the fired event to the activation history in the very packet that
                // clears the stage: {"0":[0],"1":[0],"3":[0]} becomes {...,"2":[0]}. It is also what
                // keeps a replayed 6008 from firing the clear a second time.
                stageSaveData.ActivatedHexaEventsAndConditions ??= new Dictionary<long, List<long>>();
                stageSaveData.ActivatedHexaEventsAndConditions[fired.Event.EventId] = fired.ConditionIds;

                endBattleType = fired.Command.EndBattleType;
                isStageClear = true;
            }
            else if (stageSaveData.EnemyInfos is { Count: 0 })
            {
                // Fallback for a stage whose strategymap dump is missing: LoadState logs a warning and
                // hands back an empty map with no Events, and without this the run could never end.
                // It can only ever fire later than the event rule, never earlier.
                isStageClear = true;
            }

            // Ending the run stops the save being something ContentSave_Get can offer to resume.
            if (isStageClear)
                stageSaveData.IsOpen = false;
        }

        if (isStageClear)
        {
            // The star conditions are stage properties, not battle ones: one for finishing at all,
            // one for finishing inside the par turn count, one for how many tactics ranked S. The
            // per-battle heuristics this replaces lit stars off a single fight and pinned
            // ClearTurnRecord at 1, so a stage cleared on turn 5 reported turn 1 and arbitrary stars.
            historyDb.Star1Flag = true;
            historyDb.Star2Flag = stageExcel != null && stageSaveData.CurrentTurn <= stageExcel.StarConditionTurnCount;
            historyDb.Star3Flag = stageExcel != null && stageSaveData.TacticRankSCount >= stageExcel.StarConditionTacticRankSCount;
            historyDb.ClearTurnRecord = stageSaveData.CurrentTurn;
            historyDb.TacticClearCountWithRankSRecord = stageSaveData.TacticRankSCount;
            historyDb.IsClearedEver = true;
        }

        var existHistory = await context.CampaignStageHistories
            .FirstOrDefaultAsync(x =>
                x.AccountServerId == req.AccountId &&
                x.StageUniqueId == req.Summary.StageId);

        // Read before the merge: the star-total achievement counts stars newly lit by this run, and
        // after MergeExistHistoryWithNew the old flags and the new ones are the same object.
        var starsBefore = existHistory != null ? StarCount(existHistory) : 0;

        if (existHistory != null)
        {
            MergeExistHistoryWithNew(context, existHistory, historyDb, dateTime);
            historyDb = existHistory;
        }
        else
        {
            context.CampaignStageHistories.Add(historyDb);
        }

        // DisplayInfos is per-response, never an accumulation. The stage-clear entry is attached to
        // the wire copy by AttachStageClearForWire rather than persisted here — it carries the whole
        // reward payload and has no business living in the save row.
        stageSaveData.DisplayInfos = new List<HexaDisplayInfo>();

        // Both rewards are once-per-account. The gates read the merged history, so a replayed stage
        // still ends but advertises nothing, which is what official does on a re-clear.
        var grantFirstClear = isStageClear && historyDb.FirstClearRewardReceive == null;
        var grantThreeStar = isStageClear
            && historyDb.Star1Flag && historyDb.Star2Flag && historyDb.Star3Flag
            && historyDb.StarRewardReceive == null;

        if (grantFirstClear) historyDb.FirstClearRewardReceive = dateTime;
        if (grantThreeStar) historyDb.StarRewardReceive = dateTime;

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        var historyMap = historyDb.ToMap(_mapper);

        var (parcelResult, clearReward) = await BuildTacticReward(
            context, account, stageExcel, req.Summary, tacticWon, isStageClear, grantFirstClear, grantThreeStar);

        if (clearReward != null)
            clearReward.CampaignStageHistoryDB = historyMap;

        // Missions tick off the tactic, not off the stage: official sends MissionProgressDBs on every
        // Campaign_TacticResult, including the three that did not finish the stage.
        var missionProgresses = await TickTacticMissions(
            context, account, stageSaveData, tacticWon, isStageClear,
            StarCount(historyDb) - starsBefore);

        return (stageSaveData, historyMap, tacticRank, clearReward, endBattleType, parcelResult, missionProgresses);
    }

    /// <summary>
    /// Everything one Campaign_TacticResult pays out, and the payload the client shows on the
    /// mission-complete screen when that tactic also finished the stage.
    ///
    /// Two economies meet here. Every won tactic — stage cleared or not — pays the stage's
    /// TacticRewardExp to each character that fought it; on official that is the *entire* content of
    /// a non-clearing tactic's ParcelResultDB, six CharacterExp parcels and the CharacterDBs they
    /// levelled. A tactic that also clears the stage additionally pays the once-per-account
    /// first-clear and three-star rows, the rolled per-run drop table, and the stage's AP cost
    /// converted to account exp. Before this, a non-clearing tactic paid nothing at all and a clear
    /// paid only the two once-per-account rows.
    /// </summary>
    private async Task<(ParcelResultDB ParcelResult, StrategyClearRewardInfo? ClearReward)> BuildTacticReward(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignStageExcelT? stageExcel,
        BattleSummary summary,
        bool tacticWon,
        bool isStageClear,
        bool grantFirstClear,
        bool grantThreeStar)
    {
        var firstClear = new List<ParcelResult>();
        var threeStar = new List<ParcelResult>();
        var drops = new List<ParcelResult>();
        var exp = new List<ParcelResult>();

        if (stageExcel != null && tacticWon)
        {
            foreach (var characterId in DeployedCharacterIds(summary, account.ServerId))
                exp.Add(new ParcelResult(ParcelType.CharacterExp, characterId, stageExcel.TacticRewardExp));

            if (isStageClear)
            {
                var rewards = _excelService.GetTable<CampaignStageRewardExcelT>()
                    .GetAllRewardsByGroupId(stageExcel.CampaignStageRewardId)
                    .ToList();

                if (grantFirstClear) firstClear = RewardsTagged(rewards, RewardTag.FirstClear);
                if (grantThreeStar) threeStar = RewardsTagged(rewards, RewardTag.ThreeStar);

                drops = RolledDrops(rewards);

                // Clearing converts the AP the stage cost into account exp, one for one — official's
                // clear of this 10-AP stage reports BaseAccountExp 10. The parcel id is 1, not the 0
                // the older sub-stage paths pass.
                exp.Add(new ParcelResult(
                    ParcelType.AccountExp, AccountExpParcelId, stageExcel.StageEnterCostAmount));
            }
        }

        // One BuildParcel call, deliberately: it opens its own transaction so a second call cannot
        // nest inside it, and every call builds a fresh resolver whose DisplaySequence/ParcelForMission
        // assignment would throw away the first call's.
        //
        // The order is official's reward order — first-clear rows, drops, character exp, account exp,
        // three-star rows. Two cosmetic differences remain and neither changes what is granted:
        // ParcelHandler.Aggregate sums same-key parcels, so a drop of an equipment the first-clear row
        // also awards arrives as one summed entry where official lists two; and currency drops stay in
        // drop order rather than being flushed to the end of the list.
        var granted = firstClear.Concat(drops).Concat(exp).Concat(threeStar).ToList();
        var resolver = await _parcelHandler.BuildParcel(context, account, granted);

        if (!isStageClear)
            return (resolver.ParcelResult, null);

        return (resolver.ParcelResult, new StrategyClearRewardInfo
        {
            FirstClearReward = ToParcelInfos(firstClear),
            ThreeStarReward = ToParcelInfos(threeStar),
            // Official sends these two on every clear, empty when there is nothing in them. The four
            // [JsonIgnore]'d lists on this type are left null so they drop off the wire.
            StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
            EventContentBonusReward = new List<ParcelInfo>(),
            ParcelResultDB = resolver.ParcelResult,
        });
    }

    private static List<ParcelResult> RewardsTagged(
        IEnumerable<CampaignStageRewardExcelT> rewards, RewardTag tag)
        => rewards
            .Where(x => x.RewardTag == tag)
            .Select(x => new ParcelResult(x.StageRewardParcelType, x.StageRewardId, x.StageRewardAmount))
            .ToList();

    /// <summary>
    /// Rolls the stage's per-run drop table. Only the Default-tagged rows take part: the FirstClear
    /// and ThreeStar rows sit in the same table and carry probabilities of their own, so rolling the
    /// whole group — which the sub-stage and sweep paths still do — pays the once-per-account rewards
    /// again on every single clear.
    /// </summary>
    public static List<ParcelResult> RolledDrops(IEnumerable<CampaignStageRewardExcelT> rewards)
        => rewards
            .Where(x => x.RewardTag == RewardTag.Default)
            .Where(x => MathService.GenerateProbability(x.StageRewardProb))
            .Select(x => new ParcelResult(x.StageRewardParcelType, x.StageRewardId, x.StageRewardAmount))
            .ToList();

    /// <summary>
    /// The characters that fought this tactic, in the order the summary lists them: strikers first,
    /// then specials. A borrowed assist is skipped — its exp belongs to the friend who lent it, and
    /// there is no character row on this account for UpdateCharacterExp to find anyway.
    /// </summary>
    public static IEnumerable<long> DeployedCharacterIds(BattleSummary summary, long accountServerId)
    {
        var group = summary?.Group01Summary;
        if (group == null) yield break;

        var deployed = (group.Heroes ?? Enumerable.Empty<HeroSummary>())
            .Concat(group.Supporters ?? Enumerable.Empty<HeroSummary>());

        var seen = new HashSet<long>();
        foreach (var hero in deployed)
        {
            if (hero.OwnerAccountId != 0 && hero.OwnerAccountId != accountServerId) continue;
            if (seen.Add(hero.CharacterId)) yield return hero.CharacterId;
        }
    }

    private static long StarCount(CampaignStageHistoryDBServer history)
        => (history.Star1Flag ? 1 : 0) + (history.Star2Flag ? 1 : 0) + (history.Star3Flag ? 1 : 0);

    /// <summary>
    /// Advances the mission counters a tactic moves. Official attaches MissionProgressDBs to all four
    /// Campaign_TacticResult responses in the capture — the three mid-stage tactics tick the per-battle
    /// counters and the fourth ticks those plus the per-stage ones — and we attached them to none, so
    /// a daily like "clear 3 tactics" never moved while a campaign was being played.
    /// </summary>
    private async Task<List<MissionProgressDB>> TickTacticMissions(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignMainStageSaveDBServer stageSaveData,
        bool tacticWon,
        bool isStageClear,
        long starsGained)
    {
        if (!tacticWon) return [];

        var progresses = new List<MissionProgressDB>();

        void Tick(MissionCompleteConditionType condition, long amount = 1, long? parameter = null)
            => progresses.AddRange(
                _missionService.UpdateMissionProgress(context, account, condition, amount, parameter));

        Tick(MissionCompleteConditionType.Reset_ClearTaticBattleCount);

        if (isStageClear)
        {
            Tick(MissionCompleteConditionType.Achieve_ClearCampaignStageCount);
            Tick(MissionCompleteConditionType.Reset_ClearSpecificCampaignStageCount,
                parameter: stageSaveData.StageUniqueId);

            // The turn mission is a personal best, so the amount is the turn count this run finished
            // in rather than an increment — see MissionService.IsLowerBetter.
            Tick(MissionCompleteConditionType.Reset_CompleteCampaignStageMinimumTurn,
                stageSaveData.CurrentTurn, stageSaveData.StageUniqueId);

            if (starsGained > 0)
                Tick(MissionCompleteConditionType.Achieve_TotalGetClearStarCount, starsGained);
        }

        // UpdateMissionProgress only stages its changes — every other caller saves for it.
        await context.SaveChangesAsync();

        // One condition can match a mission more than once across the calls above; the client keys the
        // list by MissionUniqueId, so send each row once.
        return progresses
            .GroupBy(x => x.MissionUniqueId)
            .Select(g => g.Last())
            .ToList();
    }

    private static List<ParcelInfo> ToParcelInfos(IEnumerable<ParcelResult> parcels)
        => parcels.Select(r => new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = r.Type, Id = r.Id },
            Amount = r.Amount,
            Multiplier = BasisPoint.One,
            Probability = BasisPoint.One
        }).ToList();

    /// <summary>
    /// Ends the mission. Firing the map's EndBattle event is the stage clear — there is no protocol
    /// for it and the client sends no further campaign request, so the one and only signal is an
    /// EndBattle entry on the clearing Campaign_TacticResult. Without it the client sat on the map
    /// with the boss dead and End Turn as the only thing left to press.
    ///
    /// Official's entry carries exactly Type, Parameter and StageRewardInfo; EntityId, UniqueId and
    /// Location stay at their defaults and are dropped by DefaultValueHandling.Ignore. This runs on
    /// the mapped wire copy, after ShapeForWire has nulled the empty DisplayInfos.
    /// </summary>
    internal static CampaignMainStageSaveDB AttachStageClearForWire(
        CampaignMainStageSaveDB save,
        StrategyClearRewardInfo? clearReward,
        CampaignEndBattle endBattleType = CampaignEndBattle.Win)
    {
        if (clearReward == null)
            return save;

        save.DisplayInfos = new List<HexaDisplayInfo>
        {
            new()
            {
                Type = HexaDisplayType.EndBattle,
                // Parameter is what the client reads as IsWin. None would serialize as 0, which
                // DefaultValueHandling.Ignore drops entirely, and a missing Parameter reads as a
                // loss — a victory would show the retreat screen. No dump carries None, so coerce.
                Parameter = (long)(endBattleType == CampaignEndBattle.None
                    ? CampaignEndBattle.Win
                    : endBattleType),
                StageRewardInfo = clearReward,
            },
        };

        return save;
    }

    private Dictionary<long, HexaUnit>? ChangeConcentratedEchelon(
        Dictionary<long, HexaUnit>? existHexaUnitData,
        BattleSummary battleSummary,
        SkillCardHand? hand = null)
    {
        if (existHexaUnitData == null || battleSummary.Group01Summary == null)
            return existHexaUnitData;

        foreach (var kvp in existHexaUnitData.Where(x => x.Value.EntityId == battleSummary.Group01Summary.TeamId))
        {
            var (hpInfos, dyingInfos) = ChangeHpInfos(
                battleSummary.Group01Summary.Heroes,
                battleSummary.Group01Summary.Supporters,
                kvp.Value.HpInfos,
                kvp.Value.DyingInfos
            );

            kvp.Value.HpInfos = hpInfos;
            kvp.Value.DyingInfos = dyingInfos;

            if (hand != null)
            {
                kvp.Value.SkillCardHand = hand;
            }

            // No MovementOrder bump here: the counter belongs to the movement queue, and official's
            // tactic results leave every echelon's order untouched. Fighting is not a move.

            kvp.Value.Rotate = null;
            kvp.Value.BuffInfos = null;
            kvp.Value.ActionCount = 0;
        }

        return existHexaUnitData;
    }

    /// <summary>
    /// Folds a battle's outcome into an echelon's HpInfos and DyingInfos.
    ///
    /// Both are a *ledger for the whole mission*, not a per-battle readout: official's HpInfos is a
    /// constant six-member dictionary whose values move as the squad takes damage, and an echelon that
    /// fought once keeps its damaged entries for every response afterwards. So this merges into what
    /// the echelon already carries rather than rebuilding from the summary.
    ///
    /// Liveness comes from <c>DeadFrame</c>, the only field that actually reports it. Keying off
    /// <c>HPRateAfter == 0</c> instead is wrong twice over: a Special who never took the field reports
    /// <c>HPRateBefore == HPRateAfter == 0</c> with <c>DeadFrame == -1</c>, so the whole support slot
    /// got filed as downed — official sends those two students back at 10000 with DyingInfos empty.
    /// Off-field slots keep whatever the ledger already had for them, which is what makes an
    /// untouched student read as healthy instead of dead.
    /// </summary>
    internal static (Dictionary<long, long> HpInfos, Dictionary<long, long> DyingInfos) ChangeHpInfos(
        HeroSummaryCollection? mainHeroSummary,
        HeroSummaryCollection? supportHeroSummary,
        IReadOnlyDictionary<long, long>? existingHpInfos = null,
        IReadOnlyDictionary<long, long>? existingDyingInfos = null)
    {
        var hpInfos = existingHpInfos == null
            ? new Dictionary<long, long>()
            : new Dictionary<long, long>(existingHpInfos);

        var dyingInfos = existingDyingInfos == null
            ? new Dictionary<long, long>()
            : new Dictionary<long, long>(existingDyingInfos);

        void Collect(HeroSummaryCollection? summary)
        {
            if (summary == null)
                return;

            foreach (var hero in summary)
            {
                // Downed in this battle: the map tracks them under DyingInfos instead.
                if (hero.DeadFrame != -1)
                {
                    hpInfos.Remove(hero.ServerId);
                    dyingInfos[hero.ServerId] = hero.HPRateAfter;
                    continue;
                }

                // Already down from an earlier battle. They still ride along in the summary as an
                // untouched slot, and taking that at face value would stand them back up.
                if (dyingInfos.ContainsKey(hero.ServerId))
                    continue;

                // Never took the field — a Special who was not called in, or a slot the battle did not
                // reach. Nothing happened to them, so the ledger keeps its existing value.
                if (hero.HPRateBefore == 0 && hero.HPRateAfter == 0)
                {
                    if (!hpInfos.ContainsKey(hero.ServerId))
                        hpInfos[hero.ServerId] = FullHpRate;
                    continue;
                }

                hpInfos[hero.ServerId] = hero.HPRateAfter;
            }
        }

        Collect(mainHeroSummary);
        Collect(supportHeroSummary);

        return (hpInfos, dyingInfos);
    }

    /// <summary>
    /// Mirrors the client's MX.GameLogic.Service.CampaignService.CalcTacticRank(bool, TimeSpan, int, int):
    ///
    ///     rank = win ? (clearMs &lt;= TacticRankClearTime ? 2 : 1) : 0;
    ///     return aliveCount == heroCount ? rank + 1 : rank;
    ///
    /// so a win runs 1..3 and a defeat 0..1. This matters far beyond the star display: on the
    /// battle-skip path the client takes the win/lose flag straight from this response field
    /// (CampaignTask.HandleCampaignTacticResultResponseMessage tests it &gt; 0 when it has no local
    /// battle to read), so leaving TacticRank at its default 0 reported every skipped victory as a
    /// defeat. Official sends 3 for a clean, quick win.
    ///
    /// The par time comes from ConstCommonExcel.TacticRankClearTime, which our excel model does not
    /// carry; 120s matches the threshold CalcAllEnemiesDefeatedInTime already uses and is consistent
    /// with the official capture, where battles of up to 85s all scored 3.
    /// </summary>
    internal static long CalcTacticRank(BattleSummary summary)
    {
        var heroes = summary.Group01Summary?.Heroes;
        if (heroes == null)
            return 0;

        var isPlayerWin = !summary.IsAbort && summary.EndType == BattleEndType.Clear;
        var clearMsec = (long)Math.Floor(summary.EndFrame / 30f) * 1000;

        var rank = isPlayerWin
            ? (clearMsec <= TacticRankClearTimeMsec ? 2 : 1)
            : 0;

        var aliveCount = heroes.Count(x => x.DeadFrame == -1);
        return aliveCount == heroes.Count ? rank + 1 : rank;
    }

    public async Task<CampaignMainStageSaveDBServer> EndTurn(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignEndTurnRequest req)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, req.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.StageUniqueId}");

        // Whatever the last move queued up has already been played by the client. Leaving it in place
        // would replay it on the turn change, and again on every response until something else
        // overwrote it.
        stageSaveData.DisplayInfos = new List<HexaDisplayInfo>();

        if (stageSaveData.CampaignState == CampaignState.PlayerPhase)
        {
            stageSaveData.CampaignState = CampaignState.EnemyPhase;
        }
        else if (stageSaveData.CampaignState == CampaignState.EnemyPhase)
        {
            stageSaveData.CampaignState = CampaignState.PlayerPhase;

            if (stageSaveData.EchelonInfos != null)
            {
                foreach (var echelon in stageSaveData.EchelonInfos.Values)
                {
                    // Restored to full, not incremented: a new player phase gives every echelon its
                    // allowance back. Adding one would bank the unspent action of an echelon that
                    // stood still and let it move twice next turn.
                    echelon.ActionCount = echelon.ActionCountMax;
                }
            }

            stageSaveData.CurrentTurn++;
        }

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    private bool CheckIfCleared(BattleSummary summary)
    {
        return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
    }

    // CalcStrategySkipStarGoals and its all-enemies-dead/in-time/all-alive helpers used to live here.
    // They scored the stars off a single battle summary — the same three questions a *tactic* answers,
    // not a stage — and pinned ClearTurnRecord at 1. The real conditions come from CampaignStageExcel
    // and are evaluated once, on the tactic that empties the map; see TacticResult. The other campaign
    // managers keep their own copies.

    private void MergeExistHistoryWithNew(
        SchaleDataContext context,
        CampaignStageHistoryDBServer existHistoryDb,
        CampaignStageHistoryDBServer newHistoryDb,
        DateTime dateTime)
    {
        existHistoryDb.Star1Flag = existHistoryDb.Star1Flag || newHistoryDb.Star1Flag;
        existHistoryDb.Star2Flag = existHistoryDb.Star2Flag || newHistoryDb.Star2Flag;
        existHistoryDb.Star3Flag = existHistoryDb.Star3Flag || newHistoryDb.Star3Flag;
        existHistoryDb.IsClearedEver = existHistoryDb.IsClearedEver || newHistoryDb.IsClearedEver;

        // Both are best-ever records, and only a run that actually cleared reports one — a defeat
        // leaves them at 0 and must not overwrite what an earlier clear achieved. Fewer turns is
        // better; more S-rank tactics is better.
        if (newHistoryDb.ClearTurnRecord > 0 &&
            (existHistoryDb.ClearTurnRecord == 0 || newHistoryDb.ClearTurnRecord < existHistoryDb.ClearTurnRecord))
        {
            existHistoryDb.ClearTurnRecord = newHistoryDb.ClearTurnRecord;
        }

        if (newHistoryDb.TacticClearCountWithRankSRecord > existHistoryDb.TacticClearCountWithRankSRecord)
            existHistoryDb.TacticClearCountWithRankSRecord = newHistoryDb.TacticClearCountWithRankSRecord;

        existHistoryDb.TodayPlayCount += 1;
        existHistoryDb.LastPlay = dateTime;

        context.CampaignStageHistories.Update(existHistoryDb);
    }

    public async Task<Schale.MX.Data.CampaignStageInfo?> GetStageInfo(long stageUniqueId)
    {
        await Task.CompletedTask;
        var stages = _excelService.GetTable<CampaignStageExcelT>();
        var stageExcel = stages.FirstOrDefault(x => x.Id == stageUniqueId);
        
        if (stageExcel == null)
            return null;

        long areaId = stageUniqueId / 1000000;
        long chapterId = (stageUniqueId / 10000) % 100;
        long chapterUniqueId = areaId * 1000 + chapterId;

        return new Schale.MX.Data.CampaignStageInfo
        {
            UniqueId = stageExcel.Id,
            DevName = stageExcel.Name,
            ChapterNumber = areaId,
            StoryUniqueId = chapterUniqueId,
            ChapterUniqueId = chapterUniqueId,
            StageNumber = stageExcel.StageNumber,
            RecommandLevel = stageExcel.RecommandLevel,
            StrategyMap = stageExcel.StrategyMap,
            BackgroundBG = stageExcel.StrategyMapBG,
            StageTopography = stageExcel.StageTopography,
            StageEnterCostAmount = stageExcel.StageEnterCostAmount,
            MaxTurn = stageExcel.MaxTurn,
            MaxEchelonCount = stageExcel.StageEnterEchelonCount,
            StageDifficulty = Schale.FlatData.StageDifficulty.Normal,
            ContentType = stageExcel.ContentType,
            StrategyEnvironment = stageExcel.StrategyEnvironment,
            GroundId = stageExcel.GroundId,
            StrategySkipGroundId = stageExcel.StrategySkipGroundId,
            BattleDuration = stageExcel.BattleDuration,
            BGMId = stageExcel.BGMId,
            TacticRewardExp = stageExcel.TacticRewardExp,
            FixedEchelonId = stageExcel.FixedEchelonId,
            // EchelonExtensionType is a CampaignStageInfo field with no CampaignStageExcel column in
            // this data version, so it stays at Base and DefaultValueHandling drops it off the wire —
            // which is what official does too. It used to read the misaligned tail slot.
            StarConditionTurnCount = stageExcel.StarConditionTurnCount,
            StarConditionSTacticRackCount = stageExcel.StarConditionTacticRankSCount,
            IsDeprecated = stageExcel.Deprecated
        };
    }

    // A ProcessStartEvents stub used to sit here: it walked every HexaConditionStartCampaign event
    // and had empty bodies for the spawn commands, no callers, and no way to signal anything. Start
    // events need no execution — CreateConcentrateCampaign seeds the map with everything they would
    // spawn and BuildStartEventActivations records them as already fired.
    //
    // The live seam for evaluating the hexa event machine is HexaMapService.FindSatisfiedEndBattle,
    // which TacticResult calls to detect the stage clear. Grow that when TileHide, UnitDie and the
    // ArriveTile conditions become worth running.
}
