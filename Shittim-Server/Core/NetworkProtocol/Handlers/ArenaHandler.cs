using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ArenaHandler : ProtocolHandlerBase
{
    // Fallback accrual when the ArenaRewardExcel Time row is absent from the shipped data.
    private const long DefaultTimeRewardGoldPerHour = 10000;
    private const int TimeRewardAccrualCapHours = 24;

    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    public ArenaHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
    }

    private ArenaPlayerInfoDB BuildPlayerInfo(AccountDBServer account)
    {
        var info = account.ContentInfo.ArenaDataInfo;
        var now = account.GameSettings.ServerDateTime();

        return new ArenaPlayerInfoDB
        {
            CurrentSeasonId = info.SeasonId,
            PlayerGroupId = 1,
            CurrentRank = 1,
            SeasonRecord = 1,
            AllTimeRecord = 1,
            CumulativeTimeReward = AccruedTimeReward(account),
            TimeRewardLastUpdateTime = info.TimeRewardLastUpdateTime ?? now,
            BattleEnterActiveTime = info.BattleEnterActiveTime ?? now,
            DailyRewardActiveTime = NextDailyRewardActiveTime(account)
        };
    }

    private long AccruedTimeReward(AccountDBServer account)
    {
        var info = account.ContentInfo.ArenaDataInfo;
        var now = account.GameSettings.ServerDateTime();
        var since = info.TimeRewardLastUpdateTime ?? now;
        var hours = Math.Clamp((now - since).TotalHours, 0, TimeRewardAccrualCapHours);
        return (long)(hours * TimeRewardRatePerHour().amount);
    }

    private (ParcelType type, long id, long amount) TimeRewardRatePerHour()
    {
        var timeRow = _excelService.GetTable<ArenaRewardExcelT>()
            .FirstOrDefault(x => x.ArenaRewardType == ArenaRewardType.Time
                && x.RankStart <= 1 && 1 <= x.RankEnd
                && (x.RewardParcelAmount?.Count ?? 0) > 0);

        if (timeRow == null)
            return (ParcelType.Currency, (long)CurrencyTypes.Gold, DefaultTimeRewardGoldPerHour);

        return (timeRow.RewardParcelType[0], timeRow.RewardParcelUniqueId[0], timeRow.RewardParcelAmount[0]);
    }

    private DateTime NextDailyRewardActiveTime(AccountDBServer account)
    {
        var info = account.ContentInfo.ArenaDataInfo;
        var now = account.GameSettings.ServerDateTime();
        if (info.LastDailyRewardClaimTime is DateTime last && last.Date == now.Date)
            return now.Date.AddDays(1);
        return now;
    }

    [ProtocolHandler(Protocol.Arena_Login)]
    public async Task<ArenaLoginResponse> Login(
        SchaleDataContext db,
        ArenaLoginRequest request,
        ArenaLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ArenaPlayerInfoDB = BuildPlayerInfo(account);

        return response;
    }

    [ProtocolHandler(Protocol.Arena_EnterLobby)]
    public async Task<ArenaEnterLobbyResponse> EnterLobby(
        SchaleDataContext db,
        ArenaEnterLobbyRequest request,
        ArenaEnterLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ArenaPlayerInfoDB = BuildPlayerInfo(account);
        response.OpponentUserDBs = ArenaService.CreateOpponents(db, account, _mapper);
        response.MapId = 1001;

        return response;
    }

    [ProtocolHandler(Protocol.Arena_OpponentList)]
    public async Task<ArenaOpponentListResponse> OpponentList(
        SchaleDataContext db,
        ArenaOpponentListRequest request,
        ArenaOpponentListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.PlayerRank = 1;
        response.OpponentUserDBs = ArenaService.CreateOpponents(db, account, _mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Arena_SyncEchelonSettingTime)]
    public async Task<ArenaSyncEchelonSettingTimeResponse> SyncEchelonSettingTime(
        SchaleDataContext db,
        ArenaSyncEchelonSettingTimeRequest request,
        ArenaSyncEchelonSettingTimeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EchelonSettingTime = account.GameSettings.ServerDateTime();

        return response;
    }

    [ProtocolHandler(Protocol.Arena_EnterBattlePart1)]
    public async Task<ArenaEnterBattlePart1Response> EnterBattlePart1(
        SchaleDataContext db,
        ArenaEnterBattlePart1Request request,
        ArenaEnterBattlePart1Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var attackingUser = new ArenaUserDB
        {
            AccountServerId = account.ServerId,
            NickName = account.Nickname ?? "Player",
            Rank = 1,
            Level = account.Level,
            RepresentCharacterUniqueId = account.RepresentCharacterServerId,
            AccountAttachmentDB = db.GetAccountAttachments(account.ServerId).FirstMapTo(_mapper),
            TeamSettingDB = ArenaService.CreateArenaTeamSetting(db, account, _mapper, false)
        };

        var opponentAccount = await db.Accounts.FirstOrDefaultAsync(x => x.ServerId == request.OpponentAccountServerId);
        
        ArenaUserDB defendingUser;
        if (opponentAccount != null)
        {
            defendingUser = ArenaService.CreateArenaUser(
                opponentAccount.ServerId,
                opponentAccount.RepresentCharacterServerId,
                opponentAccount.Nickname ?? "Opponent",
                request.OpponentRank,
                opponentAccount.Level,
                ArenaService.CreateArenaTeamSetting(db, opponentAccount, _mapper, true));
        }
        else
        {
            defendingUser = ArenaService.CreateArenaUser(
                request.OpponentAccountServerId,
                10065,
                "Dummy Opponent",
                request.OpponentRank,
                90,
                ArenaService.DummyTeamFormation);
        }

        response.ArenaBattleDB = new ArenaBattleDB
        {
            Season = 1,
            Group = 1,
            BattleStartTime = account.GameSettings.ServerDateTime(),
            Seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttackingUserDB = attackingUser,
            DefendingUserDB = defendingUser
        };

        return response;
    }

    [ProtocolHandler(Protocol.Arena_EnterBattlePart2)]
    public async Task<ArenaEnterBattlePart2Response> EnterBattlePart2(
        SchaleDataContext db,
        ArenaEnterBattlePart2Request request,
        ArenaEnterBattlePart2Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ArenaBattleDB = request.ArenaBattleDB;
        response.ArenaPlayerInfoDB = BuildPlayerInfo(account);

        var ticketCost = _excelService.GetTable<ConstArenaExcelT>().FirstOrDefault()?.TicketCost ?? 1;
        if (ticketCost <= 0) ticketCost = 1;

        var currency = db.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (currency != null
            && currency.CurrencyDict.TryGetValue(CurrencyTypes.ArenaTicket, out var tickets)
            && tickets >= ticketCost)
        {
            var resolver = await _parcelHandler.BuildParcel(
                db, account,
                new ParcelResult(ParcelType.Currency, (long)CurrencyTypes.ArenaTicket, ticketCost),
                isConsume: true);
            await db.SaveChangesAsync();
            response.AccountCurrencyDB = resolver.ParcelResult.AccountCurrencyDB;
        }

        response.AccountCurrencyDB ??= db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);

        return response;
    }

    // the unsplit enter; live clients send the Part1/Part2 pair instead but the protocol is still declared
    [ProtocolHandler(Protocol.Arena_EnterBattle)]
    public async Task<ArenaEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        ArenaEnterBattleRequest request,
        ArenaEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var opponentAccount = await db.Accounts.FirstOrDefaultAsync(x => x.ServerId == request.OpponentAccountServerId);
        var defendingUser = opponentAccount != null
            ? ArenaService.CreateArenaUser(
                opponentAccount.ServerId,
                opponentAccount.RepresentCharacterServerId,
                opponentAccount.Nickname ?? "Opponent",
                request.OpponentIndex + 1,
                opponentAccount.Level,
                ArenaService.CreateArenaTeamSetting(db, opponentAccount, _mapper, true))
            : ArenaService.CreateArenaUser(
                request.OpponentAccountServerId,
                10065,
                "Dummy Opponent",
                request.OpponentIndex + 1,
                90,
                ArenaService.DummyTeamFormation);

        response.ArenaBattleDB = new ArenaBattleDB
        {
            Season = 1,
            Group = 1,
            BattleStartTime = account.GameSettings.ServerDateTime(),
            Seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttackingUserDB = new ArenaUserDB
            {
                AccountServerId = account.ServerId,
                NickName = account.Nickname ?? "Player",
                Rank = 1,
                Level = account.Level,
                RepresentCharacterUniqueId = account.RepresentCharacterServerId,
                AccountAttachmentDB = db.GetAccountAttachments(account.ServerId).FirstMapTo(_mapper),
                TeamSettingDB = ArenaService.CreateArenaTeamSetting(db, account, _mapper, false)
            },
            DefendingUserDB = defendingUser
        };
        response.ArenaPlayerInfoDB = BuildPlayerInfo(account);
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Arena_CumulativeTimeReward)]
    public async Task<ArenaCumulativeTimeRewardResponse> CumulativeTimeReward(
        SchaleDataContext db,
        ArenaCumulativeTimeRewardRequest request,
        ArenaCumulativeTimeRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = account.ContentInfo.ArenaDataInfo;
        var now = account.GameSettings.ServerDateTime();

        var accrued = AccruedTimeReward(account);
        if (accrued > 0)
        {
            var (type, id, _) = TimeRewardRatePerHour();
            var resolver = await _parcelHandler.BuildParcel(
                db, account, new ParcelResult(type, id, accrued));
            response.ParcelResult = resolver.ParcelResult;
        }
        else
        {
            response.ParcelResult = new ParcelResultDB
            {
                AccountDB = account.ToMap(_mapper),
                AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
            };
        }

        info.CumulativeTimeReward = 0;
        info.TimeRewardLastUpdateTime = now;
        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.TimeRewardAmount = 0;
        response.TimeRewardLastUpdateTime = now;

        return response;
    }

    [ProtocolHandler(Protocol.Arena_DailyReward)]
    public async Task<ArenaDailyRewardResponse> DailyReward(
        SchaleDataContext db,
        ArenaDailyRewardRequest request,
        ArenaDailyRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = account.ContentInfo.ArenaDataInfo;
        var now = account.GameSettings.ServerDateTime();

        var alreadyClaimedToday = info.LastDailyRewardClaimTime is DateTime last && last.Date == now.Date;
        if (!alreadyClaimedToday)
        {
            var dailyRow = _excelService.GetTable<ArenaRewardExcelT>()
                .FirstOrDefault(x => x.ArenaRewardType == ArenaRewardType.Daily
                    && x.RankStart <= 1 && 1 <= x.RankEnd
                    && (x.RewardParcelAmount?.Count ?? 0) > 0);

            var parcels = dailyRow != null
                ? ParcelResult.ConvertParcelResult(dailyRow.RewardParcelType, dailyRow.RewardParcelUniqueId, dailyRow.RewardParcelAmount)
                : new List<ParcelResult> { new(ParcelType.Currency, (long)CurrencyTypes.ArenaTicket, 5) };

            var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResult = resolver.ParcelResult;

            info.LastDailyRewardClaimTime = now;
            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();
        }
        else
        {
            response.ParcelResult = new ParcelResultDB
            {
                AccountDB = account.ToMap(_mapper),
                AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
            };
        }

        response.DailyRewardActiveTime = NextDailyRewardActiveTime(account);

        return response;
    }

    [ProtocolHandler(Protocol.Arena_RankList)]
    public async Task<ArenaRankListResponse> RankList(
        SchaleDataContext db,
        ArenaRankListRequest request,
        ArenaRankListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var rankedUsers = new List<ArenaUserDB>();
        var rank = 1;
        foreach (var other in db.Accounts.OrderByDescending(x => x.Level).ThenBy(x => x.ServerId).ToList())
        {
            rankedUsers.Add(ArenaService.CreateArenaUser(
                other.ServerId,
                other.RepresentCharacterServerId,
                other.Nickname ?? "Sensei",
                rank++,
                other.Level,
                ArenaService.CreateArenaTeamSetting(db, other, _mapper, true)));
        }

        response.TopRankedUserDBs = rankedUsers
            .Skip(Math.Max(0, request.StartIndex))
            .Take(request.Count > 0 ? request.Count : rankedUsers.Count)
            .ToList();

        return response;
    }

    [ProtocolHandler(Protocol.Arena_History)]
    public async Task<ArenaHistoryResponse> History(
        SchaleDataContext db,
        ArenaHistoryRequest request,
        ArenaHistoryResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // No arena battle persistence yet; an empty (non-null) list keeps the client happy.
        response.ArenaHistoryDBs = [];
        response.ArenaDamageReportDB = [];

        return response;
    }

    [ProtocolHandler(Protocol.Arena_SettingChange)]
    public async Task<ArenaSettingChangeResponse> SettingChange(
        SchaleDataContext db,
        ArenaSettingChangeRequest request,
        ArenaSettingChangeResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Arena_SettingAnonymous)]
    public async Task<ArenaSettingAnonymousResponse> SettingAnonymous(
        SchaleDataContext db,
        ArenaSettingAnonymousRequest request,
        ArenaSettingAnonymousResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.ContentInfo.Option.ArenaIsAnonymous = request.IsAnonymous;
        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Arena_BattleResult)]
    public async Task<ArenaBattleResultResponse> BattleResult(
        SchaleDataContext db,
        ArenaBattleResultRequest request,
        ArenaBattleResultResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Arena_CheckSeasonCloseReward)]
    public async Task<ArenaCheckSeasonCloseRewardResponse> CheckSeasonCloseReward(
        SchaleDataContext db,
        ArenaCheckSeasonCloseRewardRequest request,
        ArenaCheckSeasonCloseRewardResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Arena_RecordSync)]
    public async Task<ArenaRecordSyncResponse> RecordSync(
        SchaleDataContext db,
        ArenaRecordSyncRequest request,
        ArenaRecordSyncResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Arena_TicketPurchase)]
    public async Task<ArenaTicketPurchaseResponse> TicketPurchase(
        SchaleDataContext db,
        ArenaTicketPurchaseRequest request,
        ArenaTicketPurchaseResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Arena_DamageReport)]
    public async Task<ArenaDamageReportResponse> DamageReport(
        SchaleDataContext db,
        ArenaDamageReportRequest request,
        ArenaDamageReportResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
