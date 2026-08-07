using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MiniGameHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;
    private readonly MiniGameMissionService _missionService;

    public MiniGameHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler,
        MiniGameMissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
        _missionService = missionService;
    }

    [ProtocolHandler(Protocol.MiniGame_StageList)]
    public async Task<MiniGameStageListResponse> StageList(
        SchaleDataContext db,
        MiniGameStageListRequest request,
        MiniGameStageListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // an empty list is the right answer for a fresh account - the client builds the song list from MiniGameRhythmExcel itself and reads this only for the per-stage records and the score gate, so a synthesised row per stage would carry a ClearDate and read as already cleared
        response.MiniGameHistoryDBs = db.MiniGameHistories
            .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
            .ToList()
            .Select(x => new MiniGameHistoryDB
            {
                EventContentId = x.EventContentId,
                UniqueId = x.UniqueId,
                HighScore = x.HighScore,
                AccumulatedScore = x.AccumulatedScore,
                ClearDate = x.ClearDate,
                IsFullCombo = x.IsFullCombo
            })
            .ToList();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_MissionList)]
    public async Task<MiniGameMissionListResponse> MissionList(
        SchaleDataContext db,
        MiniGameMissionListRequest request,
        MiniGameMissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionIds = _excelService.GetTable<MiniGameMissionExcelT>()
            .Where(x => x.EventContentId == request.EventContentId)
            .Select(x => x.Id)
            .ToList();

        var progresses = await db.MissionProgresses
            .Where(x => x.AccountServerId == account.ServerId && missionIds.Contains(x.MissionUniqueId))
            .ToListAsync();

        response.ProgressDBs = progresses.ToMapList(_mapper);

        // the claim table, not the complete-progress rows - membership here is what greys the claim button out, so deriving it from Complete would make every finished mission unclaimable
        response.MissionHistoryUniqueIds = await db.MissionHistories
            .Where(x => x.AccountServerId == account.ServerId && missionIds.Contains(x.MissionUniqueId))
            .Select(x => x.MissionUniqueId)
            .ToListAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_EnterStage)]
    public async Task<MiniGameEnterStageResponse> EnterStage(
        SchaleDataContext db,
        MiniGameEnterStageRequest request,
        MiniGameEnterStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        ResolveRhythmStage(db, account, request.EventContentId, request.UniqueId);

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_Result)]
    public async Task<MiniGameResultResponse> Result(
        SchaleDataContext db,
        MiniGameResultRequest request,
        MiniGameResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var summary = request.Summary;
        if (summary == null)
            throw new WebAPIException(WebAPIErrorCode.MiniGameStageInvalidResult, "Rhythm result carried no summary");

        var stage = ResolveRhythmStage(db, account, request.EventContentId, request.UniqueId);

        // the chart itself is a client asset (RhythmFileName), so the run cannot be re-simulated - these are the bounds the excel row actually supports
        if (summary.FinalScore < 0 || summary.FinalScore > stage.MaxScore)
            throw new WebAPIException(WebAPIErrorCode.MiniGameStageInvalidResult, $"Score {summary.FinalScore} is outside 0..{stage.MaxScore} for stage {request.UniqueId}");

        var history = await db.MiniGameHistories.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId && x.UniqueId == request.UniqueId);
        if (history == null)
        {
            history = new MiniGameHistoryDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId,
                UniqueId = request.UniqueId
            };
            db.MiniGameHistories.Add(history);
        }

        history.HighScore = Math.Max(history.HighScore, summary.FinalScore);
        // a running total across every play, not a copy of the best run - this is what both OpenStageScoreAmount and Reset_GetTotalScoreRhythm read
        history.AccumulatedScore += summary.FinalScore;
        history.IsFullCombo |= summary.IsFullCombo;
        history.ClearDate = account.GameSettings.ServerDateTime();

        var updated = new List<MissionProgressDB>();
        void Advance(MissionCompleteConditionType type, long amount)
        {
            if (amount > 0)
                updated.AddRange(_missionService.UpdateMissionProgress(db, account, request.EventContentId, type, amount, request.UniqueId));
        }

        Advance(MissionCompleteConditionType.Reset_ClearStageRhythm, 1);
        Advance(MissionCompleteConditionType.Reset_GetTotalScoreRhythm, summary.FinalScore);
        Advance(MissionCompleteConditionType.Reset_GetBestScoreRhythm, summary.FinalScore);
        Advance(MissionCompleteConditionType.Reset_GetSpecificScoreRhythm, summary.FinalScore);
        Advance(MissionCompleteConditionType.Reset_GetComboCountRhythm, summary.MaxCombo);
        Advance(MissionCompleteConditionType.Reset_GetCriticalCountRhythm, summary.CriticalCount);
        if (summary.IsFullCombo)
            Advance(MissionCompleteConditionType.Reset_GetFullComboRhythm, 1);

        // MinigameRhythmSummary has no fever tally of its own, so a fever run is a false->true transition across the per-note flags. A client that sends no note detail leaves this condition with no data source for the run.
        var feverCount = 0;
        if (summary.MinigameJudgeRecords != null)
        {
            var wasOn = false;
            foreach (var record in summary.MinigameJudgeRecords)
            {
                if (record.IsFeverOn && !wasOn)
                    feverCount++;
                wasOn = record.IsFeverOn;
            }
        }

        Advance(MissionCompleteConditionType.Reset_GetFeverCountRhythm, feverCount);

        response.MissionProgressDBs = updated;

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_MissionReward)]
    public async Task<MiniGameMissionRewardResponse> MissionReward(
        SchaleDataContext db,
        MiniGameMissionRewardRequest request,
        MiniGameMissionRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionProgress = await db.MissionProgresses
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == request.MissionUniqueId);

        if (missionProgress == null)
        {
            throw new WebAPIException(WebAPIErrorCode.MissionRewardInvalid,
                $"Mission {request.MissionUniqueId} has no progress row for this account");
        }

        if (!missionProgress.Complete)
        {
            throw new WebAPIException(WebAPIErrorCode.MissionCannotComplete,
                $"Mission {request.MissionUniqueId} is not complete");
        }

        var missionExcel = _excelService.GetTable<MiniGameMissionExcelT>().FirstOrDefault(x => x.Id == request.MissionUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound,
                $"MiniGameMission excel {request.MissionUniqueId} not found");

        var parcelResultList = ParcelResult.ConvertParcelResult(
            missionExcel.MissionRewardParcelType,
            missionExcel.MissionRewardParcelId,
            missionExcel.MissionRewardAmount
        );

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcelResultList);
        response.ParcelResultDB = parcelResolver.ParcelResult;

        db.MissionProgresses.Remove(missionProgress);

        var completeTime = account.GameSettings.ServerDateTime();

        db.MissionHistories.Add(new MissionHistoryDBServer
        {
            AccountServerId = account.ServerId,
            MissionUniqueId = missionProgress.MissionUniqueId,
            CompleteTime = completeTime
        });

        response.AddedHistoryDB = new MissionHistoryDB
        {
            MissionUniqueId = missionProgress.MissionUniqueId,
            CompleteTime = completeTime
        };

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_MissionMultipleReward)]
    public async Task<MiniGameMissionMultipleRewardResponse> MissionMultipleReward(
        SchaleDataContext db,
        MiniGameMissionMultipleRewardRequest request,
        MiniGameMissionMultipleRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionExcelById = _excelService.GetTable<MiniGameMissionExcelT>()
            .Where(x => x.EventContentId == request.EventContentId)
            .ToDictionary(x => x.Id, x => x);

        var progresses = db.MissionProgresses
            .Where(x => x.AccountServerId == account.ServerId && x.Complete)
            .ToList();

        // MissionCategory.All is the client's "claim everything" tab value; no excel row ever carries it, so it must bypass the category filter rather than match against it.
        var targetProgresses = progresses
            .Where(p => missionExcelById.TryGetValue(p.MissionUniqueId, out var missionExcel)
                && (request.MissionCategory == MissionCategory.All
                    || missionExcel.Category == request.MissionCategory))
            .ToList();

        if (targetProgresses.Count == 0)
        {
            response.AddedHistoryDBs = [];
            response.ParcelResultDB = new ParcelResultDB
            {
                AccountDB = account.ToMap(_mapper),
                AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
            };
            return response;
        }

        var rewardParcels = new List<ParcelResult>();
        var addedHistory = new List<MissionHistoryDB>();

        foreach (var missionProgress in targetProgresses)
        {
            var missionExcel = missionExcelById[missionProgress.MissionUniqueId];

            rewardParcels.AddRange(ParcelResult.ConvertParcelResult(
                missionExcel.MissionRewardParcelType,
                missionExcel.MissionRewardParcelId,
                missionExcel.MissionRewardAmount));

            var completeTime = account.GameSettings.ServerDateTime();

            db.MissionHistories.Add(new MissionHistoryDBServer
            {
                AccountServerId = account.ServerId,
                MissionUniqueId = missionProgress.MissionUniqueId,
                CompleteTime = completeTime
            });

            addedHistory.Add(new MissionHistoryDB
            {
                MissionUniqueId = missionProgress.MissionUniqueId,
                CompleteTime = completeTime
            });
        }

        db.MissionProgresses.RemoveRange(targetProgresses);

        var batchResolver = await _parcelHandler.BuildParcel(db, account, rewardParcels);
        response.ParcelResultDB = batchResolver.ParcelResult;
        response.AddedHistoryDBs = addedHistory;

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_ShootingLobby)]
    public async Task<MiniGameShootingLobbyResponse> ShootingLobby(
        SchaleDataContext db,
        MiniGameShootingLobbyRequest request,
        MiniGameShootingLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var now = account.GameSettings.ServerDateTime();
        var todaysReset = now.Date.AddHours(4);
        var reset = now < todaysReset ? todaysReset.AddDays(-1) : todaysReset;

        // IsClearToday has no column behind it - a stored flag would stay true until the next play, so it is derived from the stamp every read
        response.HistoryDBs = db.MiniGameShootingHistories
            .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
            .ToList()
            .Select(x => new MiniGameShootingHistoryDB
            {
                EventContentId = x.EventContentId,
                UniqueId = x.UniqueId,
                ArriveSection = x.ArriveSection,
                LastUpdateDate = x.LastUpdateDate,
                IsClearToday = x.LastUpdateDate >= reset
            })
            .ToList();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_ShootingBattleEnter)]
    public async Task<MiniGameShootingBattleEnterResponse> ShootingBattleEnter(
        SchaleDataContext db,
        MiniGameShootingBattleEnterRequest request,
        MiniGameShootingBattleEnterResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stage = _excelService.GetTable<MiniGameShootingStageExcelT>().FirstOrDefault(x => x.UniqueId == request.UniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameShootingStageInvlid, $"MiniGameShootingStage {request.UniqueId} not found");

        // CostGoodsId is a GoodsExcel row, not a parcel. goods 130015 charges 0 of item 85212, which runs through the same path as a no-op.
        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == stage.CostGoodsId);
        if (goodsExcel != null)
        {
            var costParcels = ParcelResult.ConvertParcelResult(goodsExcel.ConsumeParcelType, goodsExcel.ConsumeParcelId, goodsExcel.ConsumeParcelAmount);
            await _parcelHandler.BuildParcel(db, account, costParcels, isConsume: true);
        }

        var history = await db.MiniGameShootingHistories.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId && x.UniqueId == request.UniqueId);
        if (history == null)
        {
            history = new MiniGameShootingHistoryDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId,
                UniqueId = request.UniqueId
            };
            db.MiniGameShootingHistories.Add(history);
        }

        history.LastUpdateDate = account.GameSettings.ServerDateTime();

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_ShootingBattleResult)]
    public async Task<MiniGameShootingBattleResultResponse> ShootingBattleResult(
        SchaleDataContext db,
        MiniGameShootingBattleResultRequest request,
        MiniGameShootingBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var summary = request.Summary;
        if (summary == null)
            throw new WebAPIException(WebAPIErrorCode.MiniGameStageInvalidResult, "Shooting result carried no summary");

        // the request itself has no stage fields - everything is read off the summary
        var stage = _excelService.GetTable<MiniGameShootingStageExcelT>().FirstOrDefault(x => x.UniqueId == summary.StageId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameShootingStageInvlid, $"MiniGameShootingStage {summary.StageId} not found");

        // the stage's own length caps what the client may claim to have reached, otherwise a made-up section falls through to the overflow tier below and sticks in the history as the sweep key
        var sectionCount = ShootingSectionCount(summary.StageId);
        var arriveSection = sectionCount > 0 ? Math.Min(summary.ArriveSection, sectionCount) : summary.ArriveSection;

        var rewardParcels = new List<ParcelResult>();
        if (arriveSection > 0)
        {
            var row = ResolveShootingReward(stage.EventContentStageRewardId, arriveSection);
            if (row != null)
                rewardParcels = ParcelResult.ConvertParcelResult(row.RewardParcelType, row.RewardParcelId, row.RewardParcelAmount);
        }

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, rewardParcels);
        response.ParcelResultDB = parcelResolver.ParcelResult;

        var history = await db.MiniGameShootingHistories.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == summary.EventContentId && x.UniqueId == summary.StageId);
        if (history == null)
        {
            history = new MiniGameShootingHistoryDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = summary.EventContentId,
                UniqueId = summary.StageId
            };
            db.MiniGameShootingHistories.Add(history);
        }

        history.ArriveSection = Math.Max(history.ArriveSection, arriveSection);
        history.LastUpdateDate = account.GameSettings.ServerDateTime();

        var updated = new List<MissionProgressDB>();
        if (summary.IsWin)
        {
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, summary.EventContentId, MissionCompleteConditionType.Reset_ClearCountShooting));
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, summary.EventContentId, MissionCompleteConditionType.Reset_ClearSpecificStageShooting, 1, summary.StageId));
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, summary.EventContentId, MissionCompleteConditionType.Reset_ClearSpecificCharacterShooting, 1, summary.PlayerCharacterId));
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, summary.EventContentId, MissionCompleteConditionType.Reset_ClearSpecificSectionShooting, 1, arriveSection, summary.StageId));
        }

        response.MissionProgressDBs = updated;

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_ShootingSweep)]
    public async Task<MiniGameShootingSweepResponse> ShootingSweep(
        SchaleDataContext db,
        MiniGameShootingSweepRequest request,
        MiniGameShootingSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stage = _excelService.GetTable<MiniGameShootingStageExcelT>().FirstOrDefault(x => x.UniqueId == request.UniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameShootingStageInvlid, $"MiniGameShootingStage {request.UniqueId} not found");

        if (request.SweepCount <= 0 || request.SweepCount > 1000)
            throw new WebAPIException(WebAPIErrorCode.MiniGameShootingCannotSweep, $"Sweep count {request.SweepCount} is out of range");

        var history = await db.MiniGameShootingHistories.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId && x.UniqueId == request.UniqueId);

        var sectionCount = ShootingSectionCount(request.UniqueId);

        if (history == null || history.ArriveSection < sectionCount)
            throw new WebAPIException(WebAPIErrorCode.MiniGameShootingCannotSweep, $"Stage {request.UniqueId} has not been cleared to section {sectionCount}");

        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == stage.CostGoodsId);
        if (goodsExcel != null)
        {
            var costParcels = ParcelResult.ConvertParcelResult(goodsExcel.ConsumeParcelType, goodsExcel.ConsumeParcelId, goodsExcel.ConsumeParcelAmount.Select(x => x * request.SweepCount).ToList());
            await _parcelHandler.BuildParcel(db, account, costParcels, isConsume: true);
        }

        // a swept run pays what the account actually managed, so the row is keyed by the stored best rather than the stage's full length
        var rewardRow = ResolveShootingReward(stage.EventContentStageRewardId, history.ArriveSection);

        var sweepRewards = new List<List<ParcelInfo>>();
        var allRewardParcels = new List<ParcelResult>();

        for (int i = 0; i < request.SweepCount; i++)
        {
            var rowParcels = rewardRow != null
                ? ParcelResult.ConvertParcelResult(rewardRow.RewardParcelType, rewardRow.RewardParcelId, rewardRow.RewardParcelAmount)
                : new List<ParcelResult>();

            sweepRewards.Add(ToParcelInfos(rowParcels));
            allRewardParcels.AddRange(rowParcels);
        }

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, allRewardParcels);

        history.LastUpdateDate = account.GameSettings.ServerDateTime();

        var updated = new List<MissionProgressDB>();
        for (int i = 0; i < request.SweepCount; i++)
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_ClearCountShooting));

        await db.SaveChangesAsync();

        response.Rewards = sweepRewards;
        response.ParcelResultDB = parcelResolver.ParcelResult;
        response.MissionProgressDBs = updated;

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DefenseGetInfo)]
    public async Task<MiniGameDefenseGetInfoResponse> DefenseGetInfo(
        SchaleDataContext db,
        MiniGameDefenseGetInfoRequest request,
        MiniGameDefenseGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = _excelService.GetTable<MiniGameDefenseInfoExcelT>().FirstOrDefault(x => x.EventContentId == request.EventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameDefenseInfoExcel {request.EventContentId} not found");

        // the point counter on the lobby is the entry-ticket stack, not the event's generic point - every stage's StageEnterCostId is this same item
        var pointItem = db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.UniqueId == info.DefenseBattleParcelId);
        response.EventPointAmount = pointItem != null ? pointItem.StackCount : 0;

        response.DefenseStageHistoryDBs = db.MiniGameDefenseStageHistories
            .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
            .ToList()
            .Select(ToDefenseHistoryDB)
            .ToList();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DefenseEnterBattle)]
    public async Task<MiniGameDefenseEnterBattleResponse> DefenseEnterBattle(
        SchaleDataContext db,
        MiniGameDefenseEnterBattleRequest request,
        MiniGameDefenseEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stage = _excelService.GetTable<MiniGameDefenseStageExcelT>().FirstOrDefault(x => x.Id == request.StageId && x.EventContentId == request.EventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameDefenseNotOpenStage, $"MiniGameDefenseStage {request.StageId} is not part of event {request.EventContentId}");

        // StarGoal[0] is Clear on all 22 rows, so Star1Flag is the cleared predicate - this model has no IsClearedEver
        if (stage.PrevStageId != 0)
        {
            var prev = await db.MiniGameDefenseStageHistories.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.StageId == stage.PrevStageId);
            if (prev == null || !prev.Star1Flag)
                throw new WebAPIException(WebAPIErrorCode.MiniGameDefenseNotOpenStage, $"Stage {stage.PrevStageId} has not been cleared");
        }

        // the fixed-echelon stages field a server-supplied team, so the player cannot bring a banned character to them
        if (stage.FixedEchelon == 0)
        {
            var bannedIds = _excelService.GetTable<MiniGameDefenseCharacterBanExcelT>()
                .Where(x => x.EventContentId == request.EventContentId)
                .Select(x => x.CharacterId)
                .ToList();

            var echelon = await db.GetAccountEchelons(account.ServerId)
                .FirstOrDefaultAsync(x => x.EchelonType == EchelonType.MinigameDefense && x.ExtensionType == stage.EchelonExtensionType);

            if (echelon != null && bannedIds.Count > 0)
            {
                var slotServerIds = echelon.MainSlotServerIds.Concat(echelon.SupportSlotServerIds).Where(x => x != 0).ToList();
                var bannedPick = db.GetAccountCharacters(account.ServerId)
                    .Where(x => slotServerIds.Contains(x.ServerId) && bannedIds.Contains(x.UniqueId))
                    .Select(x => x.UniqueId)
                    .FirstOrDefault();

                if (bannedPick != 0)
                    throw new WebAPIException(WebAPIErrorCode.MiniGameDefenseCannotUseCharacter, $"Character {bannedPick} is banned from event {request.EventContentId}");
            }
        }

        await _parcelHandler.BuildParcel(db, account, new ParcelResult(stage.StageEnterCostType, stage.StageEnterCostId, stage.StageEnterCostAmount), isConsume: true);

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DefenseBattleResult)]
    public async Task<MiniGameDefenseBattleResultResponse> DefenseBattleResult(
        SchaleDataContext db,
        MiniGameDefenseBattleResultRequest request,
        MiniGameDefenseBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stage = _excelService.GetTable<MiniGameDefenseStageExcelT>().FirstOrDefault(x => x.Id == request.StageId && x.EventContentId == request.EventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameDefenseNotOpenStage, $"MiniGameDefenseStage {request.StageId} is not part of event {request.EventContentId}");

        var info = _excelService.GetTable<MiniGameDefenseInfoExcelT>().FirstOrDefault(x => x.EventContentId == request.EventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameDefenseInfoExcel {request.EventContentId} not found");

        var history = await db.MiniGameDefenseStageHistories.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.StageId == request.StageId);

        // CanApplyMultiplier on the client is `multiplier >= 2 && Star1Flag && Star2Flag && Star3Flag`
        if (request.Multiplier < 1 || request.Multiplier > info.DefenseBattleMultiplierMax)
            throw new WebAPIException(WebAPIErrorCode.MiniGameDefenseCannotApplyMultiplier, $"Multiplier {request.Multiplier} is outside 1..{info.DefenseBattleMultiplierMax}");

        if (request.Multiplier >= 2 && (history == null || !history.Star1Flag || !history.Star2Flag || !history.Star3Flag))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDefenseCannotApplyMultiplier, $"Stage {request.StageId} needs three stars before a multiplier run");

        if (history == null)
        {
            history = new MiniGameDefenseStageHistoryDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId,
                StageId = request.StageId
            };
            db.MiniGameDefenseStageHistories.Add(history);
        }

        // the star slots are hardcoded on the client rather than read off StarGoal's order, and AllAlive is scored without consulting the win flag
        var goals = new Dictionary<StarGoalType, long>();
        for (int i = 0; i < stage.StarGoal.Count && i < stage.StarGoalAmount.Count; i++)
            goals[stage.StarGoal[i]] = stage.StarGoalAmount[i];

        if (!(history.Star1Flag && history.Star2Flag && history.Star3Flag))
        {
            if (goals.ContainsKey(StarGoalType.Clear) && !history.Star1Flag)
                history.Star1Flag = request.IsPlayerWin;

            if (goals.TryGetValue(StarGoalType.AllyBaseDamage, out var allowedDamage) && !history.Star2Flag)
                history.Star2Flag = request.IsPlayerWin && allowedDamage >= request.BaseDamage;

            if (goals.ContainsKey(StarGoalType.AllAlive) && !history.Star3Flag)
                history.Star3Flag = request.HeroCount == request.AliveCount;
        }

        var rewardRows = _excelService.GetTable<EventContentStageRewardExcelT>().Where(x => x.GroupId == stage.EventContentStageRewardId).ToList();
        var rewardParcels = new List<ParcelResult>();

        for (int run = 0; run < request.Multiplier; run++)
        {
            foreach (var row in rewardRows.Where(x => x.RewardTag == RewardTag.Default))
            {
                if (row.RewardProb == 0 || Random.Shared.Next(10000) < row.RewardProb)
                    rewardParcels.Add(new ParcelResult(row.RewardParcelType, row.RewardId, row.RewardAmount));
            }
        }

        if (!history.FirstClearRewardReceive && history.Star1Flag)
        {
            rewardParcels.AddRange(rewardRows.Where(x => x.RewardTag == RewardTag.FirstClear).Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount)));
            history.FirstClearRewardReceive = true;
        }

        // the Normal groups ship no ThreeStar rows at all, so the latch has to be driven by the flags rather than by having found rows
        if (!history.StarRewardReceive && history.Star1Flag && history.Star2Flag && history.Star3Flag)
        {
            rewardParcels.AddRange(rewardRows.Where(x => x.RewardTag == RewardTag.ThreeStar).Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount)));
            history.StarRewardReceive = true;
        }

        // MiniGame_DefenseEnterBattle carries no multiplier, so it only ever charges the single entry - the rest of a multiplied run is settled here
        if (request.Multiplier > 1)
        {
            await _parcelHandler.BuildParcel(db, account, new ParcelResult(stage.StageEnterCostType, stage.StageEnterCostId, stage.StageEnterCostAmount * (request.Multiplier - 1)), isConsume: true);
        }

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, rewardParcels);

        var updated = new List<MissionProgressDB>();
        if (request.IsPlayerWin)
        {
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_ClearCountDefense, request.Multiplier));
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_ClearSpecificDefenseStage, request.Multiplier, request.StageId));
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_ClearCharacterLimitDefense, request.HeroCount, request.StageId));
            updated.AddRange(_missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_ClearTimeLimitDefenseFromSecond, request.ClearSecond, request.StageId));
        }

        response.MissionProgressDBs = updated;

        await db.SaveChangesAsync();

        response.ParcelResultDB = parcelResolver.ParcelResult;
        response.StageHistoryDB = ToDefenseHistoryDB(history);

        return response;
    }

    private static MiniGameDefenseStageHistoryDB ToDefenseHistoryDB(MiniGameDefenseStageHistoryDBServer history)
        => new()
        {
            StageId = history.StageId,
            Star1Flag = history.Star1Flag,
            Star2Flag = history.Star2Flag,
            Star3Flag = history.Star3Flag,
            FirstClearRewardReceive = history.FirstClearRewardReceive,
            StarRewardReceive = history.StarRewardReceive
        };

    // every group carries exactly one ClearSection == 0 row whose amounts continue the per-section ramp one step past the last numbered section, so it is the overflow tier for a run that went further than the table names
    private MiniGameShootingStageRewardExcelT? ResolveShootingReward(long groupId, long arriveSection)
    {
        var rows = _excelService.GetTable<MiniGameShootingStageRewardExcelT>().Where(x => x.GroupId == groupId).ToList();
        return rows.FirstOrDefault(x => x.ClearSection == arriveSection) ?? rows.FirstOrDefault(x => x.ClearSection == 0);
    }

    private long ShootingSectionCount(long stageId)
    {
        var shootingConst = _excelService.GetTable<ConstMiniGameShootingExcelT>().FirstOrDefault();
        if (shootingConst == null)
            return 0;

        if (stageId == shootingConst.NormalStageId)
            return shootingConst.NormalSectionCount;
        if (stageId == shootingConst.HardStageId)
            return shootingConst.HardSectionCount;
        if (stageId == shootingConst.FreeStageId)
            return shootingConst.FreeSectionCount;

        return 0;
    }

    private static List<ParcelInfo> ToParcelInfos(List<ParcelResult> parcels)
        => parcels.Select(r => new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = r.Type, Id = r.Id },
            Amount = r.Amount,
            Multiplier = BasisPoint.One,
            Probability = BasisPoint.One
        }).ToList();

    // MiniGameRhythmExcel carries no event id of its own; the link runs stage -> RhythmBgmId -> MiniGameRhythmBgmExcel.EventContentId, so a stage id belonging to another event has to be caught there.
    private MiniGameRhythmExcelT ResolveRhythmStage(SchaleDataContext db, AccountDBServer account, long eventContentId, long uniqueId)
    {
        var stage = _excelService.GetTable<MiniGameRhythmExcelT>().FirstOrDefault(x => x.UniqueId == uniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameRhythmExcel {uniqueId} not found");

        var bgm = _excelService.GetTable<MiniGameRhythmBgmExcelT>().FirstOrDefault(x => x.RhythmBgmId == stage.RhythmBgmId);
        if (bgm == null || bgm.EventContentId != eventContentId)
            throw new WebAPIException(WebAPIErrorCode.MiniGameStageIsNotOpen, $"Rhythm stage {uniqueId} does not belong to event {eventContentId}");

        // the tiers are 0 / 2500000 / 7500000 / 15000000 and no single stage's MaxScore reaches the top one, so the sum has to span the whole event
        if (stage.OpenStageScoreAmount > 0)
        {
            var accumulated = db.MiniGameHistories
                .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == eventContentId)
                .Sum(x => x.AccumulatedScore);

            if (accumulated < stage.OpenStageScoreAmount)
                throw new WebAPIException(WebAPIErrorCode.MiniGameStageIsNotOpen, $"Rhythm stage {uniqueId} needs {stage.OpenStageScoreAmount} accumulated score, account has {accumulated}");
        }

        return stage;
    }
}
