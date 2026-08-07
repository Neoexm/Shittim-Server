using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Core.Math;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class AcademyHandler : ProtocolHandlerBase
{
    private const long LocationExpPerAttend = 100;

    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;
    private readonly MissionService _missionService;

    public AcademyHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        IMapper mapper,
        MissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
        _missionService = missionService;
    }

    private static DateTime LastDailyReset(DateTime now)
    {
        var todaysReset = now.Date.AddHours(4);
        return now < todaysReset ? todaysReset.AddDays(-1) : todaysReset;
    }

    // Lesson tickets refill to the rank-determined cap at the 04:00 game-day boundary; the per-zone schedule records reset with them.
    private void EnsureDailyTicketRefill(SchaleDataContext db, AccountDBServer account, AcademyDBServer? academy)
    {
        var currency = db.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (currency == null) return;

        var now = account.GameSettings.ServerDateTime();
        var reset = LastDailyReset(now);

        var lastUpdate = currency.UpdateTimeDict.TryGetValue(CurrencyTypes.AcademyTicket, out var t)
            ? t
            : DateTime.MinValue;
        if (lastUpdate >= reset) return;

        var ticketMax = _excelService.GetTable<AcademyTicketExcelT>()
            .Where(x => x.LocationRankSum <= currency.AcademyLocationRankSum)
            .OrderByDescending(x => x.LocationRankSum)
            .FirstOrDefault()?.ScheduleTicktetMax ?? 5;

        var current = currency.CurrencyDict.TryGetValue(CurrencyTypes.AcademyTicket, out var c) ? c : 0;
        currency.CurrencyDict[CurrencyTypes.AcademyTicket] = Math.Max(current, ticketMax);
        currency.UpdateTimeDict[CurrencyTypes.AcademyTicket] = now;
        db.Currencies.Update(currency);

        if (academy != null)
        {
            academy.ZoneScheduleGroupRecords = new Dictionary<long, List<long>>();
            academy.LastUpdate = now;
            db.Academies.Update(academy);
        }
    }

    [ProtocolHandler(Protocol.Academy_GetInfo)]
    public async Task<AcademyGetInfoResponse> GetInfo(
        SchaleDataContext db,
        AcademyGetInfoRequest request,
        AcademyGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var academy = await db.Academies
            .FirstOrDefaultAsync(a => a.AccountServerId == account.ServerId);

        EnsureDailyTicketRefill(db, account, academy);
        await db.SaveChangesAsync();

        if (academy != null)
        {
            response.AcademyDB = _mapper.Map<AcademyDB>(academy);
        }

        var locations = db.GetAccountAcademyLocations(account.ServerId).ToList();

        response.AcademyLocationDBs = _mapper.Map<List<AcademyLocationDB>>(locations);

        return response;
    }

    [ProtocolHandler(Protocol.Academy_AttendSchedule)]
    public async Task<AcademyAttendScheduleResponse> AttendSchedule(
        SchaleDataContext db,
        AcademyAttendScheduleRequest request,
        AcademyAttendScheduleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var academy = await db.Academies
            .FirstOrDefaultAsync(a => a.AccountServerId == account.ServerId);

        EnsureDailyTicketRefill(db, account, academy);

        var currency = db.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (currency == null
            || !currency.CurrencyDict.TryGetValue(CurrencyTypes.AcademyTicket, out var tickets)
            || tickets <= 0)
        {
            throw new WebAPIException(WebAPIErrorCode.AcademyTicketZero, "No schedule tickets left");
        }

        var zone = _excelService.GetTable<AcademyZoneExcelT>().FirstOrDefault(x => x.Id == request.ZoneId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound,
                $"Academy zone {request.ZoneId} not found");

        academy ??= new AcademyDBServer { AccountServerId = account.ServerId };
        academy.ZoneScheduleGroupRecords ??= new Dictionary<long, List<long>>();
        if (!academy.ZoneScheduleGroupRecords.TryGetValue(request.ZoneId, out var zoneRecords))
        {
            zoneRecords = [];
            academy.ZoneScheduleGroupRecords[request.ZoneId] = zoneRecords;
        }

        var locationRank = db.GetAccountAcademyLocations(account.ServerId)
            .FirstOrDefault(x => x.LocationId == zone.LocationId)?.Rank ?? 1;

        // The next unclaimed step of this zone's schedule group at the current location rank.
        var rewardRow = _excelService.GetTable<AcademyRewardExcelT>()
            .Where(x => x.ScheduleGroupId == zone.RewardGroupId
                && x.LocationRank <= locationRank
                && !zoneRecords.Contains(x.Id))
            .OrderBy(x => x.OrderInGroup)
            .FirstOrDefault()
            ?? throw new WebAPIException(WebAPIErrorCode.AcademyAlreadyAttendedSchedule,
                $"Zone {request.ZoneId} has no remaining schedule step today");

        var parcels = new List<ParcelResult>
        {
            new(ParcelType.Currency, (long)CurrencyTypes.AcademyTicket, -1),
            new(ParcelType.LocationExp, zone.LocationId, LocationExpPerAttend)
        };

        // Favor for every student attending this zone's lesson.
        var visitors = new List<VisitingCharacterDBServer>();
        if (academy.ZoneVisitCharacterDBs != null
            && academy.ZoneVisitCharacterDBs.TryGetValue(request.ZoneId, out var visiting))
        {
            visitors = visiting;
        }
        foreach (var visitor in visitors)
        {
            var favorExp = rewardRow.FavorExp;
            if (rewardRow.ExtraFavorExpProb > 0 && Random.Shared.Next(10000) < rewardRow.ExtraFavorExpProb)
                favorExp += rewardRow.ExtraFavorExp;
            if (favorExp > 0)
                parcels.Add(new ParcelResult(ParcelType.FavorExp, visitor.UniqueId, favorExp));
        }

        var rewardCount = new[]
        {
            rewardRow.RewardParcelType?.Count ?? 0,
            rewardRow.RewardParcelId?.Count ?? 0,
            rewardRow.RewardAmount?.Count ?? 0
        }.Min();
        for (int i = 0; i < rewardCount; i++)
        {
            if (rewardRow.RewardAmount![i] > 0)
                parcels.Add(new ParcelResult(rewardRow.RewardParcelType![i], rewardRow.RewardParcelId![i], rewardRow.RewardAmount[i]));
        }

        response.ExtraRewards = [];
        var extraCount = new[]
        {
            rewardRow.ExtraRewardParcelType?.Count ?? 0,
            rewardRow.ExtraRewardParcelId?.Count ?? 0,
            rewardRow.ExtraRewardAmount?.Count ?? 0,
            rewardRow.ExtraRewardProb?.Count ?? 0
        }.Min();
        for (int i = 0; i < extraCount; i++)
        {
            if (Random.Shared.Next(10000) >= rewardRow.ExtraRewardProb![i])
                continue;

            parcels.Add(new ParcelResult(rewardRow.ExtraRewardParcelType![i], rewardRow.ExtraRewardParcelId![i], rewardRow.ExtraRewardAmount![i]));
            response.ExtraRewards.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = rewardRow.ExtraRewardParcelType[i], Id = rewardRow.ExtraRewardParcelId[i] },
                Amount = rewardRow.ExtraRewardAmount[i],
                Multiplier = BasisPoint.One,
                Probability = BasisPoint.One
            });
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
        response.ParcelResultDB = resolver.ParcelResult;

        zoneRecords.Add(rewardRow.Id);
        academy.LastUpdate = account.GameSettings.ServerDateTime();
        if (academy.ServerId == 0 && db.Entry(academy).State == EntityState.Detached)
            db.Academies.Add(academy);
        else
            db.Academies.Update(academy);

        await db.SaveChangesAsync();

        response.AcademyDB = _mapper.Map<AcademyDB>(academy);

        // Official's Academy_AttendSchedule ticks the schedule dailies (MissionProgressDBs on every captured attend).
        var updatedMissions = _missionService.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_CompleteScheduleCount);
        if (updatedMissions.Count > 0)
            response.MissionProgressDBs = updatedMissions;

        return response;
    }

    [ProtocolHandler(Protocol.Academy_AttendFavorSchedule)]
    public async Task<AcademyAttendFavorScheduleResponse> AttendFavorSchedule(
        SchaleDataContext db,
        AcademyAttendFavorScheduleRequest request,
        AcademyAttendFavorScheduleResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
