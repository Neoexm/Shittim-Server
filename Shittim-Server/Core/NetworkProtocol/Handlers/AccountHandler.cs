using System.Text;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Data.Models;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Services;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class AccountHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ILogger<AccountHandler> _logger;

    public AccountHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ILogger<AccountHandler> logger) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _logger = logger;
    }

    [ProtocolHandler(Protocol.Account_CheckNexon)]
    public async Task<AccountCheckNexonResponse> CheckNexon(
        SchaleDataContext db,
        AccountCheckNexonRequest request,
        AccountCheckNexonResponse response)
    {
        var ticketBytes = Convert.FromBase64String(request.EnterTicket);
        var ticketString = Encoding.UTF8.GetString(ticketBytes);
        var parts = ticketString.Split('/');
        
        var publisherId = long.Parse(parts[0]);
        var token = parts[1];

        var user = await db.UserAccounts
            .FirstOrDefaultAsync(u => u.NpSN == publisherId);

        if (user == null)
        {
            var newUser = new UserAccount 
            { 
                Uid = -1, 
                NpSN = publisherId, 
                NpToken = token 
            };
            db.UserAccounts.Add(newUser);

            var newAccount = new AccountDBServer(publisherId);
            db.Accounts.Add(newAccount);
            
            await db.SaveChangesAsync();

            user = await db.UserAccounts.FirstAsync(u => u.NpSN == publisherId);
            var account = await db.Accounts.FirstAsync(a => a.PublisherAccountId == publisherId);
            
            user.Uid = account.ServerId;
            await AccountInitializationService.InitializeCompleteAccount(db, account);
            await db.SaveChangesAsync();
        }
        else if (user.NpToken != token)
        {
            user.NpToken = token;
            await db.SaveChangesAsync();
        }

        var sessionKey = await _sessionService.GenerateSession(publisherId);
        
        response.ResultState = 1;
        response.SessionKey = sessionKey;
        return response;
    }
    [ProtocolHandler(Protocol.Account_Auth)]
    public async Task<AccountAuthResponse> Auth(
        SchaleDataContext db,
        AccountAuthRequest request,
        AccountAuthResponse response)
    {
        if (request.SessionKey == null)
            throw new UnauthorizedAccessException("Session key required");

        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.CurrentVersion = request.Version;
        response.TTSCdnUri = "https://ba.dn.nexoncdn.co.kr/tts/version2/";
        response.IssueAlertInfos = [];
        response.AttendanceBookRewards = [];
        response.AttendanceHistoryDBs = [];
        response.RepurchasableMonthlyProductCountDBs = [];
        response.MonthlyProductParcel = [];
        response.MonthlyProductMail = [];
        response.BiweeklyProductParcel = [];
        response.BiweeklyProductMail = [];
        response.WeeklyProductParcel = [];
        response.WeeklyProductMail = [];
        response.EncryptedUID = "";
        response.AccountRestrictionsDB = new();
        response.ServerNotification = ServerNotificationFlag.None;
        response.StaticOpenConditions = Enum.GetValues<OpenConditionContent>()
            .ToDictionary(c => c, _ => OpenConditionLockReason.None);

        response.AccountDB = _mapper.Map<AccountDB>(account);
        response.MissionProgressDBs = db.GetAccountMissionProgresses(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Account_Create)]
    public async Task<AccountCreateResponse> Create(
        SchaleDataContext db,
        AccountCreateRequest request,
        AccountCreateResponse response)
    {
        if (request.SessionKey == null)
            throw new UnauthorizedAccessException("Session required");

        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        
        if (account.PublisherAccountId == null)
            throw new InvalidOperationException("Account not linked to publisher");

        await AccountInitializationService.InitializeCompleteAccount(db, account);

        var newSession = await _sessionService.GenerateSession(account.PublisherAccountId.Value);
        response.SessionKey = newSession;
        
        return response;
    }

    [ProtocolHandler(Protocol.Account_Nickname)]
    public async Task<AccountNicknameResponse> SetNickname(
        SchaleDataContext db,
        AccountNicknameRequest request,
        AccountNicknameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.Nickname = request.Nickname;
        account.CallName = request.Nickname;
        account.CallNameUpdateTime = account.GameSettings.CurrentDateTime.Date;
        
        await db.SaveChangesAsync();

        response.AccountDB = _mapper.Map<AccountDB>(account);
        return response;
    }

    [ProtocolHandler(Protocol.Account_CallName)]
    public async Task<AccountCallNameResponse> SetCallName(
        SchaleDataContext db,
        AccountCallNameRequest request,
        AccountCallNameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.CallName = request.CallName;
        account.CallNameUpdateTime = account.GameSettings.CurrentDateTime.Date;
        
        await db.SaveChangesAsync();

        response.AccountDB = _mapper.Map<AccountDB>(account);
        return response;
    }

    [ProtocolHandler(Protocol.Account_LoginSync)]
    public async Task<AccountLoginSyncResponse> LoginSync(
        SchaleDataContext db,
        AccountLoginSyncRequest request,
        AccountLoginSyncResponse response)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        ArgumentNullException.ThrowIfNull(account);
        _logger.LogInformation("GetAuthenticatedUser took {Ms}ms", sw.ElapsedMilliseconds);

        sw.Restart();
        response.CafeGetInfoResponse = new CafeGetInfoResponse
        {
            CafeDBs = db.GetAccountCafes(account.ServerId).ToMapList(_mapper),
            FurnitureDBs = db.GetAccountFurnitures(account.ServerId).ToMapList(_mapper)
        };
        _logger.LogInformation("Cafe queries took {Ms}ms", sw.ElapsedMilliseconds);

        sw.Restart();
        response.AccountCurrencySyncResponse = new AccountCurrencySyncResponse
        {
            AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper)
        };

        response.CharacterListResponse = new CharacterListResponse
        {
            CharacterDBs = db.GetAccountCharacters(account.ServerId).ToMapList(_mapper),
            TSSCharacterDBs = [],
            WeaponDBs = db.GetAccountWeapons(account.ServerId).ToMapList(_mapper),
            CostumeDBs = db.GetAccountCostumes(account.ServerId).ToMapList(_mapper)
        };

        response.ItemListResponse = new ItemListResponse
        {
            ItemDBs = db.GetAccountItems(account.ServerId).ToMapList(_mapper)
        };

        response.EquipmentItemListResponse = new EquipmentItemListResponse
        {
            EquipmentDBs = db.GetAccountEquipments(account.ServerId).ToMapList(_mapper)
        };

        response.CharacterGearListResponse = new CharacterGearListResponse
        {
            GearDBs = db.GetAccountGears(account.ServerId).ToMapList(_mapper)
        };

        response.EchelonListResponse = new EchelonListResponse
        {
            EchelonDBs = db.GetAccountEchelons(account.ServerId).ToMapList(_mapper)
        };

        response.MemoryLobbyListResponse = new MemoryLobbyListResponse
        {
            MemoryLobbyDBs = db.GetAccountMemoryLobbies(account.ServerId).ToMapList(_mapper)
        };

        response.CampaignListResponse = new CampaignListResponse
        {
            CampaignChapterClearRewardHistoryDBs = db.GetAccountCampaignChapterClearRewardHistories(account.ServerId).ToMapList(_mapper),
            StageHistoryDBs = db.GetAccountCampaignStageHistories(account.ServerId).GetNormalCampaignStageHistories().ToMapList(_mapper),
            StrategyObjecthistoryDBs = db.GetAccountStrategyObjectHistories(account.ServerId).ToMapList(_mapper)
        };

        response.ArenaLoginResponse = new ArenaLoginResponse
        {
            ArenaPlayerInfoDB = new ArenaPlayerInfoDB
            {
                CurrentSeasonId = account.ContentInfo.ArenaDataInfo.SeasonId,
                CurrentRank = 1,
                TimeRewardLastUpdateTime = account.GameSettings.ServerDateTime()
            }
        };

        response.RaidLoginResponse = new RaidLoginResponse
        {
            SeasonType = RaidSeasonType.Open
        };

        response.EliminateRaidLoginResponse = new EliminateRaidLoginResponse
        {
            SeasonType = RaidSeasonType.Open,
            SweepPointByRaidUniqueId = new()
        };

        response.CraftInfoListResponse = new CraftInfoListResponse();

        response.ClanLoginResponse = new ClanLoginResponse
        {
            AccountClanMemberDB = new ClanMemberDB { AccountId = account.ServerId }
        };

        var momoTalkOutlines = db.GetAccountMomoTalkOutLines(account.ServerId).ToList();
        response.MomotalkOutlineResponse = new MomoTalkOutLineResponse
        {
            FavorScheduleRecords = MomoTalkService.GetAllFavorSchedules(momoTalkOutlines),
            MomoTalkOutLineDBs = momoTalkOutlines.ToMapList(_mapper)
        };

        response.ScenarioListResponse = new ScenarioListResponse
        {
            ScenarioHistoryDBs = db.GetAccountScenarioHistories(account.ServerId).ToMapList(_mapper),
            ScenarioGroupHistoryDBs = db.GetAccountScenarioGroupHistories(account.ServerId).ToMapList(_mapper)
        };

        response.ShopGachaRecruitListResponse = new ShopGachaRecruitListResponse
        {
            ShopFreeRecruitHistoryDBs = []
        };

        response.TimeAttackDungeonLoginResponse = new TimeAttackDungeonLoginResponse
        {
            PreviousRoomDB = db.GetAccountTimeAttackDungeonRooms(account.ServerId).FirstOrDefaultMapTo(_mapper)
        };

        response.BillingPurchaseListByNexonResponse = new BillingPurchaseListByNexonResponse
        {
            CountList = [],
            OrderList = [],
            MonthlyProductList = [],
            BlockedProductDBs = [],
            GachaTicketItemIdList = [],
            ProductMonthlyIdInMailList = []
        };

        response.EventContentPermanentListResponse = new EventContentPermanentListResponse
        {
            PermanentDBs = db.GetAccountEventContentPermanents(account.ServerId).ToMapList(_mapper)
        };

        response.AttachmentGetResponse = new AttachmentGetResponse
        {
            AccountAttachmentDB = db.GetAccountAttachments(account.ServerId).FirstMapTo(_mapper)
        };

        response.AttachmentEmblemListResponse = new AttachmentEmblemListResponse
        {
            EmblemDBs = db.GetAccountEmblems(account.ServerId).ToMapList(_mapper)
        };

        response.ContentSweepMultiSweepPresetListResponse = new ContentSweepMultiSweepPresetListResponse
        {
            MultiSweepPresetDBs = []
        };

        response.StickerListResponse = new StickerLoginResponse
        {
            StickerBookDB = db.GetAccountStickerBooks(account.ServerId).FirstMapTo(_mapper)
        };

        response.MultiFloorRaidSyncResponse = new MultiFloorRaidSyncResponse
        {
            MultiFloorRaidDBs = db.GetAccountMultiFloorRaids(account.ServerId).ToMapList(_mapper),
            ServerTimeTicks = account.GameSettings.EnableMultiFloorRaid 
                ? MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks 
                : account.GameSettings.ServerDateTimeTicks()
        };

        response.AccountLevelRewardIds = db.GetAccountLevelRewards(account.ServerId).Select(r => r.RewardId).ToList();
        _logger.LogInformation("All queries took {Ms}ms total", sw.ElapsedMilliseconds);
        
        response.FriendCount = 0;
        response.FriendCode = "SKYE_SERVER";
        
        response.StaticOpenConditions = Enum.GetValues<OpenConditionContent>()
            .ToDictionary(c => c, _ => OpenConditionLockReason.None);

        return response;
    }

    [ProtocolHandler(Protocol.Account_GetTutorial)]
    public async Task<AccountGetTutorialResponse> GetTutorial(
        SchaleDataContext db,
        AccountGetTutorialRequest request,
        AccountGetTutorialResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var tutorial = db.AccountTutorials.FirstOrDefault(x => x.AccountServerId == account.ServerId);

        response.TutorialIds = tutorial?.TutorialIds 
            ?? Enumerable.Range(1, 27).Select(i => (long)i).ToList();

        return response;
    }

    [ProtocolHandler(Protocol.Account_SetTutorial)]
    public async Task<AccountSetTutorialResponse> SetTutorial(
        SchaleDataContext db,
        AccountSetTutorialRequest request,
        AccountSetTutorialResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var tutorial = db.AccountTutorials.FirstOrDefault(x => x.AccountServerId == account.ServerId);

        if (tutorial == null)
        {
            tutorial = new AccountTutorial
            {
                AccountServerId = account.ServerId,
                TutorialIds = request.TutorialIds.ToList()
            };
            db.AccountTutorials.Add(tutorial);
        }
        else
        {
            tutorial.TutorialIds = request.TutorialIds.ToList();
        }

        await db.SaveChangesAsync();
        return response;
    }

    [ProtocolHandler(Protocol.Account_CheckAccountLevelReward)]
    public async Task<CheckAccountLevelRewardResponse> CheckLevelReward(
        SchaleDataContext db,
        CheckAccountLevelRewardRequest request,
        CheckAccountLevelRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var rewardIds = db.GetAccountLevelRewards(account.ServerId)
            .Select(r => r.RewardId)
            .ToList();

        response.AccountLevelRewardIds = rewardIds;
        return response;
    }

    [ProtocolHandler(Protocol.Account_ReceiveAccountLevelReward)]
    public async Task<ReceiveAccountLevelRewardResponse> ReceiveLevelReward(
        SchaleDataContext db,
        ReceiveAccountLevelRewardRequest request,
        ReceiveAccountLevelRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var claimedIds = db.GetAccountLevelRewards(account.ServerId)
            .Select(r => r.RewardId)
            .ToList();

        var availableRewards = _excelService.GetTable<AccountLevelRewardExcelT>()
            .Where(r => r.Level <= account.Level && !claimedIds.Contains(r.Id))
            .ToList();

        var newRewards = availableRewards.Select(r => new AccountLevelRewardDBServer
        {
            AccountServerId = account.ServerId,
            RewardId = r.Id
        });

        db.AccountLevelRewards.AddRange(newRewards);
        await db.SaveChangesAsync();

        response.ReceivedAccountLevelRewardIds = availableRewards.Select(r => r.Id).ToList();
        response.ParcelResultDB = new();

        return response;
    }

    [ProtocolHandler(Protocol.Account_SetRepresentCharacterAndComment)]
    public async Task<AccountSetRepresentCharacterAndCommentResponse> SetRepresentCharacter(
        SchaleDataContext db,
        AccountSetRepresentCharacterAndCommentRequest request,
        AccountSetRepresentCharacterAndCommentResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.RepresentCharacterServerId != 0)
            account.RepresentCharacterServerId = request.RepresentCharacterServerId;
        
        if (request.Comment != null)
            account.Comment = request.Comment;

        await db.SaveChangesAsync();

        response.AccountDB = _mapper.Map<AccountDB>(account);
        
        var character = db.GetAccountCharacters(account.ServerId)
            .FirstOrDefault(c => c.ServerId == request.RepresentCharacterServerId);
        response.RepresentCharacterDB = character != null 
            ? _mapper.Map<CharacterDB>(character) 
            : null;

        return response;
    }

    [ProtocolHandler(Protocol.Account_LinkReward)]
    public async Task<AccountLinkRewardResponse> LinkReward(
        SchaleDataContext db,
        AccountLinkRewardRequest request,
        AccountLinkRewardResponse response)
    {
        return response;
    }

    [ProtocolHandler(Protocol.Account_InvalidateToken)]
    public async Task<AccountInvalidateTokenResponse> InvalidateToken(
        SchaleDataContext db,
        AccountInvalidateTokenRequest request,
        AccountInvalidateTokenResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        _sessionService.RevokeSession(account.ServerId);
        return response;
    }
}
