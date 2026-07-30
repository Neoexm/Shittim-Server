using System.Text;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Data.Models;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.GameLogic.Services;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class AccountHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ILogger<AccountHandler> _logger;
    private readonly MissionService _missionService;
    private readonly AttendanceService _attendanceService;
    private readonly ParcelHandler _parcelHandler;

    public AccountHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ILogger<AccountHandler> logger,
        MissionService missionService,
        AttendanceService attendanceService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _logger = logger;
        _missionService = missionService;
        _attendanceService = attendanceService;
        _parcelHandler = parcelHandler;
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
        var gatewayCrypto = GatewaySessionCryptoBuilder.Build(request.ClientGeneratedKey, request.ClientGeneratedIV);
        
        response.ResultState = 1;
        response.SessionKey = sessionKey;
        response.EncryptedKey = gatewayCrypto.EncryptedKey;
        response.SignedKey = gatewayCrypto.SignedKey;
        response.EncryptedIV = gatewayCrypto.EncryptedIV;
        response.SignedIV = gatewayCrypto.SignedIV;
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

        // The client evaluates content locks (cafe, clan, crafting, schedule, ...) client-side from OpenConditionExcel's ScenarioModeId prerequisites against the account's scenario history. Seed every referenced mode as cleared so all content is unlocked by default, which also repairs accounts whose rows predate the seeding.
        await EnsureOpenConditionScenariosCleared(db, account);

        // The client always sends Version: 0 here; the official server answers with its own current data-build number (446690 against the captured 1.90.443855 client).
        response.CurrentVersion = Config.Instance.ServerConfiguration.AuthCurrentVersion;
        response.MinimumVersion = 0;
        response.IsDevelopment = false;
        response.BattleValidation = false;
        response.UpdateRequired = false;
        response.TTSCdnUri = "https://ba.dn.nexoncdn.co.kr/tts/version2/";
        response.IssueAlertInfos = [];
        // Official's Auth carries the full definitions of books with an unclaimed day today, and [] once everything is claimed (off1 04:12 pre-claim: 2 books; off2/off3 later the same game day: []). Histories are always the account's complete record.
        response.AttendanceBookRewards = _attendanceService.BuildClaimableBooks(db, account);
        response.AttendanceHistoryDBs = _attendanceService.BuildHistories(db, account);
        response.RepurchasableMonthlyProductCountDBs = [];
        response.MonthlyProductParcel = [];
        response.MonthlyProductMail = [];
        response.BiweeklyProductParcel = [];
        response.BiweeklyProductMail = [];
        response.WeeklyProductParcel = [];
        response.WeeklyProductMail = [];
        // the support UID: an opaque 26-char base32 string, never empty on official.
        response.EncryptedUID = BuildEncryptedUid(account.PublisherAccountId ?? account.ServerId);
        response.AccountRestrictionsDB = new();
        // Official Account_Auth always carries OptionDB, and it is always {} (no option has ever been observed inside it).
        response.OptionDB = new();
        // accountBanByNexonDBs / DailyRecordDBs stay null: official omits them.
        response.StaticOpenConditions = BuildOfficialStaticOpenConditions();

        response.AccountDB = _mapper.Map<AccountDB>(account);
        // Official omits the envelope MissionProgressDBs unless the login created/changed progress (first-login-of-day packs); an empty [] is never sent.
        // Battle-pass and event-content rows share the MissionProgresses table but never appear here on official - an id the client cannot resolve against MissionExcel/GuideMissionExcel poisons its mission model.
        var missionProgresses = MissionHandler.FilterMissionScreenProgresses(
                db.GetAccountMissionProgresses(account.ServerId).ToList(), _excelService)
            .ToMapList(_mapper);
        response.MissionProgressDBs = missionProgresses.Count > 0 ? missionProgresses : null;

        return response;
    }

    // The official StaticOpenConditions dictionary (Account_Auth / Account_LoginSync) contains exactly these keys, in this order - notably no Favor/Prologue/Guild/WorldRaid, and it includes zero-valued entries (verified against live captures of client 1.90).
    private static readonly OpenConditionContent[] OfficialStaticOpenConditionKeys =
    {
        OpenConditionContent.Shop, OpenConditionContent.Gacha, OpenConditionContent.LobbyIllust,
        OpenConditionContent.Raid, OpenConditionContent.Cafe, OpenConditionContent.Unit_Growth_Skill,
        OpenConditionContent.Unit_Growth_LevelUp, OpenConditionContent.Unit_Growth_Transcendence,
        OpenConditionContent.WeekDungeon, OpenConditionContent.Arena, OpenConditionContent.Academy,
        OpenConditionContent.Equip, OpenConditionContent.Item, OpenConditionContent.Mission,
        OpenConditionContent.WeekDungeon_Chase, OpenConditionContent.__Deprecated_WeekDungeon_FindGift,
        OpenConditionContent.__Deprecated_WeekDungeon_Blood, OpenConditionContent.Story_Sub,
        OpenConditionContent.Story_Replay, OpenConditionContent.None, OpenConditionContent.Shop_Gem,
        OpenConditionContent.Craft, OpenConditionContent.Student, OpenConditionContent.GuideMission,
        OpenConditionContent.Clan, OpenConditionContent.Echelon, OpenConditionContent.Campaign,
        OpenConditionContent.EventContent, OpenConditionContent.EventStage_1, OpenConditionContent.EventStage_2,
        OpenConditionContent.Talk, OpenConditionContent.Billing, OpenConditionContent.Schedule,
        OpenConditionContent.Story, OpenConditionContent.Tactic_Speed, OpenConditionContent.Cafe_Invite,
        OpenConditionContent.Cafe_Invite_2, OpenConditionContent.EventMiniGame_1, OpenConditionContent.SchoolDungeon,
        OpenConditionContent.TimeAttackDungeon, OpenConditionContent.ShiftingCraft, OpenConditionContent.Tactic_Skip,
        OpenConditionContent.Mulligan, OpenConditionContent.EventPermanent, OpenConditionContent.Main_L_1_2,
        OpenConditionContent.Main_L_1_3, OpenConditionContent.Main_L_1_4, OpenConditionContent.EliminateRaid,
        OpenConditionContent.Cafe_2, OpenConditionContent.MultiFloorRaid, OpenConditionContent.StrategySkip,
        OpenConditionContent.MinigameDreamMaker, OpenConditionContent.MiniGameDefense, OpenConditionContent.MiniGameCCG,
        OpenConditionContent.Main_L_1_5,
    };

    internal static Dictionary<OpenConditionContent, OpenConditionLockReason> BuildOfficialStaticOpenConditions()
        => OfficialStaticOpenConditionKeys.ToDictionary(c => c, _ => OpenConditionLockReason.None);

    private async Task EnsureOpenConditionScenariosCleared(SchaleDataContext db, AccountDBServer account)
    {
        var requiredModeIds = _excelService.GetTable<OpenConditionExcelT>()
            .Where(x => x.ScenarioModeId != 0)
            .Select(x => x.ScenarioModeId)
            .Distinct()
            .ToList();

        if (requiredModeIds.Count == 0)
            return;

        var cleared = db.GetAccountScenarioHistories(account.ServerId)
            .Select(x => x.ScenarioUniqueId)
            .ToHashSet();

        var now = account.GameSettings.ServerDateTime();
        var added = false;
        foreach (var modeId in requiredModeIds.Where(id => !cleared.Contains(id)))
        {
            db.ScenarioHistories.Add(new ScenarioHistoryDBServer
            {
                AccountServerId = account.ServerId,
                ScenarioUniqueId = modeId,
                ClearDateTime = now
            });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }

    // Deterministic 26-char base32 (A-Z2-7) support UID, mirroring the official format (e.g. "2YIXKGSNBP435QRHVUZD3VYUHE"). Opaque to the client.
    private static string BuildEncryptedUid(long publisherAccountId)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var hash = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes($"shittim-uid:{publisherAccountId}"));
        var chars = new char[26];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[hash[i] % 32];
        return new string(chars);
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

        // Trigger Daily Login Mission Progress
        _missionService.UpdateMissionProgress(db, account, MissionCompleteConditionType.Reset_DailyLogin, 1);
        await db.SaveChangesAsync();

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
            AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper),
            // Official always sends this map, empty when nothing expired.
            ExpiredCurrency = new()
        };

        response.CharacterListResponse = new CharacterListResponse
        {
            CharacterDBs = db.GetAccountCharacters(account.ServerId).ToMapList(_mapper),
            TSSCharacterDBs = [],
            WeaponDBs = db.GetAccountWeapons(account.ServerId).ToMapList(_mapper),
            CostumeDBs = db.GetAccountCostumes(account.ServerId).ToMapList(_mapper)
        };

        // Official Account_LoginSync has NO ItemListResponse child (the client fetches Item_List separately right after login), so it stays null here.

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
                PlayerGroupId = 1,
                CurrentRank = 1,
                TimeRewardLastUpdateTime = account.ContentInfo.ArenaDataInfo.TimeRewardLastUpdateTime
                    ?? account.GameSettings.ServerDateTime(),
                BattleEnterActiveTime = account.ContentInfo.ArenaDataInfo.BattleEnterActiveTime
                    ?? account.GameSettings.ServerDateTime(),
                DailyRewardActiveTime = account.GameSettings.ServerDateTime()
            }
        };

        response.RaidLoginResponse = new RaidLoginResponse
        {
            SeasonType = RaidSeasonType.Open
        };

        // Official EliminateRaidLoginResponse carries only Protocol + SeasonType.
        response.EliminateRaidLoginResponse = new EliminateRaidLoginResponse
        {
            SeasonType = RaidSeasonType.Open
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

        // Populate Billing Response (CRITICAL for BattlePass Visual Unlock)
        var billingResponse = new BillingPurchaseListByNexonResponse
        {
            CountList = [],
            OrderList = [],
            MonthlyProductList = [],
            BlockedProductDBs = [],
            GachaTicketItemIdList = [],
            ProductMonthlyIdInMailList = [],
            // Official always includes these two (as [] when empty).
            BattlePassIdInMailList = [],
            DailyRecordIdInMailList = []
        };

        var battlePasses = db.BattlePasses.Where(x => x.AccountServerId == account.ServerId && x.PurchaseGroupId != 0).ToList();
        var battlePassProductList = new List<BattlePassProductPurchaseDB>();

        if (battlePasses.Any())
        {
            var productExcels = _excelService.GetTable<ProductExcelT>();
            var shopCashExcels = _excelService.GetTable<ShopCashExcelT>();

            foreach (var bp in battlePasses)
            {
                // A battle pass is sold through a ProductExcel row carrying a ProductBattlePass (28) parcel; the parcel id at that slot is the BattlePassId.
                var battlePassProducts = productExcels.Where(x => x.ParcelType.Contains(ParcelType.ProductBattlePass)).ToList();

                ProductExcelT product = battlePassProducts.FirstOrDefault(x => {
                    var idx = x.ParcelType.IndexOf(ParcelType.ProductBattlePass);
                    return idx >= 0 && idx < x.ParcelId.Count && x.ParcelId[idx] == bp.BattlePassId;
                });

                if (product != null)
                {
                    var shopCash = shopCashExcels.FirstOrDefault(x => x.CashProductId == product.Id);
                    if (shopCash != null)
                    {
                        battlePassProductList.Add(new BattlePassProductPurchaseDB
                        {
                            ProductId = shopCash.Id, 
                            BattlePassId = bp.BattlePassId,
                            PurchaseBattlePassGroupId = bp.PurchaseGroupId
                        });
                        _logger.LogDebug("[AccountHandler] Mapped BP {BattlePassId} -> ShopCash {ShopCashId}", bp.BattlePassId, shopCash.Id);
                    }
                }
            }
        }
        billingResponse.BattlePassProductList = battlePassProductList;
        response.BillingPurchaseListByNexonResponse = billingResponse;

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

        // Official LoginSync also carries a MultiFloorRaidLoginResponse child (on the wire it is just {"Protocol":49004}) and a PickupFirstGetHistoryDBs list ([] when empty).
        response.MultiFloorRaidLoginResponse = new MultiFloorRaidLoginResponse();
        response.PickupFirstGetHistoryDBs = [];

        response.AccountLevelRewardIds = db.GetAccountLevelRewards(account.ServerId).Select(r => r.RewardId).ToList();
        _logger.LogInformation("All queries took {Ms}ms total", sw.ElapsedMilliseconds);

        response.FriendCount = 0;
        // Official friend codes are 8 uppercase letters, e.g. "ALAQJBVL".
        response.FriendCode = BuildFriendCode(account.ServerId);

        response.StaticOpenConditions = BuildOfficialStaticOpenConditions();

        // Official LoginSync children carry only their own payload + Protocol - no per-child ServerTimeTicks/SessionKey/notification fields. Zero the tick so the serializer drops it.
        foreach (var property in typeof(AccountLoginSyncResponse).GetProperties())
        {
            if (typeof(ResponsePacket).IsAssignableFrom(property.PropertyType)
                && property.GetValue(response) is ResponsePacket child
                && !ReferenceEquals(child, response))
            {
                child.ServerTimeTicks = 0;
            }
        }

        // Exception: the MultiFloorRaid time-travel feature pins a custom tick.
        if (account.GameSettings.EnableMultiFloorRaid && response.MultiFloorRaidSyncResponse != null)
            response.MultiFloorRaidSyncResponse.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    // 8-char A-Z code in the official FriendCode format, stable per account.
    private static string BuildFriendCode(long accountServerId)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes($"shittim-friend:{accountServerId}"));
        var chars = new char[8];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = (char)('A' + (hash[i] % 26));
        return new string(chars);
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

        // Grant the excel parcels BEFORE marking the ids consumed; the reverse order marks them claimed while granting nothing, permanently losing the rewards for the account.
        var parcels = availableRewards
            .Select(r => new ParcelResult(r.RewardParcelType, r.RewardParcelId, r.RewardParcelAmount))
            .Where(p => p.Amount > 0)
            .ToList();

        if (parcels.Count > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResultDB = resolver.ParcelResult;
        }
        else
        {
            response.ParcelResultDB = new ParcelResultDB
            {
                AccountDB = account.ToMap(_mapper),
                AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
            };
        }

        var newRewards = availableRewards.Select(r => new AccountLevelRewardDBServer
        {
            AccountServerId = account.ServerId,
            RewardId = r.Id
        });

        db.AccountLevelRewards.AddRange(newRewards);
        await db.SaveChangesAsync();

        response.ReceivedAccountLevelRewardIds = availableRewards.Select(r => r.Id).ToList();

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
