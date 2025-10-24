// Models needed: Character, AccountCurrency, AccountTutorial, AccountLevelReward
// BAContext additions: DbSet<Character>, DbSet<AccountCurrency>, DbSet<AccountTutorial>, DbSet<AccountLevelReward>
// Migrations: dotnet ef migrations add AddAccountGameData

using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Services;
using BlueArchiveAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using Plana.FlatData;

namespace BlueArchiveAPI.Handlers
{
    public static class Account
    {
        /// <summary>
        /// Handles initial login check and account creation
        /// Protocol: Account_CheckNexon
        /// </summary>
        [ProtocolHandler(Protocol.Account_CheckNexon)]
        public class Login : BaseHandler<AccountCheckNexonRequest, AccountCheckNexonResponse>
        {
            private readonly BAContext _dbContext;

            public Login(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountCheckNexonResponse> Handle(AccountCheckNexonRequest request)
            {
                byte[] decodedBytes = Convert.FromBase64String(request.EnterTicket);
                string decryptedTicket = Encoding.UTF8.GetString(decodedBytes);
                string[] enterTicket = decryptedTicket.Split('/');

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.PublisherAccountId == enterTicket[0]);

                if (user == null)
                {
                    // Create new user account
                    user = new Models.User
                    {
                        Nickname = "",
                        PublisherAccountId = enterTicket[0],
                        CreateDate = DateTime.UtcNow,
                        LastConnectTime = DateTime.UtcNow,
                        Level = 1,
                        IsNew = true
                    };
                    _dbContext.Users.Add(user);
                    await _dbContext.SaveChangesAsync();
                    
                    // Initialize complete account with all required game data
                    await AccountInitializationService.InitializeCompleteAccount(_dbContext, user);
                }
                else
                {
                    user.LastConnectTime = DateTime.UtcNow;
                    
                    if (user.CreateDate == DateTime.MinValue || user.CreateDate.Year < 2000)
                    {
                        user.CreateDate = DateTime.UtcNow;
                    }
                    
                    await _dbContext.SaveChangesAsync();
                }

                var session = new SessionKey
                {
                    AccountServerId = user.Id,
                    MxToken = Guid.NewGuid().ToString()
                };

                return new AccountCheckNexonResponse
                {
                    ResultState = 1,
                    AccountId = user.Id,
                    SessionKey = session
                };
            }
        }

        /// <summary>
        /// Handles authentication and returns account data
        /// Protocol: Account_Auth
        /// </summary>
        [ProtocolHandler(Protocol.Account_Auth)]
        public class Auth : BaseHandler<AccountAuthRequest, AccountAuthResponse>
        {
            private readonly BAContext _dbContext;

            public Auth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountAuthResponse> Handle(AccountAuthRequest request)
            {
                if (request.SessionKey == null)
                    throw new Exception("Session key is required");

                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                // Get mission progress if available
                var missionProgressList = new List<MissionProgressDB>();
                // TODO: Load from database once MissionProgress table exists

                return new AccountAuthResponse
                {
                    CurrentVersion = request.Version,
                    MinimumVersion = 0,
                    IsDevelopment = false,
                    BattleValidation = false,
                    UpdateRequired = false,
                    TTSCdnUri = "https://ba.dn.nexoncdn.co.kr/tts/version2/",
                    AccountDB = new AccountDB
                    {
                        ServerId = user?.Id ?? request.SessionKey.AccountServerId,
                        Nickname = user?.Nickname ?? "",
                        // CallName, CallNameKatakana, Comment - omit to match Atrahasis (will be null/default)
                        State = NetworkModels.AccountState.Normal,
                        Level = user?.Level ?? 1,
                        Exp = 0,
                        RepresentCharacterServerId = user?.RepresentCharacterServerId ?? 9,
                        PublisherAccountId = 0,  // Match Atrahasis - always 0
                        LastConnectTime = user?.LastConnectTime ?? DateTime.UtcNow,
                        CreateDate = user?.CreateDate ?? DateTime.UtcNow,
                        BirthDay = DateTime.MinValue,
                        CallNameUpdateTime = DateTime.Parse("2025-10-20T00:00:00"),  // Match Atrahasis format
                    },
                    AttendanceBookRewards = new List<AttendanceBookReward>(),
                    AttendanceHistoryDBs = new List<AttendanceHistoryDB>(),
                    RepurchasableMonthlyProductCountDBs = new List<PurchaseCountDB>(),
                    MonthlyProductParcel = new List<ParcelInfo>(),
                    MonthlyProductMail = new List<ParcelInfo>(),
                    BiweeklyProductParcel = new List<ParcelInfo>(),
                    BiweeklyProductMail = new List<ParcelInfo>(),
                    WeeklyProductParcel = new List<ParcelInfo>(),
                    WeeklyProductMail = new List<ParcelInfo>(),
                    EncryptedUID = string.Empty,
                    AccountRestrictionsDB = new AccountRestrictionsDB(),
                    IssueAlertInfos = new List<IssueAlertInfoDB>(),
                    MissionProgressDBs = missionProgressList,
                    // Use string keys (enum names) with integer 0 values to match Atrahasis
                    StaticOpenConditions = Enum.GetValues(typeof(NetworkModels.OpenConditionContent))
                        .Cast<NetworkModels.OpenConditionContent>()
                        .ToDictionary(key => key.ToString(), key => 0)
                };
            }
        }

        /// <summary>
        /// Creates and initializes a new game account with starter data
        /// Protocol: Account_Create
        /// </summary>
        [ProtocolHandler(Protocol.Account_Create)]
        public class Create : BaseHandler<AccountCreateRequest, AccountCreateResponse>
        {
            private readonly BAContext _dbContext;

            public Create(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountCreateResponse> Handle(AccountCreateRequest request)
            {
                if (request.SessionKey == null)
                    throw new Exception("Session key is required");

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // TODO: Load from DefaultCharacterExcel instead of hardcoding
                // For now, give starter characters (Aru, Haruka, Shiroko)
                var starterCharIds = new[] { 10000L, 10015L, 10010L };

                foreach (var charId in starterCharIds)
                {
                    var character = new Models.Character
                    {
                        AccountServerId = user.Id,
                        UniqueId = charId,
                        StarGrade = 3,
                        Level = 1,
                        Exp = 0,
                        FavorRank = 1,
                        FavorExp = 0,
                        PublicSkillLevel = 1,
                        ExSkillLevel = 1,
                        PassiveSkillLevel = 1,
                        ExtraPassiveSkillLevel = 1,
                        LeaderSkillLevel = 1,
                        IsNew = true,
                        IsLocked = false,
                        IsFavorite = false,
                        EquipmentServerIds = "[0,0,0]",
                        PotentialStats = "{\"1\":0,\"2\":0,\"3\":0}"
                    };
                    _dbContext.Characters.Add(character);
                }

                await _dbContext.SaveChangesAsync();

                // Set representative character
                var firstChar = await _dbContext.Characters
                    .FirstOrDefaultAsync(c => c.AccountServerId == user.Id);
                if (firstChar != null)
                {
                    user.RepresentCharacterServerId = (int)firstChar.ServerId;
                    await _dbContext.SaveChangesAsync();
                }

                return new AccountCreateResponse
                {
                    SessionKey = request.SessionKey
                };
            }
        }

        /// <summary>
        /// Sets player nickname
        /// Protocol: Account_Nickname
        /// </summary>
        [ProtocolHandler(Protocol.Account_Nickname)]
        public class Nickname : BaseHandler<AccountNicknameRequest, AccountNicknameResponse>
        {
            private readonly BAContext _dbContext;

            public Nickname(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountNicknameResponse> Handle(AccountNicknameRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user != null)
                {
                    user.Nickname = request.Nickname;
                    user.CallName = request.Nickname;
                    await _dbContext.SaveChangesAsync();
                }

                return new AccountNicknameResponse
                {
                    AccountDB = new AccountDB
                    {
                        ServerId = user?.Id ?? request.SessionKey.AccountServerId,
                        Nickname = request.Nickname,
                        CallName = request.Nickname,
                        State = NetworkModels.AccountState.Normal,
                        Level = user?.Level ?? 1,
                        Exp = 0,
                        Comment = "",
                        RepresentCharacterServerId = user?.RepresentCharacterServerId ?? 9,
                        PublisherAccountId = user != null ? long.Parse(user.PublisherAccountId) : 0,
                        LastConnectTime = user?.LastConnectTime ?? DateTime.UtcNow,
                        CreateDate = user?.CreateDate ?? DateTime.UtcNow,
                        BirthDay = DateTime.MinValue,
                        CallNameUpdateTime = DateTime.UtcNow,
                    }
                };
            }
        }

        /// <summary>
        /// Sets player call name (katakana name)
        /// Protocol: Account_CallName
        /// </summary>
        [ProtocolHandler(Protocol.Account_CallName)]
        public class CallName : BaseHandler<AccountCallNameRequest, AccountCallNameResponse>
        {
            private readonly BAContext _dbContext;

            public CallName(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountCallNameResponse> Handle(AccountCallNameRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user != null)
                {
                    user.CallName = request.CallName ?? request.CallNameKatakana ?? "";
                    user.CallNameKatakana = request.CallNameKatakana;
                    await _dbContext.SaveChangesAsync();
                }

                return new AccountCallNameResponse
                {
                    AccountDB = new AccountDB
                    {
                        ServerId = user?.Id ?? request.SessionKey.AccountServerId,
                        Nickname = user?.Nickname ?? "",
                        CallName = request.CallNameKatakana ?? request.CallName ?? "",
                        CallNameKatakana = request.CallNameKatakana,
                        State = NetworkModels.AccountState.Normal,
                        Level = user?.Level ?? 1,
                        Exp = 0,
                        Comment = "",
                        RepresentCharacterServerId = user?.RepresentCharacterServerId ?? 9,
                        PublisherAccountId = user != null ? long.Parse(user.PublisherAccountId) : 0,
                        LastConnectTime = user?.LastConnectTime ?? DateTime.UtcNow,
                        CreateDate = user?.CreateDate ?? DateTime.UtcNow,
                        BirthDay = DateTime.MinValue,
                        CallNameUpdateTime = DateTime.UtcNow,
                    }
                };
            }
        }

        /// <summary>
        /// Syncs all game data on login
        /// Protocol: Account_LoginSync
        /// </summary>
        [ProtocolHandler(Protocol.Account_LoginSync)]
        public class LoginSync : BaseHandler<AccountLoginSyncRequest, AccountLoginSyncResponse>
        {
            private readonly BAContext _dbContext;
            private readonly SessionKeyService sessionKeyService;
            private readonly ExcelTableService excelTableService;
            private readonly CafeService cafeService;
            private readonly object mapper = null; // Compatibility placeholder for Atrahasis mapper parameter

            public LoginSync(BAContext dbContext, SessionKeyService sessionKeyService, ExcelTableService excelTableService, CafeService cafeService)
            {
                _dbContext = dbContext;
                this.sessionKeyService = sessionKeyService;
                this.excelTableService = excelTableService;
                this.cafeService = cafeService;
            }

            protected override async Task<AccountLoginSyncResponse> Handle(AccountLoginSyncRequest req)
            {
                var account = sessionKeyService.GetAccount(_dbContext, req.SessionKey);
                ArgumentNullException.ThrowIfNull(account);

                // Set context for mappings that need it (e.g., Cafe mapping for server time and production)
                MappingExtensions.SetContext(_dbContext, cafeService);

                // Capture server time once for consistent timestamps across all sub-responses
                var serverTimeTicks = account.GameSettings.ServerDateTimeTicks();

                var res = new AccountLoginSyncResponse();
                
                res.CafeGetInfoResponse = new CafeGetInfoResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    CafeDBs = _dbContext.GetAccountCafes(account.Id).ToMapList<Models.Cafe, CafeDB>(mapper),
                    FurnitureDBs = _dbContext.GetAccountFurnitures(account.Id).ToMapList<Models.Furniture, FurnitureDB>(mapper)
                };
                res.AccountCurrencySyncResponse = new AccountCurrencySyncResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    AccountCurrencyDB = _dbContext.GetAccountCurrencies(account.Id).FirstOrDefaultMapTo<Models.AccountCurrency, AccountCurrencyDB>(mapper)
                };
                res.CharacterListResponse = new CharacterListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    CharacterDBs = _dbContext.GetAccountCharacters(account.Id).ToMapList<Models.Character, CharacterDB>(mapper),
                    TSSCharacterDBs = new List<CharacterDB>(),
                    WeaponDBs = _dbContext.GetAccountWeapons(account.Id).ToMapList<Models.Weapon, WeaponDB>(mapper),
                    CostumeDBs = _dbContext.GetAccountCostumes(account.Id).ToMapList<Models.Costume, CostumeDB>(mapper)
                };
                res.ItemListResponse = new ItemListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    ItemDBs = _dbContext.GetAccountItems(account.Id).ToMapList<Models.Item, ItemDB>(mapper)
                };
                res.EquipmentItemListResponse = new EquipmentItemListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    EquipmentDBs = _dbContext.GetAccountEquipments(account.Id).ToMapList<Models.Equipment, EquipmentDB>(mapper)
                };
                res.CharacterGearListResponse = new CharacterGearListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    GearDBs = _dbContext.GetAccountGears(account.Id).ToMapList<Models.Gear, GearDB>(mapper)
                };
                res.EchelonListResponse = new EchelonListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    EchelonDBs = _dbContext.GetAccountEchelons(account.Id).ToMapList<Models.Echelon, EchelonDB>(mapper)
                };
                res.MemoryLobbyListResponse = new MemoryLobbyListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    MemoryLobbyDBs = _dbContext.GetAccountMemoryLobbies(account.Id).ToMapList<Models.MemoryLobby, MemoryLobbyDB>(mapper)
                };
                res.CampaignListResponse = new CampaignListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    CampaignChapterClearRewardHistoryDBs = _dbContext.GetAccountCampaignChapterClearRewardHistories(account.Id).ToMapList<Models.CampaignChapterReward, CampaignChapterClearRewardHistoryDB>(mapper),
                    StageHistoryDBs = _dbContext.GetAccountCampaignStageHistories(account.Id).GetNormalCampaignStageHistories().ToMapList<Models.CampaignStageHistory, CampaignStageHistoryDB>(mapper),
                    StrategyObjecthistoryDBs = _dbContext.GetAccountStrategyObjectHistories(account.Id).ToMapList<Models.StrategyObjectHistory, StrategyObjectHistoryDB>(mapper)
                };
                res.ArenaLoginResponse = new ArenaLoginResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    ArenaPlayerInfoDB = new ArenaPlayerInfoDB
                    {
                        CurrentSeasonId = account.ContentInfo.ArenaDataInfo.SeasonId,
                        CurrentRank = 1,
                        TimeRewardLastUpdateTime = account.GameSettings.ServerDateTime()
                    }
                };
                res.RaidLoginResponse = new RaidLoginResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    SeasonType = NetworkModels.RaidSeasonType.Open,
                };
                res.EliminateRaidLoginResponse = new EliminateRaidLoginResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    SeasonType = NetworkModels.RaidSeasonType.Open,
                    SweepPointByRaidUniqueId = new Dictionary<long, long>()
                };
                res.CraftInfoListResponse = new CraftInfoListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    CraftInfos = new List<CraftInfoDB>(),
                    ShiftingCraftInfos = new List<ShiftingCraftInfoDB>()
                };
                res.ClanLoginResponse = new ClanLoginResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    AccountClanMemberDB = new ClanMemberDB { AccountId = account.Id }
                };
                var MTOutlines = _dbContext.GetAccountMomoTalkOutLines(account.Id).ToList();
                res.MomotalkOutlineResponse = new MomoTalkOutLineResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    FavorScheduleRecords = MomoTalkService.GetAllFavorSchedules(MTOutlines),
                    MomoTalkOutLineDBs = _dbContext.GetAccountMomoTalkOutLines(account.Id).ToMapList<Models.MomoTalkOutline, MomoTalkOutLineDB>(mapper)
                };
                res.ScenarioListResponse = new ScenarioListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    ScenarioHistoryDBs = _dbContext.GetAccountScenarioHistories(account.Id).ToMapList<Models.ScenarioHistory, ScenarioHistoryDB>(mapper),
                    ScenarioGroupHistoryDBs = _dbContext.GetAccountScenarioGroupHistories(account.Id).ToMapList<Models.ScenarioGroupHistory, ScenarioGroupHistoryDB>(mapper)
                };
                res.ShopGachaRecruitListResponse = new ShopGachaRecruitListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    ShopFreeRecruitHistoryDBs = new List<ShopFreeRecruitHistoryDB>()
                };
                res.TimeAttackDungeonLoginResponse = new TimeAttackDungeonLoginResponse()
                {
                    ServerTimeTicks = serverTimeTicks
                };
                res.BillingPurchaseListByNexonResponse = new BillingPurchaseListByNexonResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    CountList = new List<PurchaseCountDB>(),
                    OrderList = new List<PurchaseOrderDB>(),
                    MonthlyProductList = new List<MonthlyProductPurchaseDB>(),
                    BlockedProductDBs = new List<BlockedProductDB>(),
                    GachaTicketItemIdList = new List<long>(),
                    ProductMonthlyIdInMailList = new List<long>()
                };
                res.EventContentPermanentListResponse = new EventContentPermanentListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    PermanentDBs = _dbContext.GetAccountEventContentPermanents(account.Id).ToMapList<Models.EventContentPermanent, EventContentPermanentDB>(mapper).ToList()
                };
                res.AttachmentGetResponse = new AttachmentGetResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    AccountAttachmentDB = _dbContext.AccountAttachments.Where(x => x.AccountServerId == account.Id).FirstOrDefaultMapTo<Models.AccountAttachment, AccountAttachmentDB>(mapper)
                };
                res.AttachmentEmblemListResponse = new AttachmentEmblemListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    EmblemDBs = _dbContext.GetAccountEmblems(account.Id).ToMapList<Models.Emblem, EmblemDB>(mapper)
                };
                res.ContentSweepMultiSweepPresetListResponse = new ContentSweepMultiSweepPresetListResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    MultiSweepPresetDBs = new List<MultiSweepPresetDB>()
                };
                res.StickerListResponse = new StickerLoginResponse()
                {
                    ServerTimeTicks = serverTimeTicks,
                    StickerBookDB = _dbContext.StickerBooks.Where(x => x.AccountServerId == account.Id).FirstOrDefaultMapTo<Models.StickerBook, StickerBookDB>(mapper)
                };
                res.MultiFloorRaidSyncResponse = new MultiFloorRaidSyncResponse()
                {
                    MultiFloorRaidDBs = _dbContext.GetAccountMultiFloorRaids(account.Id).ToMapList<Models.MultiFloorRaid, MultiFloorRaidDB>(mapper),
                    ServerTimeTicks = account.GameSettings.EnableMultiFloorRaid
                        ? MultiFloorRaidSeasonLoader.GetSeasonServerTimeTicks(
                            (int)account.ContentInfo.MultiFloorRaidDataInfo.SeasonId,
                            account.GameSettings.ServerDateTime())
                        : serverTimeTicks
                };
                res.AccountLevelRewardIds = _dbContext.GetAccountLevelRewards(account.Id).Select(x => x.RewardId).ToList();
                res.FriendCount = 0;
                res.FriendCode = "SUS";  // Match Atrahasis friend code
                // Use string keys (enum names) with integer 0 values to match Atrahasis
                res.StaticOpenConditions = Enum.GetValues(typeof(NetworkModels.OpenConditionContent))
                    .Cast<NetworkModels.OpenConditionContent>()
                    .ToDictionary(key => key.ToString(), key => 0);

                // Set top-level ServerTimeTicks and AccountId to match Atrahasis
                res.ServerTimeTicks = serverTimeTicks;
                res.AccountId = account.Id;

                return res;
            }
        }

        /// <summary>
        /// Gets tutorial completion status
        /// Protocol: Account_GetTutorial
        /// </summary>
        [ProtocolHandler(Protocol.Account_GetTutorial)]
        public class GetTutorial : BaseHandler<AccountGetTutorialRequest, AccountGetTutorialResponse>
        {
            private readonly BAContext _dbContext;

            public GetTutorial(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountGetTutorialResponse> Handle(AccountGetTutorialRequest request)
            {
                var tutorial = await _dbContext.AccountTutorials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.AccountServerId == request.SessionKey.AccountServerId);

                List<long> tutorialIds;
                if (tutorial != null && !string.IsNullOrEmpty(tutorial.TutorialIds))
                {
                    tutorialIds = JsonSerializer.Deserialize<List<long>>(tutorial.TutorialIds) 
                        ?? Enumerable.Range(1, 27).Select(i => (long)i).ToList();
                }
                else
                {
                    // Default: all tutorials completed
                    tutorialIds = Enumerable.Range(1, 27).Select(i => (long)i).ToList();
                }

                return new AccountGetTutorialResponse
                {
                    TutorialIds = tutorialIds
                };
            }
        }

        /// <summary>
        /// Updates tutorial completion status
        /// Protocol: Account_SetTutorial
        /// </summary>
        [ProtocolHandler(Protocol.Account_SetTutorial)]
        public class SetTutorial : BaseHandler<AccountSetTutorialRequest, AccountSetTutorialResponse>
        {
            private readonly BAContext _dbContext;

            public SetTutorial(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountSetTutorialResponse> Handle(AccountSetTutorialRequest request)
            {
                var tutorial = await _dbContext.AccountTutorials
                    .FirstOrDefaultAsync(t => t.AccountServerId == request.SessionKey.AccountServerId);

                var tutorialIdsJson = JsonSerializer.Serialize(request.TutorialIds);

                if (tutorial == null)
                {
                    tutorial = new AccountTutorial
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        TutorialIds = tutorialIdsJson
                    };
                    _dbContext.AccountTutorials.Add(tutorial);
                }
                else
                {
                    tutorial.TutorialIds = tutorialIdsJson;
                }

                await _dbContext.SaveChangesAsync();

                return new AccountSetTutorialResponse();
            }
        }

        /// <summary>
        /// Sets representative character and profile comment
        /// Protocol: Account_SetRepresentCharacterAndComment
        /// </summary>
        [ProtocolHandler(Protocol.Account_SetRepresentCharacterAndComment)]
        public class SetRepresentCharacterAndComment : BaseHandler<AccountSetRepresentCharacterAndCommentRequest, AccountSetRepresentCharacterAndCommentResponse>
        {
            private readonly BAContext _dbContext;

            public SetRepresentCharacterAndComment(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AccountSetRepresentCharacterAndCommentResponse> Handle(AccountSetRepresentCharacterAndCommentRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                if (request.RepresentCharacterServerId != 0)
                    user.RepresentCharacterServerId = request.RepresentCharacterServerId;
                
                // Note: Comment field doesn't exist in User model yet, would need to add it

                await _dbContext.SaveChangesAsync();

                // Get the representative character
                var representChar = await _dbContext.Characters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ServerId == request.RepresentCharacterServerId);

                return new AccountSetRepresentCharacterAndCommentResponse
                {
                    AccountDB = new AccountDB
                    {
                        ServerId = user.Id,
                        Nickname = user.Nickname,
                        CallName = user.CallName,
                        CallNameKatakana = user.CallNameKatakana,
                        State = NetworkModels.AccountState.Normal,
                        Level = user.Level,
                        Exp = 0,
                        Comment = request.Comment ?? "",
                        RepresentCharacterServerId = user.RepresentCharacterServerId,
                        PublisherAccountId = long.Parse(user.PublisherAccountId),
                        LastConnectTime = user.LastConnectTime,
                        CreateDate = user.CreateDate,
                        BirthDay = DateTime.MinValue,
                        CallNameUpdateTime = DateTime.UtcNow
                    },
                    RepresentCharacterDB = representChar != null ? new CharacterDB
                    {
                        ServerId = representChar.ServerId,
                        UniqueId = representChar.UniqueId,
                        StarGrade = representChar.StarGrade,
                        Level = representChar.Level,
                        Exp = representChar.Exp,
                        FavorRank = representChar.FavorRank,
                        FavorExp = representChar.FavorExp,
                        PublicSkillLevel = representChar.PublicSkillLevel,
                        ExSkillLevel = representChar.ExSkillLevel,
                        PassiveSkillLevel = representChar.PassiveSkillLevel,
                        ExtraPassiveSkillLevel = representChar.ExtraPassiveSkillLevel,
                        LeaderSkillLevel = representChar.LeaderSkillLevel,
                        IsNew = representChar.IsNew,
                        IsLocked = representChar.IsLocked,
                        IsFavorite = representChar.IsFavorite,
                        EquipmentServerIds = !string.IsNullOrEmpty(representChar.EquipmentServerIds)
                            ? JsonSerializer.Deserialize<List<long>>(representChar.EquipmentServerIds) ?? new List<long>()
                            : new List<long>()
                    } : new CharacterDB()
                };
            }
        }

        /// <summary>
        /// Checks which account level rewards can be claimed
        /// Protocol: Account_CheckAccountLevelReward
        /// </summary>
        [ProtocolHandler(Protocol.Account_CheckAccountLevelReward)]
        public class CheckAccountLevelReward : BaseHandler<CheckAccountLevelRewardRequest, CheckAccountLevelRewardResponse>
        {
            private readonly BAContext _dbContext;

            public CheckAccountLevelReward(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CheckAccountLevelRewardResponse> Handle(CheckAccountLevelRewardRequest request)
            {
                var rewardIds = await _dbContext.AccountLevelRewards
                    .AsNoTracking()
                    .Where(r => r.AccountServerId == request.SessionKey.AccountServerId)
                    .Select(r => r.RewardId)
                    .ToListAsync();

                return new CheckAccountLevelRewardResponse
                {
                    AccountLevelRewardIds = rewardIds
                };
            }
        }

        /// <summary>
        /// Claims account level rewards
        /// Protocol: Account_ReceiveAccountLevelReward
        /// </summary>
        [ProtocolHandler(Protocol.Account_ReceiveAccountLevelReward)]
        public class ReceiveAccountLevelReward : BaseHandler<ReceiveAccountLevelRewardRequest, ReceiveAccountLevelRewardResponse>
        {
            private readonly BAContext _dbContext;

            public ReceiveAccountLevelReward(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<ReceiveAccountLevelRewardResponse> Handle(ReceiveAccountLevelRewardRequest request)
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var receivedRewardIds = await _dbContext.AccountLevelRewards
                    .Where(r => r.AccountServerId == user.Id)
                    .Select(r => r.RewardId)
                    .ToListAsync();

                // TODO: Load from AccountLevelRewardExcel to determine available rewards
                // For now, just return empty since we don't have Excel table access here
                var rewardsToGive = new List<long>();

                // Add rewards to database
                foreach (var rewardId in rewardsToGive)
                {
                    _dbContext.AccountLevelRewards.Add(new AccountLevelReward
                    {
                        AccountServerId = user.Id,
                        RewardId = rewardId
                    });
                }

                await _dbContext.SaveChangesAsync();

                return new ReceiveAccountLevelRewardResponse
                {
                    ReceivedAccountLevelRewardIds = rewardsToGive,
                    ParcelResultDB = new ParcelResultDB
                    {
                        // TODO: Build parcel result from rewards
                    }
                };
            }
        }

        /// <summary>
        /// Stub handler for account link rewards
        /// Protocol: Account_LinkReward
        /// </summary>
        [ProtocolHandler(Protocol.Account_LinkReward)]
        public class LinkReward : BaseHandler<AccountLinkRewardRequest, AccountLinkRewardResponse>
        {
            protected override async Task<AccountLinkRewardResponse> Handle(AccountLinkRewardRequest request)
            {
                return new AccountLinkRewardResponse();
            }
        }

        /// <summary>
        /// Stub handler for invalidating session tokens
        /// Protocol: Account_InvalidateToken
        /// </summary>
        [ProtocolHandler(Protocol.Account_InvalidateToken)]
        public class InvalidateToken : BaseHandler<AccountInvalidateTokenRequest, AccountInvalidateTokenResponse>
        {
            protected override async Task<AccountInvalidateTokenResponse> Handle(AccountInvalidateTokenRequest request)
            {
                return new AccountInvalidateTokenResponse();
            }
        }
    }
}
