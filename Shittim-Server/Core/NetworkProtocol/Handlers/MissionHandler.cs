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

public class MissionHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly MissionService _missionService;

    public MissionHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        MissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
        _missionService = missionService;
    }

    [ProtocolHandler(Protocol.Mission_Sync)]
    public async Task<MissionSyncResponse> Sync(
        SchaleDataContext db,
        MissionSyncRequest request,
        MissionSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionProgresses = db.GetAccountMissionProgresses(account.ServerId).ToList();

        response.MissionProgressDBs = _mapper.Map<List<MissionProgressDB>>(
            FilterToMissionScreenIds(missionProgresses));

        return response;
    }

    [ProtocolHandler(Protocol.Mission_List)]
    public async Task<MissionListResponse> List(
        SchaleDataContext db,
        MissionListRequest request,
        MissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Official's MissionHistoryUniqueIds carries claimed-mission ids only - never campaign stage data. In the 2026-07-28 capture pair the account-wide list is every claim once, plus a second copy of exactly the claims whose id is NOT in MissionExcel (its 78 guide-mission claims, ids 1000200-1000375 in GuideMissionExcel's range), and the event-scoped list is exactly that non-MissionExcel subset.
        // An id the client cannot resolve against an excel kills the mission screen outright - it renders from this login-cached response without any further request, so nothing loads and no error reaches the wire.
        // CampaignStageHistory.StoryUniqueId is 0 on every row written here, so campaign history must not be folded in.
        var claims = db.GetAccountMissionHistories(account.ServerId)
            .Select(x => x.MissionUniqueId)
            .Distinct()
            .ToList();

        var missionExcelIds = _excelService.GetTable<MissionExcelT>().Select(x => x.Id).ToHashSet();
        var guideOrEventIds = _excelService.GetTable<GuideMissionExcelT>().Select(x => x.Id)
            .Concat(_excelService.GetTable<EventContentMissionExcelT>().Select(x => x.Id))
            .ToHashSet();

        response.MissionHistoryUniqueIds = BuildMissionHistoryIds(
            claims, missionExcelIds, guideOrEventIds, eventScoped: request.EventContentId != null);

        if (request.EventContentId == null)
        {
            // Official's account-wide ProgressDBs holds mission + guide-mission rows only. Battle-pass rows (shared MissionProgresses storage, served by BattlePass_MissionList) and event rows never appear here.
            var missionProgresses = db.GetAccountMissionProgresses(account.ServerId).ToList();
            response.ProgressDBs = _mapper.Map<List<MissionProgressDB>>(
                FilterToMissionScreenIds(missionProgresses));
        }
        else
        {
            // Official omits ProgressDBs entirely on event-scoped calls (nulls are dropped by the serializer); event-mission progress reaches the client through other protocols.
            response.ProgressDBs = null;
        }

        // Mission_List always carries today's sudden-mission definition, never {}.
        response.DailySuddenMissionInfo = BuildDailySuddenMissionInfo(account);
        response.ClearedOrignalMissionIds = BuildClearedOriginalMissionIds(db, account, request.EventContentId);

        return response;
    }

    // Official's account-wide list is every resolvable claim once plus a second copy of the non-MissionExcel claims, and the event-scoped list is only that non-MissionExcel subset (verified against the 2026-07-28 capture: 953 = 875 distinct claims + the 78 guide claims repeated; the event call returns exactly those 78).
    // Ids no excel can resolve are dropped outright.
    internal static List<long> BuildMissionHistoryIds(
        IEnumerable<long> claims,
        HashSet<long> missionExcelIds,
        HashSet<long> guideOrEventIds,
        bool eventScoped)
    {
        if (missionExcelIds.Count == 0 && guideOrEventIds.Count == 0)
            return eventScoped ? [] : claims.ToList();

        var resolvable = claims
            .Where(id => missionExcelIds.Contains(id) || guideOrEventIds.Contains(id))
            .ToList();
        var outsideMissionExcel = resolvable.Where(id => !missionExcelIds.Contains(id)).ToList();

        return eventScoped
            ? outsideMissionExcel
            : outsideMissionExcel.Concat(resolvable).ToList();
    }

    // The ids the client's mission screen can resolve: MissionExcel plus GuideMissionExcel. MissionProgresses also stores battle-pass rows (BattlePassMissionExcel ids 2000001+) and event-content rows (856xxx...), and an unresolvable id in a mission-screen payload breaks the screen.
    // When both tables degrade to empty (dump/schema failure) the filter passes everything through rather than blanking the screen.
    internal static List<MissionProgressDBServer> FilterMissionScreenProgresses(
        List<MissionProgressDBServer> progresses, ExcelTableService excelService)
    {
        var validIds = excelService.GetTable<MissionExcelT>().Select(x => x.Id)
            .Concat(excelService.GetTable<GuideMissionExcelT>().Select(x => x.Id))
            .ToHashSet();

        return FilterMissionScreenProgresses(progresses, validIds);
    }

    internal static List<MissionProgressDBServer> FilterMissionScreenProgresses(
        List<MissionProgressDBServer> progresses, HashSet<long> validIds)
    {
        return validIds.Count == 0
            ? progresses
            : progresses.Where(p => validIds.Contains(p.MissionUniqueId)).ToList();
    }

    private List<MissionProgressDBServer> FilterToMissionScreenIds(List<MissionProgressDBServer> progresses)
        => FilterMissionScreenProgresses(progresses, _excelService);

    // Official sends ClearedOrignalMissionIds only when the queried event is a rerun (a "return" event), so the client can mark the missions the account already cleared during the original run; for every other scope the key is omitted rather than sent as []. Both captures agree: EventContentId null -> absent, 856 (a normal event) -> absent, 10842 -> present and empty.
    // 10842 is a rerun of 842 and that account, despite 950 cleared missions, had never played 842, so empty is the correct content and not an omission.
    private List<long>? BuildClearedOriginalMissionIds(
        SchaleDataContext db,
        AccountDBServer account,
        long? eventContentId)
    {
        if (eventContentId is not long queriedEventId)
            return null;

        var season = _excelService.GetTable<EventContentSeasonExcelT>()
            .FirstOrDefault(x => x.EventContentId == queriedEventId);

        if (season is not { IsReturn: true })
            return null;

        // Past this point the key is present even when it resolves to nothing.
        if (season.OriginalEventContentId <= 0)
            return [];

        var originalIdPrefix = season.OriginalEventContentId.ToString();

        return db.GetAccountMissionProgresses(account.ServerId)
            .Where(x => x.Complete)
            .Select(x => x.MissionUniqueId)
            .ToList()
            .Where(x => x.ToString().StartsWith(originalIdPrefix, StringComparison.Ordinal))
            .ToList();
    }

    // Official Mission_List always includes a fully-populated DailySuddenMissionInfo (the MissionExcel row of the day's DailySudden mission projected to the client MissionInfo shape).
    // Pick deterministically per day among the currently-active DailySudden rows.
    private object BuildDailySuddenMissionInfo(AccountDBServer account)
    {
        var now = account.GameSettings.ServerDateTime();

        var candidates = _excelService.GetTable<MissionExcelT>()
            .Where(x => x.Category == MissionCategory.DailySudden)
            .Where(x => ParseExcelDate(x.StartDate, DateTime.MinValue) <= now
                     && now < ParseExcelDate(x.StartableEndDate, DateTime.MaxValue)
                     && now < ParseExcelDate(x.EndDate, DateTime.MaxValue))
            .OrderBy(x => x.Id)
            .ToList();

        if (candidates.Count == 0)
            return new { };

        var pick = candidates[(int)((now.Date.Ticks / TimeSpan.TicksPerDay) % candidates.Count)];

        var rewards = new List<ParcelInfo>();
        for (int i = 0; i < pick.MissionRewardParcelType.Count; i++)
        {
            rewards.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = pick.MissionRewardParcelType[i], Id = pick.MissionRewardParcelId[i] },
                Amount = pick.MissionRewardAmount[i],
                Multiplier = BasisPoint.One,
                Probability = BasisPoint.One
            });
        }

        return new DailySuddenMissionInfo
        {
            Id = pick.Id,
            Category = pick.Category,
            ResetType = pick.ResetType,
            Description = pick.Description,
            IsVisible = pick.ViewFlag,
            StartDate = ParseExcelDate(pick.StartDate, DateTime.MinValue),
            StartableEndDate = ParseExcelDate(pick.StartableEndDate, DateTime.MaxValue),
            EndDate = ParseExcelDate(pick.EndDate, DateTime.MaxValue),
            Rewards = rewards,
            CompleteConditionType = pick.CompleteConditionType,
            CompleteConditionCount = pick.CompleteConditionCount,
            CompleteConditionParameters = pick.CompleteConditionParameter ?? [],
            PreMissionIds = pick.PreMissionId ?? [],
            AccountLevel = Math.Max(1, pick.AccountLevel),
            TargetGroup = 1,
            SuddenMissionContentTypes = pick.ContentTags ?? []
        };
    }

    private static DateTime ParseExcelDate(string? value, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : fallback;
    }

    [ProtocolHandler(Protocol.Mission_GuideMissionSeasonList)]
    public async Task<GuideMissionSeasonListResponse> GuideMissionSeasonList(
        SchaleDataContext db,
        GuideMissionSeasonListRequest request,
        GuideMissionSeasonListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Official always sends both collections; they are empty for an account that has not started a guide-mission season yet.
        response.GuideMissionSeasonDBs = [];
        response.MissionProgressDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Mission_Reward)]
    public async Task<MissionRewardResponse> Reward(
        SchaleDataContext db,
        MissionRewardRequest request,
        MissionRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionProgress = await db.MissionProgresses
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == request.MissionUniqueId);

        if (missionProgress == null)
        {
            throw new WebAPIException(WebAPIErrorCode.MissionRewardInvalid,
                $"Mission {request.MissionUniqueId} has no progress row for this account");
        }

        // The client only offers the claim button once the mission is complete, so a request for an incomplete one is either a desync or a crafted packet. Without this guard the reward is handed out and the progress row consumed for a mission the player never finished.
        if (!missionProgress.Complete)
        {
            throw new WebAPIException(WebAPIErrorCode.MissionCannotComplete,
                $"Mission {request.MissionUniqueId} is not complete");
        }

        // An unknown id falling through with a null excel would still delete the progress row and write a history entry while skipping the reward, leaving the mission permanently unclaimable.
        var missionExcel = _excelService.GetTable<MissionExcelT>().FirstOrDefault(x => x.Id == request.MissionUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound,
                $"Mission excel {request.MissionUniqueId} not found");

        response.MissionProgressDBs = new List<MissionProgressDB>();

        var parcelResultList = ParcelResult.ConvertParcelResult(
            missionExcel.MissionRewardParcelType,
            missionExcel.MissionRewardParcelId,
            missionExcel.MissionRewardAmount
        );

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcelResultList);
        response.ParcelResultDB = parcelResolver.ParcelResult;

        // Delete progress to mark as claimed
        db.MissionProgresses.Remove(missionProgress);

        var completeTime = account.GameSettings.ServerDateTime();

        // official's account-wide Mission_List returns the claim forever after
        db.MissionHistories.Add(new MissionHistoryDBServer
        {
            AccountServerId = account.ServerId,
            MissionUniqueId = missionProgress.MissionUniqueId,
            CompleteTime = completeTime
        });

        // Official's wire history carries only these two members - the detailed capture shows ServerId and AccountServerId omitted.
        response.AddedHistoryDB = new MissionHistoryDB
        {
            MissionUniqueId = missionProgress.MissionUniqueId,
            CompleteTime = completeTime
        };

        await db.SaveChangesAsync();

        if (missionExcel.Category == MissionCategory.Daily)
        {
            var updatedMetaMissions = _missionService.UpdateMissionProgress(
                db, 
                account, 
                MissionCompleteConditionType.Reset_DailyMissionFulfill, 
                1
            );
            
            // so the client sees the meta-mission progress bar move
            response.MissionProgressDBs = updatedMetaMissions;
            await db.SaveChangesAsync();
        }
        else
        {
            // Return empty list implies it's gone from active list.
            response.MissionProgressDBs = new List<MissionProgressDB>();
        }

        return response;
    }

    [ProtocolHandler(Protocol.Mission_MultipleReward)]
    public async Task<MissionMultipleRewardResponse> MultipleReward(
        SchaleDataContext db,
        MissionMultipleRewardRequest request,
        MissionMultipleRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionExcels = _excelService.GetTable<MissionExcelT>();
        var missionExcelById = missionExcels.ToDictionary(x => x.Id, x => x);

        var progresses = db.MissionProgresses
            .Where(x => x.AccountServerId == account.ServerId && x.Complete)
            .ToList();

        // MissionCategory.All is the client's "claim everything" tab value; no excel row ever carries it, so it must bypass the category filter rather than match against it.
        var targetProgresses = progresses
            .Where(p => missionExcelById.TryGetValue(p.MissionUniqueId, out var missionExcel)
                && (request.MissionCategory == MissionCategory.All
                    || missionExcel.Category == request.MissionCategory)
                && (!request.EventContentId.HasValue
                    || p.MissionUniqueId.ToString().StartsWith(request.EventContentId.Value.ToString())))
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
            if (!missionExcelById.TryGetValue(missionProgress.MissionUniqueId, out var missionExcel))
                continue;

            rewardParcels.AddRange(ParcelResult.ConvertParcelResult(
                missionExcel.MissionRewardParcelType,
                missionExcel.MissionRewardParcelId,
                missionExcel.MissionRewardAmount));

            var completeTime = account.GameSettings.ServerDateTime();

            // Persist the claim for the account-wide MissionHistoryUniqueIds; the wire copy carries only the two members official emits.
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

        if (rewardParcels.Count > 0)
        {
            var parcelResolver = await _parcelHandler.BuildParcel(db, account, rewardParcels);
            response.ParcelResultDB = parcelResolver.ParcelResult;
        }

        response.AddedHistoryDBs = addedHistory;

        // Count only the rows that actually were Daily missions (with the All tab, the batch can span every category).
        var claimedDailyCount = targetProgresses.Count(p =>
            missionExcelById.TryGetValue(p.MissionUniqueId, out var excel)
            && excel.Category == MissionCategory.Daily);

        if (claimedDailyCount > 0)
        {
            var updatedMetaMissions = _missionService.UpdateMissionProgress(
                db,
                account,
                MissionCompleteConditionType.Reset_DailyMissionFulfill,
                claimedDailyCount);

            if (updatedMetaMissions.Count > 0)
                response.MissionProgressDBs = updatedMetaMissions;
        }

        await db.SaveChangesAsync();

        return response;
    }
}
