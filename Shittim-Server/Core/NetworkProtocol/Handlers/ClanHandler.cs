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
    // official's clan attendance mail carries localisation keys rather than rendered text, and the client resolves both. taken from the delivered mail in the non-truncated capture.
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

        // the president's represent-character field carries the character's UNIQUE id (16013 in the recovered official lobby), not a CharacterDB ServerId.
        var aronaCharacter = aronaAccount != null
            ? db.Characters.FirstOrDefault(c => c.ServerId == aronaAccount.RepresentCharacterServerId)
            : null;

        // shape per the recovered official Clan_Lobby (clan 4775): channel name is "channel_<ClanDBId>", and no ClanPresidentRepresentCharacterCostumeId / ClanJoinOption keys go out (both stay at their defaults there).
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

        // official's own member entry carries no AttachmentDB; other members' do.
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
        // every other member in the recovered official lobby carries a CafeComfortValue.
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

        // mission 1603 (clan-login daily) completes on the recovered Clan_Lobby.
        var updatedMissions = _missionService.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_ClanLogin);
        if (updatedMissions.Count > 0)
            response.MissionProgressDBs = updatedMissions;

        // the delivering response itself carries NewMailArrived: the official Clan_Lobby recovered from the non-truncated capture (its clan notice's raw control chars broke strict JSON, it was never actually cut) shows ServerNotification=4, mailbox empty at entry, so no bit 8.
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

        // ClanCheckResponse declares no fields, so the notification flag is the entire payload. Clan_Check is the only protocol in either official capture that ever sets 2048, and the gateway ORs its own mailbox bits on top - that is how official produces 2056 (2048|8).
        if (!HasClaimedClanAttendance(db, account))
            response.ServerNotification = ServerNotificationFlag.CanReceiveClanAttendanceReward;

        return response;
    }

    // official claims the day's clan attendance reward on Clan_Lobby rather than on a protocol of its own. in the non-truncated capture Clan_Check reports 2048 over an empty mailbox, the client then calls Clan_Lobby, and from the next response onwards ServerNotification is 8 with Mail_Check reporting one common mail - the reward is delivered as mail and 2048 has cleared by the following Clan_Check.
    // the other capture never calls Clan_Lobby and keeps 2048 across all three of its checks, the same rule seen from the other side.
    private async Task<bool> GrantClanAttendanceReward(SchaleDataContext db, AccountDBServer account)
    {
        if (HasClaimedClanAttendance(db, account))
            return false;

        var reward = _excelService.GetTable<ClanRewardExcelT>()
            .FirstOrDefault(x => x.ClanRewardType == ClanRewardType.Attendance);

        if (reward == null)
            return false;

        var now = account.GameSettings.ServerDateTime();

        // no MailNotificationService.MarkNewMail here: unlike attendance-reward mail, clan mail raises NewMailArrived only on the delivering Clan_Lobby response (the caller sets it) and leaves nothing pending - in the non-truncated capture the Mail_Check after this very delivery reports 8, where off1's Mail_Check after Attendance_Reward reports 12.
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
            // official's mail expired exactly 7 days after it was sent, to the second (2026-07-27T04:19:59 -> 2026-08-03T04:19:59).
            ExpireDate = now.AddDays(7),
            ParcelInfos =
            [
                new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = reward.RewardParcelType, Id = reward.RewardParcelId },
                    Amount = reward.RewardParcelAmount,
                    // official's mail carries both at rawValue 10000 (= 1x). left at the struct default they are 0, which the serializer drops as a default and which makes the client's own MultipliedAmount (Amount * Multiplier) render the reward as zero ActionPoint.
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One
                }
            ],
            // official omits RemainParcelInfos on this mail rather than sending [].
            RemainParcelInfos = null
        });

        await db.SaveChangesAsync();
        return true;
    }

    // one clan attendance reward per game day. there is no per-account claim column and the project has no EF migrations, so the delivered mail is the record: mail rows are only ever expired, never deleted, so a ClanAttendance mail sent since today's reset means today's reward is out.
    private static bool HasClaimedClanAttendance(SchaleDataContext db, AccountDBServer account)
    {
        var resetTime = DailyResetTime(account.GameSettings.ServerDateTime());

        return db.GetAccountMails(account.ServerId)
            .Any(x => x.Type == MailType.ClanAttendance && x.SendDate >= resetTime);
    }

    // the game day rolls over at 04:00 - where every EventContentSeason row in the shipped ExcelDB puts its phase boundaries, and just before the 04:19:59 send time of official's own mail.
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

    [ProtocolHandler(Protocol.Clan_MyAssistList)]
    public async Task<ClanMyAssistListResponse> MyAssistList(
        SchaleDataContext db,
        ClanMyAssistListRequest request,
        ClanMyAssistListResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // "which of my characters am I lending out" - legitimately empty on a solo server, but the list itself must be present or the client faults on it.
        response.ClanAssistSlotDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Clan_SetAssist)]
    public async Task<ClanSetAssistResponse> SetAssist(
        SchaleDataContext db,
        ClanSetAssistRequest request,
        ClanSetAssistResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClanAssistSlotDB = new ClanAssistSlotDB
        {
            EchelonType = request.EchelonType,
            SlotNumber = request.SlotNumber,
            CharacterDBId = request.CharacterDBId,
            CombatStyleIndex = request.CombatStyleIndex,
            DeployDate = account.GameSettings.ServerDateTime()
        };

        return response;
    }

    private ClanDB BuildClanDB(SchaleDataContext db, AccountDBServer account)
    {
        var state = account.GameSettings.Clan;
        var aronaAccount = db.Accounts.FirstOrDefault(x => x.DevId == SchaleAI.AccountDevId);
        var aronaCharacter = aronaAccount != null
            ? db.Characters.FirstOrDefault(c => c.ServerId == aronaAccount.RepresentCharacterServerId)
            : null;
        var accountCharacter = db.Characters.FirstOrDefault(c => c.ServerId == account.RepresentCharacterServerId);

        return state.IsPlayerOwned
            ? new ClanDB
            {
                ClanDBId = 777,
                ClanName = state.ClanName ?? "Schale Network",
                ClanChannelName = "channel_777",
                ClanPresidentNickName = account.Nickname ?? "Sensei",
                ClanPresidentRepresentCharacterUniqueId = accountCharacter?.UniqueId ?? account.RepresentCharacterServerId,
                ClanNotice = state.Notice ?? "",
                ClanJoinOption = state.JoinOption,
                ClanMemberCount = 2
            }
            : new ClanDB
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
    }

    // official's own member entry carries no AttachmentDB; other members' do.
    private ClanMemberDB BuildAccountMemberDB(SchaleDataContext db, AccountDBServer account)
    {
        var accountCharacter = db.Characters.FirstOrDefault(c => c.ServerId == account.RepresentCharacterServerId);
        return new ClanMemberDB
        {
            AccountId = account.ServerId,
            AccountLevel = account.Level,
            ClanDBId = 777,
            RepresentCharacterUniqueId = accountCharacter?.UniqueId ?? account.RepresentCharacterServerId,
            ClanSocialGrade = account.GameSettings.Clan.IsPlayerOwned ? ClanSocialGrade.President : ClanSocialGrade.Member,
            AccountNickName = account.Nickname ?? "Sensei",
            AttendanceCount = 33,
            GameLoginDate = account.LastConnectTime,
            LastLoginDate = account.LastConnectTime,
            JoinDate = account.CreateDate
        };
    }

    private ClanMemberDB BuildAronaMemberDB(SchaleDataContext db, AccountDBServer account)
    {
        var aronaAccount = db.Accounts.FirstOrDefault(x => x.DevId == SchaleAI.AccountDevId);
        var aronaCharacter = aronaAccount != null
            ? db.Characters.FirstOrDefault(c => c.ServerId == aronaAccount.RepresentCharacterServerId)
            : null;
        var aronaAttachment = aronaAccount != null ? db.GetAccountAttachments(aronaAccount.ServerId).FirstOrDefault() : null;
        var aronaCafeComfort = aronaAccount != null
            ? db.Cafes.FirstOrDefault(x => x.AccountServerId == aronaAccount.ServerId)?.ProductionDB?.ComfortValue ?? 0
            : 0;

        return new ClanMemberDB
        {
            AccountId = aronaAccount?.ServerId ?? 100000,
            AccountLevel = aronaAccount?.Level ?? 90,
            ClanDBId = 777,
            RepresentCharacterUniqueId = aronaCharacter?.UniqueId ?? 19900006,
            ClanSocialGrade = account.GameSettings.Clan.IsPlayerOwned ? ClanSocialGrade.Member : ClanSocialGrade.President,
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
        };
    }

    private IrcServerConfig BuildIrcConfig() => new()
    {
        HostAddress = _configuration["Irc:Address"] ?? "localhost",
        Port = int.Parse(_configuration["Irc:Port"] ?? "6667"),
        Password = _configuration["Irc:Password"] ?? ""
    };

    [ProtocolHandler(Protocol.Clan_Login)]
    public async Task<ClanLoginResponse> Login(
        SchaleDataContext db,
        ClanLoginRequest request,
        ClanLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (account.GameSettings.Clan.HasClan)
        {
            response.AccountClanDB = BuildClanDB(db, account);
            response.AccountClanMemberDB = BuildAccountMemberDB(db, account);
        }
        else
        {
            response.AccountClanMemberDB = new ClanMemberDB { AccountId = account.ServerId };
        }
        response.ClanAssistSlotDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Create)]
    public async Task<ClanCreateResponse> Create(
        SchaleDataContext db,
        ClanCreateRequest request,
        ClanCreateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var state = account.GameSettings.Clan;

        if (state.HasClan)
            throw new WebAPIException(WebAPIErrorCode.ClanAccountAlreadyJoinedClan, "Already in a clan");

        ValidateClanName(request.ClanNickName);

        state.HasClan = true;
        state.IsPlayerOwned = true;
        state.ClanName = request.ClanNickName;
        state.JoinOption = request.ClanJoinOption;
        state.JoinDate = account.GameSettings.ServerDateTime();
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        response.ClanDB = BuildClanDB(db, account);
        response.ClanMemberDB = BuildAccountMemberDB(db, account);
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Join)]
    public async Task<ClanJoinResponse> Join(
        SchaleDataContext db,
        ClanJoinRequest request,
        ClanJoinResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var state = account.GameSettings.Clan;

        if (state.HasClan)
            throw new WebAPIException(WebAPIErrorCode.ClanAccountAlreadyJoinedClan, "Already in a clan");
        if (request.ClanDBId != 777)
            throw new WebAPIException(WebAPIErrorCode.ClanNotFound, $"Clan {request.ClanDBId} does not exist");

        JoinDefaultClan(account);
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        response.IrcConfig = BuildIrcConfig();
        response.ClanDB = BuildClanDB(db, account);
        response.ClanMemberDB = BuildAccountMemberDB(db, account);

        return response;
    }

    [ProtocolHandler(Protocol.Clan_AutoJoin)]
    public async Task<ClanAutoJoinResponse> AutoJoin(
        SchaleDataContext db,
        ClanAutoJoinRequest request,
        ClanAutoJoinResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var state = account.GameSettings.Clan;

        if (state.HasClan)
            throw new WebAPIException(WebAPIErrorCode.ClanAccountAlreadyJoinedClan, "Already in a clan");

        JoinDefaultClan(account);
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        response.IrcConfig = BuildIrcConfig();
        response.ClanDB = BuildClanDB(db, account);
        response.ClanMemberDB = BuildAccountMemberDB(db, account);

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Quit)]
    public async Task<ClanQuitResponse> Quit(
        SchaleDataContext db,
        ClanQuitRequest request,
        ClanQuitResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var state = account.GameSettings.Clan;

        if (!state.HasClan)
            throw new WebAPIException(WebAPIErrorCode.ClanAccountAlreadyQuitClan, "Not in a clan");
        if (state.IsPlayerOwned)
            throw new WebAPIException(WebAPIErrorCode.ClanCanNotQuit, "The president dismisses, not quits");

        state.HasClan = false;
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Dismiss)]
    public async Task<ClanDismissResponse> Dismiss(
        SchaleDataContext db,
        ClanDismissRequest request,
        ClanDismissResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var state = account.GameSettings.Clan;

        if (!state.HasClan)
            throw new WebAPIException(WebAPIErrorCode.ClanAccountAlreadyQuitClan, "Not in a clan");
        if (!state.IsPlayerOwned)
            throw new WebAPIException(WebAPIErrorCode.ClanCanNotDismiss, "Only the president dismisses the clan");

        state.HasClan = false;
        state.IsPlayerOwned = false;
        state.ClanName = null;
        state.Notice = null;
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Search)]
    public async Task<ClanSearchResponse> Search(
        SchaleDataContext db,
        ClanSearchRequest request,
        ClanSearchResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // There is exactly one club; every search finds it unless the player owns it already.
        response.ClanDBs = account.GameSettings.Clan is { HasClan: true, IsPlayerOwned: true }
            ? []
            : [BuildClanDB(db, account)];

        return response;
    }

    [ProtocolHandler(Protocol.Clan_MemberList)]
    public async Task<ClanMemberListResponse> MemberList(
        SchaleDataContext db,
        ClanMemberListRequest request,
        ClanMemberListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClanDB = BuildClanDB(db, account);
        response.ClanMemberDBs = account.GameSettings.Clan.IsPlayerOwned
            ? [BuildAccountMemberDB(db, account), BuildAronaMemberDB(db, account)]
            : [BuildAronaMemberDB(db, account), BuildAccountMemberDB(db, account)];

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Member)]
    public async Task<ClanMemberResponse> Member(
        SchaleDataContext db,
        ClanMemberRequest request,
        ClanMemberResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var aronaAccount = db.Accounts.FirstOrDefault(x => x.DevId == SchaleAI.AccountDevId);

        AccountDBServer target;
        if (request.MemberAccountId == account.ServerId)
        {
            target = account;
            response.ClanMemberDB = BuildAccountMemberDB(db, account);
        }
        else if (aronaAccount != null && request.MemberAccountId == aronaAccount.ServerId)
        {
            target = aronaAccount;
            response.ClanMemberDB = BuildAronaMemberDB(db, account);
        }
        else
        {
            throw new WebAPIException(WebAPIErrorCode.ClanMemberNotFound, $"Member {request.MemberAccountId} not found");
        }

        var clanDb = BuildClanDB(db, account);
        response.ClanDB = clanDb;

        var targetCharacter = db.Characters.FirstOrDefault(c => c.ServerId == target.RepresentCharacterServerId);
        response.DetailedAccountInfoDB = new DetailedAccountInfoDB
        {
            AccountId = target.ServerId,
            Nickname = target.Nickname ?? "Sensei",
            Level = target.Level,
            ClanName = clanDb.ClanName,
            Comment = target.Comment ?? "",
            FriendCount = 0,
            RepresentCharacterUniqueId = targetCharacter?.UniqueId ?? target.RepresentCharacterServerId,
            CharacterCount = db.GetAccountCharacters(target.ServerId).Count(),
            AssistCharacterDBs = []
        };

        return response;
    }

    private static void JoinDefaultClan(AccountDBServer account)
    {
        var state = account.GameSettings.Clan;
        state.HasClan = true;
        state.IsPlayerOwned = false;
        state.JoinDate = account.GameSettings.ServerDateTime();
    }

    private void ValidateClanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new WebAPIException(WebAPIErrorCode.ClanNameEmptyString, "Clan name is empty");

        var maxLength = _excelService.GetTable<ConstCommonExcelT>().First().ClanNameLength;
        if (maxLength > 0 && name.Length > maxLength)
            throw new WebAPIException(WebAPIErrorCode.ClanNameWithInvalidLength, $"Clan name exceeds {maxLength} characters");
    }
}
