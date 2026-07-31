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
    // ConstCommonExcel.TacticRankClearTime on the client, and our ConstCommonExcel model carries no fields, so the par time is pinned here; 120s is also the threshold CalcAllEnemiesDefeatedInTime uses
    private const long TacticRankClearTimeMsec = 120 * 1000;

    // HpInfos is a rate on 0..10000, not absolute hp
    private const long FullHpRate = 10000L;

    private const long TacticRankS = 3L;

    // AccountExp has no meaningful id of its own (the sub-stage paths pass 0) but the capture says 1, and the id is what the client keys the ParcelForMission entry by
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

    // ContentSave_Get carries no stage id - the client is only asking whether it is mid-run at all - so the newest still-open save across every stage is the answer.
    // BeforeStart does not count: Campaign_EnterMainStage creates the row while the player is still on echelon select, and there is no battle to resume into until Campaign_ConfirmMainStage moves it to PlayerPhase.
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

    // retires open runs so ContentSave_Get stops offering them; a stage id closes just that run (ContentSave_Discard names one), null closes all of them
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

        // entering a stage abandons whatever the player was in before it - only one campaign run can be open at a time, and the new one is what ContentSave_Get owes from here on
        await CloseConcentrateCampaigns(context, account);

        // official's save carries the stage's enter cost (e.g. 10 AP) as a parcel list; it drives the fee display and the retreat refund
        var stageExcel = _excelService.GetTable<CampaignStageExcelT>()
            .FirstOrDefault(x => x.Id == stageUniqueId);
        List<ParcelInfo> entranceFee = stageExcel is null
            ? []
            : ParcelInfo.CreateParcelInfo(
                stageExcel.StageEnterCostType, stageExcel.StageEnterCostId, stageExcel.StageEnterCostAmount);

        // DB row keeps every collection non-null (NOT NULL columns); ShapeForWire trims the wire copy down to official's key set at the handler
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

    // Official serializes these six save collections only once they have content: EchelonInfos first appears at DeployEchelon, DeployedEchelonInfos at ConfirmMainStage, DisplayInfos at MapMove, and WithdrawInfos/StrategyObjectRewards/StrategyObjectHistory never in the captured flow. Sending them non-null-but-empty at map entry mislabels a fresh run as an in-progress one.
    // The DB entity keeps them non-null, so the trimming has to happen on the mapped wire copy that every campaign response carries.
    internal static CampaignMainStageSaveDB ShapeForWire(CampaignMainStageSaveDB save)
    {
        if (save.EchelonInfos is { Count: 0 }) save.EchelonInfos = null;
        if (save.WithdrawInfos is { Count: 0 }) save.WithdrawInfos = null;
        if (save.StrategyObjectRewards is { Count: 0 }) save.StrategyObjectRewards = null;
        if (save.StrategyObjectHistory is { Count: 0 }) save.StrategyObjectHistory = null;
        if (save.DisplayInfos is { Count: 0 }) save.DisplayInfos = null;
        if (save.DeployedEchelonInfos is { Count: 0 }) save.DeployedEchelonInfos = null;
        // EnemyInfos is the odd one out: populated from map entry onwards, empty only once the last enemy dies, and official drops the key entirely on that clearing TacticResult
        if (save.EnemyInfos is { Count: 0 }) save.EnemyInfos = null;
        return save;
    }

    // Official's fresh-save 6001 already carries {"0":[0]}: every event whose conditions include HexaConditionStartCampaign is marked fired ({EventId: [ConditionIds]}) because the save's EnemyInfos/StrategyObjects already contain the entities those events' spawn commands create.
    // Sending {} instead has the client re-evaluate the start event against the pre-populated map while it constructs the strategy scene, and the map never opens: the 6001 callback never completes, no Echelon_List follows, and the client replays 6001 on reconnect.
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

        // start-event activations are seeded at save creation, so Confirm must not clobber what has accumulated since (MapMove/ArriveTile events append here on official)
        stageSaveData.ActivatedHexaEventsAndConditions ??= new Dictionary<long, List<long>> { { 0, new List<long> { 0 } } };

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    // Returns the save plus a snapshot of the moving echelon as it stood BEFORE the step; RewindMovedEchelonForWire has the reason the response shows the old position while the new one is what gets persisted.
    // A move is three pieces of state and all three have to be written: the unit's Location, its slot in the global MovementOrder queue, and the ActionCount that stops it moving twice in a turn. A DisplayInfos entry alone is only the animation - the client plays the walk, the next response re-asserts EchelonInfos with the original tile, and the unit snaps back.
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

            // fresh HexLocation2D rather than a write through the existing one - the snapshot above holds that reference, and the mapped wire copy shares it too
            echelon.Location = new HexLocation2D
            {
                x = moveReq.DestPosition.x,
                y = moveReq.DestPosition.y,
                z = moveReq.DestPosition.z,
            };
            echelon.MovementOrder = NextMovementOrder(stageSaveData);
            echelon.ActionCount = Math.Max(0, echelon.ActionCount - 1);
        }

        // Replaced, not appended: DisplayInfos is the "play this now" list and official's MapMove carries exactly the current step, so appending makes every response replay the whole run's movement history, one extra entry per move.
        stageSaveData.DisplayInfos = new List<HexaDisplayInfo>
        {
            HexaMapService.AddHexaDisplayInfo(moveReq.EchelonEntityId, moveReq.DestPosition),
        };

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return (stageSaveData, preMove);
    }

    // MovementOrder is one counter shared by the whole force, not a per-echelon step count: deploy hands out 1 and 2, then every move takes the next value in turn, so a two-echelon run reads 3, 4, 5, 6. A tactic does not consume a slot.
    private static int NextMovementOrder(CampaignMainStageSaveDBServer save)
    {
        var highest = save.EchelonInfos is { Count: > 0 }
            ? save.EchelonInfos.Values.Max(x => x.MovementOrder)
            : 0;

        return highest + 1;
    }

    // Puts the mover back to its pre-step position, order and action count in the wire copy only.
    // Official's MapMove reports the mover exactly as it stood before the request: the DisplayInfos entry walks it to the new tile and EchelonInfos catches up in the following response, so sending the destination in both would have the client sync the unit onto the tile and then animate it walking from there to itself.
    // The mapped copy shares its HexaUnit references with the tracked entity, so the rewind swaps in a fresh unit through a fresh dictionary rather than writing through the shared one, which would quietly undo the move that was just saved.
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

    // The response is empty (official's Campaign_EnterTactic reply carries nothing beyond the protocol header) but the request's EnemyIndex is the only place the engaged unit is ever named, and Campaign_TacticResult needs it to take that unit off the map.
    public async Task<CampaignMainStageSaveDBServer> EnterTactic(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignEnterTacticRequest req)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, req.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.StageUniqueId}");

        stageSaveData.EngagedEnemyEntityId = req.EnemyIndex;

        // enemy-phase tactic: the client just played the walks up to and including the engaging enemy, so those become real before the battle
        if (stageSaveData.CampaignState == CampaignState.EnemyPhase)
            ApplyEnemyMoves(stageSaveData, req.EnemyIndex);

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

        // read before the win branch clears them - the enemy-phase continuation below resumes off the engaged id
        var engagedEnemyId = stageSaveData.EngagedEnemyEntityId;
        var wasEnemyPhase = stageSaveData.CampaignState == CampaignState.EnemyPhase;

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
            // The engaged unit is named by the EnemyIndex the client sent with Campaign_EnterTactic, never by anything in the battle summary: the summary's Group02 heroes carry character ids (7020201, ...) while EnemyInfos is keyed by hex entity id and holds campaign-unit ids (111110201, ...).
            // Matching Id against CharacterId can never be true, so nothing gets removed, EnemyClearCount stays at zero and the stage cannot be completed.
            if (stageSaveData.EnemyInfos != null &&
                stageSaveData.EnemyInfos.Remove(stageSaveData.EngagedEnemyEntityId))
            {
                stageSaveData.EnemyClearCount++;
            }

            stageSaveData.EngagedEnemyEntityId = 0;

            // the third star is scored against how many tactics finished at S, so the counter tracks every battle; it feeds the star and cannot be gated on it
            if (tacticRank >= TacticRankS)
                stageSaveData.TacticRankSCount++;

            // The stage clear is the map's own EndBattle event, fired by a HexaConditionUnitDead naming one designated boss, not "the map is empty": on strategymap_1011104 the boss is 10013 while 10017 and 10018 are still standing when official ends the mission, and on stage 1111102 it is 10044 out of 10040-10044.
            // Requiring an empty map makes the player mop up units official never asks for, and on maps where a TileHide deletes an enemy for you it can never be satisfied at all.
            var hexaData = await _hexaMapService.LoadState(stageSaveData.StageUniqueId);
            var endBattle = HexaMapService.FindSatisfiedEndBattle(
                hexaData, stageSaveData.EnemyInfos, stageSaveData.ActivatedHexaEventsAndConditions);

            if (endBattle is { } fired)
            {
                // Official appends the fired event to the activation history in the very packet that clears the stage - {"0":[0],"1":[0],"3":[0]} becomes {...,"2":[0]} - and it is also what keeps a replayed 6008 from firing the clear twice.
                stageSaveData.ActivatedHexaEventsAndConditions ??= new Dictionary<long, List<long>>();
                stageSaveData.ActivatedHexaEventsAndConditions[fired.Event.EventId] = fired.ConditionIds;

                endBattleType = fired.Command.EndBattleType;
                isStageClear = true;
            }
            else if (stageSaveData.EnemyInfos is { Count: 0 })
            {
                // Fallback for a stage whose strategymap dump is missing: LoadState warns and hands back an empty map with no Events, so without this the run could never end. It can only fire later than the event rule, never earlier.
                isStageClear = true;
            }

            // ending the run stops ContentSave_Get offering the save as resumable
            if (isStageClear)
                stageSaveData.IsOpen = false;
        }

        if (isStageClear)
        {
            // star conditions are stage properties, not battle ones: one for finishing at all, one for finishing inside the par turn count, one for how many tactics ranked S
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

        // read before the merge - the star-total achievement counts only stars newly lit by this run, and after MergeExistHistoryWithNew the old flags and the new ones are the same object
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

        // DisplayInfos is per-response, never an accumulation; the stage-clear entry carries the whole reward payload, so AttachStageClearForWire hangs it on the wire copy instead of persisting it in the save row.
        // exception: a tactic fought mid-enemy-phase interrupted the decision pass, so the enemies after the engaged one still owe their moves and this response is how the client gets them
        if (wasEnemyPhase && !isStageClear)
        {
            DecideEnemyMoves(
                await _hexaMapService.LoadState(stageSaveData.StageUniqueId), stageSaveData, engagedEnemyId,
                _excelService.GetTable<CampaignUnitExcelT>(), _excelService.GetTable<CampaignStrategyObjectExcelT>());
        }
        else
        {
            stageSaveData.DisplayInfos = new List<HexaDisplayInfo>();
        }

        // Both rewards are once-per-account, and the gates read the merged history, so a replayed stage still ends but advertises nothing - official does the same on a re-clear.
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

        // missions tick off the tactic, not the stage: official sends MissionProgressDBs on every Campaign_TacticResult, including the three that did not finish the stage
        var missionProgresses = await TickTacticMissions(
            context, account, stageSaveData, tacticWon, isStageClear,
            StarCount(historyDb) - starsBefore);

        return (stageSaveData, historyMap, tacticRank, clearReward, endBattleType, parcelResult, missionProgresses);
    }

    // Every won tactic, stage cleared or not, pays the stage's TacticRewardExp to each character that fought it; on official that is the entire content of a non-clearing tactic's ParcelResultDB, six CharacterExp parcels and the CharacterDBs they levelled.
    // A tactic that also clears the stage pays the once-per-account first-clear and three-star rows on top, plus the rolled per-run drop table and the stage's AP cost converted to account exp. The clear payload is what the client shows on the mission-complete screen.
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

                // clearing converts the AP the stage cost into account exp one for one - official's clear of this 10-AP stage reports BaseAccountExp 10
                exp.Add(new ParcelResult(
                    ParcelType.AccountExp, AccountExpParcelId, stageExcel.StageEnterCostAmount));
            }
        }

        // One BuildParcel call, deliberately: it opens its own transaction so a second call cannot nest inside it, and every call builds a fresh resolver whose DisplaySequence/ParcelForMission assignment would throw away the first call's. Order is official's: first-clear rows, drops, character exp, account exp, three-star rows.
        // Two cosmetic differences remain and neither changes what is granted - ParcelHandler.Aggregate sums same-key parcels, so a drop of an equipment the first-clear row also awards arrives as one summed entry where official lists two, and currency drops stay in drop order rather than being flushed to the end of the list.
        var granted = firstClear.Concat(drops).Concat(exp).Concat(threeStar).ToList();
        var resolver = await _parcelHandler.BuildParcel(context, account, granted);

        if (!isStageClear)
            return (resolver.ParcelResult, null);

        return (resolver.ParcelResult, new StrategyClearRewardInfo
        {
            FirstClearReward = ToParcelInfos(firstClear),
            ThreeStarReward = ToParcelInfos(threeStar),
            // official sends these two on every clear, empty when there is nothing in them; the four [JsonIgnore]'d lists on this type stay null so they drop off the wire
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

    // Only Default-tagged rows take part. FirstClear and ThreeStar rows sit in the same table with probabilities of their own, so rolling the whole group - which the sub-stage and sweep paths still do - pays the once-per-account rewards again on every single clear.
    public static List<ParcelResult> RolledDrops(IEnumerable<CampaignStageRewardExcelT> rewards)
        => rewards
            .Where(x => x.RewardTag == RewardTag.Default)
            .Where(x => MathService.GenerateProbability(x.StageRewardProb))
            .Select(x => new ParcelResult(x.StageRewardParcelType, x.StageRewardId, x.StageRewardAmount))
            .ToList();

    // summary order: strikers first, then specials. A borrowed assist is skipped because its exp belongs to the friend who lent it, and there is no character row on this account for UpdateCharacterExp to find anyway.
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

    // Official attaches MissionProgressDBs to all four Campaign_TacticResult responses in the capture: the three mid-stage tactics tick the per-battle counters, the fourth ticks those plus the per-stage ones. Dailies like "clear 3 tactics" ride on this.
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

            // personal best, not an increment: the amount is the turn count this run finished in (MissionService.IsLowerBetter)
            Tick(MissionCompleteConditionType.Reset_CompleteCampaignStageMinimumTurn,
                stageSaveData.CurrentTurn, stageSaveData.StageUniqueId);

            if (starsGained > 0)
                Tick(MissionCompleteConditionType.Achieve_TotalGetClearStarCount, starsGained);
        }

        // UpdateMissionProgress only stages its changes, every caller saves for it
        await context.SaveChangesAsync();

        // one condition can match a mission more than once across the calls above, and the client keys the list by MissionUniqueId, so each row goes out once
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

    // Firing the map's EndBattle event is the stage clear, and there is no protocol for it - the client sends no further campaign request, so the one and only signal is an EndBattle entry on the clearing Campaign_TacticResult. Without it the client sits on the map with the boss dead and End Turn as the only thing left to press.
    // Official's entry carries exactly Type, Parameter and StageRewardInfo; EntityId, UniqueId and Location stay at their defaults and DefaultValueHandling.Ignore drops them. Runs on the mapped wire copy, after ShapeForWire has nulled the empty DisplayInfos.
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
                // Parameter is what the client reads as IsWin, and None would serialize as 0, which DefaultValueHandling.Ignore drops entirely - a missing Parameter reads as a loss and a victory would show the retreat screen. No dump carries None, so coerce it.
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

            // no MovementOrder bump: the counter belongs to the movement queue and official's tactic results leave every echelon's order untouched

            kvp.Value.Rotate = null;
            kvp.Value.BuffInfos = null;
            kvp.Value.ActionCount = 0;
        }

        return existHexaUnitData;
    }

    // HpInfos and DyingInfos are a ledger for the whole mission, not a per-battle readout: official's HpInfos is a constant six-member dictionary whose values move as the squad takes damage, and an echelon that fought once keeps its damaged entries for every response afterwards, so this merges into what the echelon already carries instead of rebuilding from the summary.
    // Liveness comes from DeadFrame, the only field that actually reports it. Keying off HPRateAfter == 0 is wrong twice over: a Special who never took the field reports HPRateBefore == HPRateAfter == 0 with DeadFrame == -1, so the whole support slot gets filed as downed where official sends those two students back at 10000 with DyingInfos empty.
    // Off-field slots keep whatever the ledger already had, which is what makes an untouched student read as healthy.
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

                // already down from an earlier battle - they still ride along in the summary as an untouched slot, and taking that at face value would stand them back up
                if (dyingInfos.ContainsKey(hero.ServerId))
                    continue;

                // never took the field (a Special who was not called in, or a slot the battle did not reach), so the ledger keeps its existing value
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

    // Mirrors the client's MX.GameLogic.Service.CampaignService.CalcTacticRank(bool, TimeSpan, int, int): a win scores 2 inside the par time and 1 outside it, a defeat 0, then +1 if nobody died. So a win runs 1..3 and a defeat 0..1.
    // Not only the star display - on the battle-skip path CampaignTask.HandleCampaignTacticResultResponseMessage has no local battle to read and tests this field > 0 for the win/lose flag, so a default 0 reports a skipped victory as a defeat. Official sends 3 for a clean, quick win, and every battle up to 85s in the capture scored 3.
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

    // the enemy phase is two EndTurns: the first answers EnemyPhase with a MoveUnit display entry per acting enemy (EnemyInfos stays put, the client walks the entries), the second - or the EnterTactic that interrupts - makes the moves real
    public async Task<CampaignMainStageSaveDBServer> EndTurn(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignEndTurnRequest req)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, req.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.StageUniqueId}");

        if (stageSaveData.CampaignState == CampaignState.PlayerPhase)
        {
            // whatever the last move queued up has already been played by the client; leaving it in place replays it on the turn change, and again on every response until something else overwrites it
            stageSaveData.DisplayInfos = new List<HexaDisplayInfo>();
            stageSaveData.CampaignState = CampaignState.EnemyPhase;

            DecideEnemyMoves(
                await _hexaMapService.LoadState(stageSaveData.StageUniqueId), stageSaveData, long.MinValue,
                _excelService.GetTable<CampaignUnitExcelT>(), _excelService.GetTable<CampaignStrategyObjectExcelT>());
        }
        else if (stageSaveData.CampaignState == CampaignState.EnemyPhase)
        {
            // an enemy on (or walking onto) an echelon keeps the phase open until that battle resolves
            if (!HasEncounter(stageSaveData))
            {
                ApplyEnemyMoves(stageSaveData);
                stageSaveData.DisplayInfos = new List<HexaDisplayInfo>();
                stageSaveData.CampaignState = CampaignState.PlayerPhase;

                if (stageSaveData.EchelonInfos != null)
                {
                    foreach (var echelon in stageSaveData.EchelonInfos.Values)
                    {
                        // restored to full, not incremented - a new player phase hands every echelon its allowance back, where adding one would bank the unspent action of an echelon that stood still and let it move twice next turn
                        echelon.ActionCount = echelon.ActionCountMax;
                    }
                }

                stageSaveData.CurrentTurn++;
            }
        }

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    // mirrors the client's CampaignService.DecideAIDestination: id order, Guard engages in reach, Pursuit engages or closes distance, the first engage stops the pass (lastAiUnitIndex resumes it after the battle)
    internal static void DecideEnemyMoves(
        HexaTileMap hexaData,
        CampaignMainStageSaveDBServer save,
        long lastAiUnitIndex,
        List<CampaignUnitExcelT> unitExcels,
        List<CampaignStrategyObjectExcelT> strategyObjectExcels)
    {
        if (save.EnemyInfos == null || save.EnemyInfos.Count == 0 || save.EchelonInfos == null || save.EchelonInfos.Count == 0)
            return;

        var tiles = new Dictionary<(int x, int y, int z), AiTile>();

        if (hexaData.HexaTileList != null)
        {
            for (var i = 0; i < hexaData.HexaTileList.Count; i++)
            {
                var tile = hexaData.HexaTileList[i];
                var isHide = tile.IsHide;
                var isFog = tile.IsFog;
                var canNotMove = tile.CanNotMove;

                if (save.TileMapStates != null && save.TileMapStates.TryGetValue(i, out var state))
                {
                    isHide = state.IsHide;
                    isFog = state.IsFog;
                    canNotMove = state.CanNotMove;
                }

                tiles[(tile.Location.x, tile.Location.y, tile.Location.z)] = new AiTile
                {
                    Location = tile.Location,
                    Blocked = canNotMove || isHide,
                    IsFog = isFog,
                };
            }
        }

        // a None-type strategy object blocks its tile, every other type is walkable
        foreach (var strategy in save.StrategyObjects.Values)
        {
            var excel = strategyObjectExcels.FirstOrDefault(x => x.Id == strategy.Id);
            if (excel == null || excel.StrategyObjectType != StrategyObjectType.None)
                continue;

            if (tiles.TryGetValue(LocationKey(strategy.Location), out var tile))
                tile.Blocked = true;
        }

        foreach (var echelon in save.EchelonInfos.Values)
        {
            if (tiles.TryGetValue(LocationKey(echelon.Location), out var tile))
                tile.Unit = echelon;
        }

        foreach (var enemy in save.EnemyInfos.Values)
        {
            if (tiles.TryGetValue(LocationKey(enemy.Location), out var tile))
                tile.Unit = enemy;
        }

        foreach (var kv in save.EnemyInfos.OrderBy(x => x.Key))
        {
            if (kv.Key <= lastAiUnitIndex)
                continue;

            var unitExcel = unitExcels.FirstOrDefault(x => x.Id == kv.Value.Id);
            if (unitExcel == null || unitExcel.AIMoveType == StrategyAIType.None)
                continue;

            if (!tiles.TryGetValue(LocationKey(kv.Value.Location), out var origin))
                continue;

            var route = TraceRouteToNearestEchelon(tiles, origin);
            if (route == null)
                continue;

            AiTile? dest = null;
            var engaged = false;

            if (route.Count <= unitExcel.MoveRange)
            {
                // walking onto the echelon is the attack
                dest = route[0];
                engaged = true;
            }
            else if (unitExcel.AIMoveType == StrategyAIType.Pursuit && unitExcel.MoveRange > 0)
            {
                // close up to MoveRange steps, settling on the last tile no fellow enemy holds
                var steps = 0;
                for (var i = route.Count - 1; i >= 0; i--)
                {
                    var tile = route[i];
                    if (tile.Unit == null || tile.Unit.IsPlayer)
                        dest = tile;
                    if (++steps >= unitExcel.MoveRange)
                        break;
                }
            }

            if (dest != null)
            {
                save.DisplayInfos.Add(new HexaDisplayInfo
                {
                    Type = HexaDisplayType.MoveUnit,
                    EntityId = kv.Key,
                    Location = dest.Location,
                });

                origin.Unit = null;
                dest.Unit = kv.Value;
            }

            if (engaged)
                break;
        }
    }

    // the client's HexaTileMap.GetAIUnitTraceRoute: BFS rings (units never block expansion), nearest echelon, ties by lowest MovementOrder. returned target-first so Count is the step distance and iterating from the end is the client's Pop order
    private static List<AiTile>? TraceRouteToNearestEchelon(Dictionary<(int x, int y, int z), AiTile> tiles, AiTile origin)
    {
        if (!tiles.Values.Any(t => t.Unit is { IsPlayer: true }))
            return null;

        var rings = new List<List<AiTile>> { new() { origin } };
        var seen = new HashSet<AiTile> { origin };

        for (var depth = 0; depth <= tiles.Count; depth++)
        {
            var next = new List<AiTile>();
            foreach (var tile in rings[depth])
            {
                foreach (var (dx, dy, dz) in HexDirections)
                {
                    if (!tiles.TryGetValue((tile.Location.x + dx, tile.Location.y + dy, tile.Location.z + dz), out var neighbor))
                        continue;
                    if (seen.Contains(neighbor) || neighbor.Blocked)
                        continue;

                    seen.Add(neighbor);
                    next.Add(neighbor);
                }
            }

            if (next.Count == 0)
                return null;

            rings.Add(next);

            var targets = next.Where(t => t.Unit is { IsPlayer: true }).ToList();
            if (targets.Count == 0)
                continue;

            var path = new List<AiTile> { targets.OrderBy(t => t.Unit!.MovementOrder).First() };
            var cur = path[0];

            for (var r = depth; r >= 1; r--)
            {
                foreach (var (dx, dy, dz) in HexDirections)
                {
                    if (!tiles.TryGetValue((cur.Location.x + dx, cur.Location.y + dy, cur.Location.z + dz), out var neighbor))
                        continue;
                    if (!rings[r].Contains(neighbor))
                        continue;

                    if (neighbor.IsFog || neighbor.Unit == null || !neighbor.Unit.IsPlayer)
                    {
                        path.Add(neighbor);
                        cur = neighbor;
                        break;
                    }
                }
            }

            return path;
        }

        return null;
    }

    // pending MoveUnit entries become positions, each consumed as it lands; upToEntityId bounds the EnterTactic interrupt
    internal static void ApplyEnemyMoves(CampaignMainStageSaveDBServer save, long? upToEntityId = null)
    {
        if (save.EnemyInfos == null || save.DisplayInfos == null)
            return;

        foreach (var kv in save.EnemyInfos.OrderBy(x => x.Key))
        {
            if (upToEntityId != null && kv.Key > upToEntityId)
                continue;

            var moveInfo = save.DisplayInfos.FirstOrDefault(x => x.Type == HexaDisplayType.MoveUnit && x.EntityId == kv.Key);
            if (moveInfo == null)
                continue;

            kv.Value.Location = new HexLocation2D { x = moveInfo.Location.x, y = moveInfo.Location.y, z = moveInfo.Location.z };
            save.DisplayInfos.Remove(moveInfo);
        }
    }

    // an enemy standing on an echelon tile, or a pending move that would land one there
    internal static bool HasEncounter(CampaignMainStageSaveDBServer save)
    {
        if (save.EchelonInfos == null || save.EnemyInfos == null)
            return false;

        var occupied = new HashSet<(int x, int y, int z)>(save.EchelonInfos.Values.Select(x => LocationKey(x.Location)));

        foreach (var kv in save.EnemyInfos)
        {
            if (occupied.Contains(LocationKey(kv.Value.Location)))
                return true;

            var moveInfo = save.DisplayInfos?.FirstOrDefault(x => x.Type == HexaDisplayType.MoveUnit && x.EntityId == kv.Key);
            if (moveInfo != null && occupied.Contains((moveInfo.Location.x, moveInfo.Location.y, moveInfo.Location.z)))
                return true;
        }

        return false;
    }

    // AddHexaUnitList and AddHexaStrategyList null out an all-zero location when building the save, so null means the origin tile
    private static (int x, int y, int z) LocationKey(HexLocation2D? location)
        => location == null ? (0, 0, 0) : (location.x, location.y, location.z);

    // hex cube neighbors in the client's HexLocation.Directions order
    private static readonly (int x, int y, int z)[] HexDirections =
    [
        (1, -1, 0), (1, 0, -1), (0, 1, -1), (-1, 1, 0), (-1, 0, 1), (0, -1, 1),
    ];

    private class AiTile
    {
        public HexLocation Location;
        public bool Blocked;
        public bool IsFog;
        public HexaUnit? Unit;
    }

    private bool CheckIfCleared(BattleSummary summary)
    {
        return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
    }

    // star conditions come from CampaignStageExcel and are scored once, on the tactic that empties the map (TacticResult); scoring them off a battle summary answers the same three questions for one tactic rather than the stage and pins ClearTurnRecord at 1

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

        // Both are best-ever records and only a run that actually cleared reports one: a defeat leaves them at 0 and must not overwrite what an earlier clear achieved. Fewer turns is better, more S-rank tactics is better.
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
            // EchelonExtensionType has no CampaignStageExcel column in this data version, so it stays at Base and DefaultValueHandling drops it off the wire, same as official
            StarConditionTurnCount = stageExcel.StarConditionTurnCount,
            StarConditionSTacticRackCount = stageExcel.StarConditionTacticRankSCount,
            IsDeprecated = stageExcel.Deprecated
        };
    }

    // Start events need no execution of their own: CreateConcentrateCampaign seeds the map with everything a HexaConditionStartCampaign event would spawn and BuildStartEventActivations records them as already fired. The rest of the hexa event machine hangs off HexaMapService.FindSatisfiedEndBattle, which TacticResult calls to detect the stage clear.
}
