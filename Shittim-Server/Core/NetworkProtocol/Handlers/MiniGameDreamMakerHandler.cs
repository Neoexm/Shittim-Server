using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MiniGameDreamMakerHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;
    private readonly MiniGameMissionService _missionService;
    private readonly EventContentCollectionService _collectionService;

    public MiniGameDreamMakerHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler,
        MiniGameMissionService missionService,
        EventContentCollectionService collectionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
        _missionService = missionService;
        _collectionService = collectionService;
    }

    [ProtocolHandler(Protocol.MiniGame_DreamMakerGetInfo)]
    public async Task<MiniGameDreamMakerGetInfoResponse> GetInfo(
        SchaleDataContext db,
        MiniGameDreamMakerGetInfoRequest request,
        MiniGameDreamMakerGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var save = await db.MiniGameDreamMakers.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);

        response.InfoDB = BuildInfo(save, info, request.EventContentId);
        response.ParameterDBs = BuildParameters(save, request.EventContentId);

        response.EndingDBs = db.GetAccountMiniGameDreamMakerEndings(account.ServerId)
            .Where(x => x.EventContentId == request.EventContentId)
            .AsEnumerable()
            .Select(x => new MiniGameDreamMakerEndingDB
            {
                EventContentId = x.EventContentId,
                EndingId = x.EndingId,
                ClearCount = x.ClearCount,
                ClearDate = x.ClearDate
            })
            .ToList();

        response.EventContentCollectionDBs = db.GetAccountEventContentCollections(account.ServerId)
            .Where(x => x.EventContentId == request.EventContentId)
            .AsEnumerable()
            .Select(x => new EventContentCollectionDB
            {
                EventContentId = x.EventContentId,
                GroupId = x.GroupId,
                UniqueId = x.UniqueId,
                ReceiveDate = x.ReceiveDate
            })
            .ToList();

        response.EventPointAmount = PointAmount(db, account, request.EventContentId);
        response.AlreadyReceivePointRewardIds = ClaimedPointRewards(account, request.EventContentId);

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DreamMakerNewGame)]
    public async Task<MiniGameDreamMakerNewGameResponse> NewGame(
        SchaleDataContext db,
        MiniGameDreamMakerNewGameRequest request,
        MiniGameDreamMakerNewGameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var save = await db.MiniGameDreamMakers.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);

        if (save != null && !RoundComplete(save, info))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamCannotStartNewGame, $"Round {save.Round} of dream {request.EventContentId} is still on day {save.DayOfNumber}");

        if (save != null && !save.EndingRewardReceived)
            throw new WebAPIException(WebAPIErrorCode.MiniGameShouldReceiveEndingReward, $"Ending {save.CurrentRoundEndingId} of dream {request.EventContentId} has not been collected");

        var multiplier = request.Multiplier <= 0 ? 1 : request.Multiplier;
        if (multiplier > info.DreamMakerMultiplierMax || (multiplier > 1 && !MultiplierUnlocked(db, account, info, save)))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamCannotApplyMultiplier, $"Multiplier {multiplier} is not available on dream {request.EventContentId}");

        var parameters = _excelService.GetTable<MiniGameDreamParameterExcelT>().Where(x => x.EventContentId == request.EventContentId).ToList();

        if (save == null)
        {
            save = new MiniGameDreamMakerDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId
            };
            db.MiniGameDreamMakers.Add(save);
        }

        foreach (var parameter in parameters)
        {
            // the run you just finished feeds the next one: whatever the stat ended on is worth DreamMakerParameterTransfer of itself on top of the flat base, up to ParameterBaseMax
            var carried = save.ParameterAmounts.GetValueOrDefault(parameter.ParameterType);
            var newBase = Math.Min(parameter.ParameterBaseMax, parameter.ParameterBase + carried * info.DreamMakerParameterTransfer / 10000);

            save.ParameterBases[parameter.ParameterType] = newBase;
            save.ParameterAmounts[parameter.ParameterType] = newBase;
        }

        save.Round += 1;
        save.Multiplier = multiplier;
        save.DayOfNumber = 1;
        save.ActionCount = 0;
        save.CurrentRoundEndingId = 0;
        save.EndingRewardReceived = false;

        await db.SaveChangesAsync();

        response.InfoDB = BuildInfo(save, info, request.EventContentId);
        response.ParameterDBs = BuildParameters(save, request.EventContentId);

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DreamMakerRestart)]
    public async Task<MiniGameDreamMakerResetResponse> Reset(
        SchaleDataContext db,
        MiniGameDreamMakerResetRequest request,
        MiniGameDreamMakerResetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var save = await LoadSave(db, account.ServerId, request.EventContentId);

        if (RoundComplete(save, info))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamCannotReset, $"Round {save.Round} of dream {request.EventContentId} is already over");

        // a restart rewinds the days but not the carry-over, so the bases stay exactly where NewGame put them and the round number does not advance
        foreach (var type in save.ParameterBases.Keys.ToList())
            save.ParameterAmounts[type] = save.ParameterBases[type];

        save.DayOfNumber = 1;
        save.ActionCount = 0;
        save.CurrentRoundEndingId = 0;

        await db.SaveChangesAsync();

        response.InfoDB = BuildInfo(save, info, request.EventContentId);
        response.ParameterDBs = BuildParameters(save, request.EventContentId);

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DreamMakerAttendSchedule)]
    public async Task<MiniGameDreamMakerAttendScheduleResponse> AttendSchedule(
        SchaleDataContext db,
        MiniGameDreamMakerAttendScheduleRequest request,
        MiniGameDreamMakerAttendScheduleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var save = await LoadSave(db, account.ServerId, request.EventContentId);

        if (RoundComplete(save, info))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamRoundCompleted, $"Round {save.Round} of dream {request.EventContentId} is over");

        if (save.ActionCount >= info.DreamMakerActionPoint)
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamNotEnoughActionCount, $"Day {save.DayOfNumber} of dream {request.EventContentId} has no actions left");

        var schedule = _excelService.GetTable<MiniGameDreamScheduleExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId && x.DreamMakerScheduleGroupId == request.ScheduleGroupId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameDreamScheduleExcel group {request.ScheduleGroupId} not found on event {request.EventContentId}");

        var goods = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == info.ScheduleCostGoodsId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"GoodsExcel {info.ScheduleCostGoodsId} not found");

        var costParcels = ParcelResult.ConvertParcelResult(goods.ConsumeParcelType, goods.ConsumeParcelId, goods.ConsumeParcelAmount);
        foreach (var cost in costParcels)
            cost.Amount *= save.Multiplier;

        await _parcelHandler.BuildParcel(db, account, costParcels, isConsume: true);

        var result = RollResult(request.EventContentId, schedule.DreamMakerScheduleGroupId);
        var parameters = _excelService.GetTable<MiniGameDreamParameterExcelT>().Where(x => x.EventContentId == request.EventContentId).ToList();

        var deltaCount = new[]
        {
            result.RewardParameter?.Count ?? 0,
            result.RewardParameterOperationType?.Count ?? 0,
            result.RewardParameterAmount?.Count ?? 0
        }.Min();

        for (int i = 0; i < deltaCount; i++)
        {
            var type = result.RewardParameter![i];
            var bounds = parameters.FirstOrDefault(x => x.ParameterType == type);
            if (bounds == null)
                continue;

            // GrowUpHigh/GrowDownHigh only pick a bigger arrow on the result popup - the sign is the only thing that separates the four operations
            var signed = result.RewardParameterOperationType![i] is DreamMakerParamOperationType.GrowUpHigh or DreamMakerParamOperationType.GrowUp
                ? result.RewardParameterAmount![i]
                : -result.RewardParameterAmount![i];

            var current = save.ParameterAmounts.GetValueOrDefault(type);
            save.ParameterAmounts[type] = Math.Clamp(current + signed, bounds.ParameterMin, bounds.ParameterMax);
        }

        save.ActionCount += 1;

        var rewardParcels = new List<ParcelResult>();
        if (result.RewardParcelAmount > 0)
            rewardParcels.Add(new ParcelResult(result.RewardParcelType, result.RewardParcelId, result.RewardParcelAmount * save.Multiplier));

        var resolver = await _parcelHandler.BuildParcel(db, account, rewardParcels);

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DreamGetScheduleCount);
        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DreamGetSpecificScheduleCount, 1, schedule.DreamMakerScheduleGroupId);

        foreach (var parameter in parameters)
            _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DreamGetSpecificParameter, save.ParameterAmounts.GetValueOrDefault(parameter.ParameterType), parameter.Id);

        foreach (var scenario in NewlyReachedScenarios(save, request.EventContentId))
        {
            save.CollectionScenarios.Add(scenario);
            _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DreamGetCollectionScenarioCount, 1, scenario);
        }

        var collections = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.MinigameDreamMakerParameter);

        await db.SaveChangesAsync();

        response.InfoDB = BuildInfo(save, info, request.EventContentId);
        response.ParameterDBs = BuildParameters(save, request.EventContentId);
        response.ParcelResultDB = resolver.ParcelResult;
        response.ScheduleResultId = result.Id;
        response.EventContentCollectionDBs = collections;

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DreamMakerDailyClosing)]
    public async Task<MiniGameDreamMakerDailyClosingResponse> DailyClosing(
        SchaleDataContext db,
        MiniGameDreamMakerDailyClosingRequest request,
        MiniGameDreamMakerDailyClosingResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var save = await LoadSave(db, account.ServerId, request.EventContentId);

        if (RoundComplete(save, info))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamRoundCompleted, $"Round {save.Round} of dream {request.EventContentId} is over");

        if (save.ActionCount < info.DreamMakerActionPoint)
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamActionCountRemain, $"Day {save.DayOfNumber} of dream {request.EventContentId} still has {info.DreamMakerActionPoint - save.ActionCount} actions");

        var total = save.ParameterAmounts.Values.Sum();
        var band = _excelService.GetTable<MiniGameDreamDailyPointExcelT>()
            .Where(x => x.EventContentId == request.EventContentId && x.TotalParameterMin <= total)
            .OrderByDescending(x => x.TotalParameterMin)
            .FirstOrDefault()
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameDreamDailyPointExcel has no band for {total} on event {request.EventContentId}");

        var point = (total * band.DailyPointCoefficient / 10000 + band.DailyPointCorrectionValue) * save.Multiplier;
        var resolver = await _parcelHandler.BuildParcel(db, account, [new ParcelResult(info.DreamMakerDailyPointParcelType, info.DreamMakerDailyPointId, point)]);

        save.DayOfNumber += 1;
        save.ActionCount = 0;

        if (save.DayOfNumber > info.DreamMakerDays)
            save.CurrentRoundEndingId = ResolveEnding(db, account, save, request.EventContentId);

        await db.SaveChangesAsync();

        response.InfoDB = BuildInfo(save, info, request.EventContentId);
        response.ParameterDBs = BuildParameters(save, request.EventContentId);
        response.ParcelResultDB = resolver.ParcelResult;
        response.EventPointAmount = PointAmount(db, account, request.EventContentId);
        response.AlreadyReceivePointRewardIds = ClaimedPointRewards(account, request.EventContentId);

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_DreamMakerEnding)]
    public async Task<MiniGameDreamMakerEndingResponse> Ending(
        SchaleDataContext db,
        MiniGameDreamMakerEndingRequest request,
        MiniGameDreamMakerEndingResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var save = await LoadSave(db, account.ServerId, request.EventContentId);

        if (!RoundComplete(save, info))
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamRoundNotComplete, $"Round {save.Round} of dream {request.EventContentId} is only on day {save.DayOfNumber}");

        if (save.EndingRewardReceived)
            throw new WebAPIException(WebAPIErrorCode.MiniGameDreamRewardAlreadyReceived, $"Ending {save.CurrentRoundEndingId} of dream {request.EventContentId} was already collected");

        if (save.CurrentRoundEndingId == 0)
            save.CurrentRoundEndingId = ResolveEnding(db, account, save, request.EventContentId);

        var record = await db.MiniGameDreamMakerEndings.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId && x.EndingId == save.CurrentRoundEndingId);
        var repeat = record != null;

        if (record == null)
        {
            record = new MiniGameDreamMakerEndingDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId,
                EndingId = save.CurrentRoundEndingId
            };
            db.MiniGameDreamMakerEndings.Add(record);
        }

        record.ClearCount += 1;
        record.ClearDate = account.GameSettings.ServerDateTime();

        var rewardType = repeat ? DreamMakerEndingRewardType.LoopEndingReward : DreamMakerEndingRewardType.FirstEndingReward;
        var rewardRow = _excelService.GetTable<MiniGameDreamEndingRewardExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId && x.EndingId == save.CurrentRoundEndingId && x.DreamMakerEndingRewardType == rewardType);

        var parcels = new List<ParcelResult>();
        if (rewardRow != null)
        {
            var count = new[]
            {
                rewardRow.RewardParcelType?.Count ?? 0,
                rewardRow.RewardParcelId?.Count ?? 0,
                rewardRow.RewardParcelAmount?.Count ?? 0
            }.Min();

            for (int i = 0; i < count; i++)
            {
                // the one-off is paid at face value, the repeat payout is what the multiplier is for
                var amount = repeat ? rewardRow.RewardParcelAmount![i] * save.Multiplier : rewardRow.RewardParcelAmount![i];
                parcels.Add(new ParcelResult(rewardRow.RewardParcelType![i], rewardRow.RewardParcelId![i], amount));
            }
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);

        save.EndingRewardReceived = true;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DreamGetEndingCount);
        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DreamGetSpecificEndingCount, 1, save.CurrentRoundEndingId);

        await db.SaveChangesAsync();

        response.InfoDB = BuildInfo(save, info, request.EventContentId);
        response.ParameterDBs = BuildParameters(save, request.EventContentId);
        response.EndingDB = new MiniGameDreamMakerEndingDB
        {
            EventContentId = record.EventContentId,
            EndingId = record.EndingId,
            ClearCount = record.ClearCount,
            ClearDate = record.ClearDate
        };
        response.ParcelResultDB = resolver.ParcelResult;

        return response;
    }

    private MiniGameDreamInfoExcelT ResolveInfo(long eventContentId)
        => _excelService.GetTable<MiniGameDreamInfoExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentNotOpen, $"MiniGameDreamInfoExcel has no row for event {eventContentId}");

    private static async Task<MiniGameDreamMakerDBServer> LoadSave(SchaleDataContext db, long accountServerId, long eventContentId)
        => await db.MiniGameDreamMakers.FirstOrDefaultAsync(x => x.AccountServerId == accountServerId && x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameDreamSaveNotExist, $"No dream save for event {eventContentId}");

    private static bool RoundComplete(MiniGameDreamMakerDBServer save, MiniGameDreamInfoExcelT info)
        => save.Round > 0 && (save.DayOfNumber > info.DreamMakerDays || save.CurrentRoundEndingId != 0);

    private MiniGameDreamMakerInfoDB BuildInfo(MiniGameDreamMakerDBServer? save, MiniGameDreamInfoExcelT info, long eventContentId)
    {
        if (save == null)
        {
            return new MiniGameDreamMakerInfoDB
            {
                EventContentId = eventContentId,
                Multiplier = 1,
                CanStartNewGame = true
            };
        }

        var complete = RoundComplete(save, info);

        return new MiniGameDreamMakerInfoDB
        {
            EventContentId = eventContentId,
            Round = save.Round,
            Multiplier = save.Multiplier,
            DayOfNumber = save.DayOfNumber,
            ActionCount = save.ActionCount,
            CurrentRoundEndingId = save.CurrentRoundEndingId,
            EndingRewardReceived = save.EndingRewardReceived,
            CanStartNewGame = complete && save.EndingRewardReceived,
            CanReceiveEndingReward = complete && !save.EndingRewardReceived
        };
    }

    private List<MiniGameDreamMakerParameterDB> BuildParameters(MiniGameDreamMakerDBServer? save, long eventContentId)
    {
        return _excelService.GetTable<MiniGameDreamParameterExcelT>()
            .Where(x => x.EventContentId == eventContentId)
            .Select(x => new MiniGameDreamMakerParameterDB
            {
                ParameterType = x.ParameterType,
                BaseAmount = save?.ParameterBases.GetValueOrDefault(x.ParameterType) ?? x.ParameterBase,
                CurrentAmount = save?.ParameterAmounts.GetValueOrDefault(x.ParameterType) ?? x.ParameterBase
            })
            .ToList();
    }

    private bool MultiplierUnlocked(SchaleDataContext db, AccountDBServer account, MiniGameDreamInfoExcelT info, MiniGameDreamMakerDBServer? save)
    {
        // the condition is a one-off gate on the multiplier button, not a per-step ramp: clear it and every value up to DreamMakerMultiplierMax is selectable
        return info.DreamMakerMultiplierCondition switch
        {
            DreamMakerMultiplierCondition.Round => (save?.Round ?? 0) >= info.DreamMakerMultiplierConditionValue,
            DreamMakerMultiplierCondition.CollectionCount => db.GetAccountEventContentCollections(account.ServerId).Count(x => x.EventContentId == info.EventContentId) >= info.DreamMakerMultiplierConditionValue,
            DreamMakerMultiplierCondition.EndingCount => db.GetAccountMiniGameDreamMakerEndings(account.ServerId).Count(x => x.EventContentId == info.EventContentId) >= info.DreamMakerMultiplierConditionValue,
            _ => true
        };
    }

    private MiniGameDreamScheduleResultExcelT RollResult(long eventContentId, long scheduleGroupId)
    {
        var rows = _excelService.GetTable<MiniGameDreamScheduleResultExcelT>()
            .Where(x => x.EventContentId == eventContentId && x.DreamMakerScheduleGroup == scheduleGroupId)
            .ToList();

        if (rows.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameDreamScheduleResultExcel has no rows for group {scheduleGroupId} on event {eventContentId}");

        var roll = Random.Shared.NextInt64(rows.Sum(x => x.Prob));
        foreach (var row in rows)
        {
            roll -= row.Prob;
            if (roll < 0)
                return row;
        }

        return rows[^1];
    }

    private long ResolveEnding(SchaleDataContext db, AccountDBServer account, MiniGameDreamMakerDBServer save, long eventContentId)
    {
        var parameters = _excelService.GetTable<MiniGameDreamParameterExcelT>().Where(x => x.EventContentId == eventContentId).ToList();
        var collectionCount = db.GetAccountEventContentCollections(account.ServerId).Count(x => x.EventContentId == eventContentId);

        long Measure(DreamMakerEndingCondition condition) => condition switch
        {
            DreamMakerEndingCondition.Param01 => save.ParameterAmounts.GetValueOrDefault(DreamMakerParameterType.Param01),
            DreamMakerEndingCondition.Param02 => save.ParameterAmounts.GetValueOrDefault(DreamMakerParameterType.Param02),
            DreamMakerEndingCondition.Param03 => save.ParameterAmounts.GetValueOrDefault(DreamMakerParameterType.Param03),
            DreamMakerEndingCondition.Param04 => save.ParameterAmounts.GetValueOrDefault(DreamMakerParameterType.Param04),
            DreamMakerEndingCondition.Round => save.Round,
            DreamMakerEndingCondition.CollectionCount => collectionCount,
            _ => 0
        };

        var endings = _excelService.GetTable<MiniGameDreamEndingExcelT>()
            .Where(x => x.EventContentId == eventContentId)
            .OrderBy(x => x.Order)
            .ToList();

        foreach (var ending in endings)
        {
            var conditions = ending.EndingCondition ?? [];
            var values = ending.EndingConditionValue ?? [];
            var count = Math.Min(conditions.Count, values.Count);

            var met = true;
            for (int i = 0; i < count && met; i++)
                met = Measure(conditions[i]) >= values[i];

            if (met)
                return ending.EndingId;
        }

        // Order 9999 with no conditions is the catch-all every event ships, so this only trips on a table that never got one
        throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MiniGameDreamEndingExcel has no reachable ending on event {eventContentId}");
    }

    private List<long> NewlyReachedScenarios(MiniGameDreamMakerDBServer save, long eventContentId)
    {
        var reached = new List<long>();

        foreach (var scenario in _excelService.GetTable<MiniGameDreamCollectionScenarioExcelT>().Where(x => x.EventContentId == eventContentId))
        {
            if (save.CollectionScenarios.Contains(scenario.Id))
                continue;

            var conditions = scenario.Parameter ?? [];
            var amounts = scenario.ParameterAmount ?? [];
            var count = Math.Min(conditions.Count, amounts.Count);
            if (count == 0)
                continue;

            var met = true;
            for (int i = 0; i < count && met; i++)
                met = save.ParameterAmounts.GetValueOrDefault(conditions[i]) >= amounts[i];

            if (met)
                reached.Add(scenario.Id);
        }

        return reached;
    }

    private long PointAmount(SchaleDataContext db, AccountDBServer account, long eventContentId)
    {
        var pointItemId = _excelService.GetTable<EventContentCurrencyItemExcelT>()
            .FirstOrDefault(x => x.EventContentId == eventContentId && x.EventContentItemType == EventContentItemType.EventPoint)
            ?.ItemUniqueId ?? 0;

        if (pointItemId == 0)
            return 0;

        return db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.UniqueId == pointItemId)?.StackCount ?? 0;
    }

    private static List<long> ClaimedPointRewards(AccountDBServer account, long eventContentId)
    {
        var received = account.ContentInfo.ReceivedEventStageTotalRewards;
        return received != null && received.TryGetValue(eventContentId, out var ids) ? [.. ids] : [];
    }
}
