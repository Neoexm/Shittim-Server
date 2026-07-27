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
using Shittim_Server.GameClient;
using Microsoft.Extensions.Configuration;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ClanHandler : ProtocolHandlerBase
{
    // Official's clan attendance mail carries localisation keys rather than rendered text; the
    // client resolves both. Verified against the delivered mail in the non-truncated capture.
    private const string ClanAttendanceMailSender = "UI_MAILBOX_POST_SENDER_ARONA";
    private const string ClanAttendanceMailComment = "UI_MAILBOX_CLAN_ATTENDANCE_REWARD_MESSAGE_NORMAL";

    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly ExcelTableService _excelService;
    private readonly MissionService _missionService;

    public ClanHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper,
        IConfiguration configuration,
        ExcelTableService excelService,
        MissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _configuration = configuration;
        _excelService = excelService;
        _missionService = missionService;
    }

    [ProtocolHandler(Protocol.Clan_Lobby)]
    public async Task<ClanLobbyResponse> Lobby(
        SchaleDataContext db,
        ClanLobbyRequest request,
        ClanLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var aronaAccount = db.Accounts.FirstOrDefault(x => x.DevId == SchaleAI.AccountDevId);

        response.IrcConfig = new IrcServerConfig
        {
            HostAddress = _configuration["Irc:Address"] ?? "localhost",
            Port = int.Parse(_configuration["Irc:Port"] ?? "6667"),
            Password = _configuration["Irc:Password"] ?? ""
        };

        // The president's represent-character field carries the character's UNIQUE id (16013 in
        // the recovered official lobby), not a CharacterDB ServerId.
        var aronaCharacter = aronaAccount != null
            ? db.Characters.FirstOrDefault(c => c.ServerId == aronaAccount.RepresentCharacterServerId)
            : null;

        // Shape per the recovered official Clan_Lobby (clan 4775): channel name is
        // "channel_<ClanDBId>", and no ClanPresidentRepresentCharacterCostumeId / ClanJoinOption
        // keys go out (both stay at their defaults there).
        response.AccountClanDB = new ClanDB
        {
            ClanDBId = 777,
            ClanName = "Schale Network",
            ClanChannelName = "channel_777",
            ClanPresidentNickName = aronaAccount?.Nickname ?? "Arona",
            ClanPresidentRepresentCharacterUniqueId = aronaCharacter?.UniqueId ?? 19900006,
            ClanNotice = "Welcome to Schale Network\n\nEnjoy your stay, Sensei!",
            ClanJoinOption = ClanJoinOption.Free,
            ClanMemberCount = 2
        };

        var accountCharacter = db.Characters.FirstOrDefault(c => c.ServerId == account.RepresentCharacterServerId);

        // Official's OWN member entry carries no AttachmentDB — only the other members' entries do.
        response.AccountClanMemberDB = new ClanMemberDB
        {
            AccountId = account.ServerId,
            AccountLevel = account.Level,
            ClanDBId = 777,
            RepresentCharacterUniqueId = accountCharacter?.UniqueId ?? account.RepresentCharacterServerId,
            ClanSocialGrade = ClanSocialGrade.Member,
            AccountNickName = account.Nickname ?? "Sensei",
            AttendanceCount = 33,
            GameLoginDate = account.LastConnectTime,
            LastLoginDate = account.LastConnectTime,
            JoinDate = account.CreateDate
        };

        var aronaAttachment = aronaAccount != null ? db.GetAccountAttachments(aronaAccount.ServerId).FirstOrDefault() : null;
        // Every OTHER member in the recovered official lobby carries a CafeComfortValue.
        var aronaCafeComfort = aronaAccount != null
            ? db.Cafes.FirstOrDefault(x => x.AccountServerId == aronaAccount.ServerId)?.ProductionDB?.ComfortValue ?? 0
            : 0;

        response.ClanMemberDBs = new List<ClanMemberDB>
        {
            new ClanMemberDB
            {
                AccountId = aronaAccount?.ServerId ?? 100000,
                AccountLevel = aronaAccount?.Level ?? 90,
                ClanDBId = 777,
                RepresentCharacterUniqueId = aronaCharacter?.UniqueId ?? 19900006,
                ClanSocialGrade = ClanSocialGrade.President,
                AccountNickName = aronaAccount?.Nickname ?? "Arona",
                CafeComfortValue = aronaCafeComfort,
                GameLoginDate = aronaAccount?.LastConnectTime ?? DateTime.UtcNow,
                LastLoginDate = aronaAccount?.LastConnectTime ?? DateTime.UtcNow,
                JoinDate = aronaAccount?.CreateDate ?? DateTime.UtcNow,
                AttachmentDB = aronaAttachment?.ToMap(_mapper) ?? new AccountAttachmentDB
                {
                    AccountId = aronaAccount?.ServerId ?? 100000,
                    EmblemUniqueId = aronaAccount?.RepresentCharacterServerId ?? 19900006
                }
            },
            response.AccountClanMemberDB
        };

        // Official's lobby ticks the clan-login daily (mission 1603 completes on the recovered
        // Clan_Lobby).
        var updatedMissions = _missionService.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_ClanLogin);
        if (updatedMissions.Count > 0)
            response.MissionProgressDBs = updatedMissions;

        // The delivering response itself carries NewMailArrived: recovering the official Clan_Lobby
        // from the non-truncated capture (its clan notice's raw control chars broke strict JSON, it
        // was never actually cut) shows ServerNotification=4 — mailbox empty at entry, so no bit 8.
        if (await GrantClanAttendanceReward(db, account))
            response.ServerNotification |= ServerNotificationFlag.NewMailArrived;

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Check)]
    public async Task<ClanCheckResponse> Check(
        SchaleDataContext db,
        ClanCheckRequest request,
        ClanCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // ClanCheckResponse declares no fields, so the notification flag is the entire payload.
        // Clan_Check is the only protocol in either official capture that ever sets 2048, and the
        // gateway ORs its own mailbox bits on top — that is how official produces 2056 (2048|8).
        if (!HasClaimedClanAttendance(db, account))
            response.ServerNotification = ServerNotificationFlag.CanReceiveClanAttendanceReward;

        return response;
    }

    // Official claims the day's clan attendance reward on Clan_Lobby rather than on a protocol of
    // its own. In the non-truncated capture Clan_Check reports 2048 over an empty mailbox, the
    // client then calls Clan_Lobby, and from the next response onwards ServerNotification is 8
    // with Mail_Check reporting one common mail — the reward is delivered as mail and 2048 has
    // cleared by the following Clan_Check. The other capture never calls Clan_Lobby and keeps 2048
    // across all three of its checks, which is the same rule seen from the other side.
    private async Task<bool> GrantClanAttendanceReward(SchaleDataContext db, AccountDBServer account)
    {
        if (HasClaimedClanAttendance(db, account))
            return false;

        var reward = _excelService.GetTable<ClanRewardExcelT>()
            .FirstOrDefault(x => x.ClanRewardType == ClanRewardType.Attendance);

        if (reward == null)
            return false;

        var now = account.GameSettings.ServerDateTime();

        // Deliberately NO MailNotificationService.MarkNewMail: unlike attendance-reward mail, clan
        // mail raises NewMailArrived only on the delivering Clan_Lobby response (the caller sets
        // it) and leaves nothing pending — in the non-truncated capture the Mail_Check after this
        // very delivery reports 8, where off1's Mail_Check after Attendance_Reward reports 12.
        db.Mails.Add(new MailDBServer
        {
            AccountServerId = account.ServerId,
            Type = MailType.ClanAttendance,
            UniqueId = -1,
            Sender = ClanAttendanceMailSender,
            Comment = ClanAttendanceMailComment,
            LocalizedSender = Enum.GetValues<Language>().ToDictionary(x => x, _ => ClanAttendanceMailSender),
            LocalizedComment = Enum.GetValues<Language>().ToDictionary(x => x, _ => ClanAttendanceMailComment),
            SendDate = now,
            // Official's mail expired exactly 7 days after it was sent (2026-07-27T04:19:59 ->
            // 2026-08-03T04:19:59), to the second.
            ExpireDate = now.AddDays(7),
            ParcelInfos =
            [
                new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = reward.RewardParcelType, Id = reward.RewardParcelId },
                    Amount = reward.RewardParcelAmount,
                    // Official's mail carries both at rawValue 10000 (= 1x). Left at the struct
                    // default they are 0, which the serializer drops as a default AND which makes
                    // the client's own MultipliedAmount (Amount * Multiplier) render the reward
                    // as zero ActionPoint.
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One
                }
            ],
            // Official omits RemainParcelInfos on this mail rather than sending [].
            RemainParcelInfos = null
        });

        await db.SaveChangesAsync();
        return true;
    }

    // One clan attendance reward per game day. There is no per-account claim column and the project
    // has no EF migrations, so the delivered mail is the record: mail rows are only ever expired,
    // never deleted, so a ClanAttendance mail sent since today's reset means today's reward is out.
    private static bool HasClaimedClanAttendance(SchaleDataContext db, AccountDBServer account)
    {
        var resetTime = DailyResetTime(account.GameSettings.ServerDateTime());

        return db.GetAccountMails(account.ServerId)
            .Any(x => x.Type == MailType.ClanAttendance && x.SendDate >= resetTime);
    }

    // The game day rolls over at 04:00 — where every EventContentSeason row in the shipped ExcelDB
    // puts its phase boundaries, and just before the 04:19:59 send time of official's own mail.
    private static DateTime DailyResetTime(DateTime now)
    {
        var todaysReset = now.Date.AddHours(4);
        return now < todaysReset ? todaysReset.AddDays(-1) : todaysReset;
    }

    [ProtocolHandler(Protocol.Clan_AllAssistList)]
    public async Task<ClanAllAssistListResponse> AllAssistList(
        SchaleDataContext db,
        ClanAllAssistListRequest request,
        ClanAllAssistListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClanDBId = 777;
        response.AssistCharacterDBs = SchaleService.GetAssistCharacter(request.EchelonType);
        response.AssistCharacterRentHistoryDBs = [];

        return response;
    }
}
