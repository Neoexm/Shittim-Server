namespace BlueArchiveAPI.NetworkModels;
using System.Collections.ObjectModel;

public class RequestPacket : BasePacket
{
    public int ClientUpTime;
    public bool Resendable;
    public long Hash;
    public DateTime? ModifiedServerTime__DebugOnly;
}

public class ResponsePacket : BasePacket
{
    public long ServerTimeTicks;
    public ServerNotificationFlag ServerNotification;
    public List<MissionProgressDB> MissionProgressDBs;
    public Dictionary<long, List<MissionProgressDB>> EventMissionProgressDBDict;
    public Dictionary<OpenConditionContent, OpenConditionLockReason> StaticOpenConditions;
}

public class SessionDB
{
    public SessionKey SessionKey;
    public DateTime LastConnect;
    public int ConnectionTime;
}

public class AccountDB
{
    public long ServerId;
    public string Nickname;
    public string CallName;
    public string CallNameKatakana;
    public string DevId;
    public AccountState State;
    public int Level;
    public long Exp;
    public string Comment;
    public int LobbyMode;
    public int RepresentCharacterServerId;
    public long MemoryLobbyUniqueId;
    public DateTime LastConnectTime;
    public DateTime BirthDay;
    public DateTime CallNameUpdateTime;
    public long PublisherAccountId;
    public Int32? RetentionDays;
    public Int32? VIPLevel;
    public DateTime CreateDate;
    public Int32? UnReadMailCount;
    public DateTime? LinkRewardDate;
}

public class AttendanceBookReward
{
    public long UniqueId;
    public AttendanceType Type;
    public AccountState AccountType;
    public long DisplayOrder;
    public long AccountLevelLimit;
    public string Title;
    public string TitleImagePath;
    public AttendanceCountRule CountRule;
    public AttendanceResetType CountReset;
    public long BookSize;
    public DateTime StartDate;
    public DateTime StartableEndDate;
    public DateTime EndDate;
    public long ExpiryDate;
    public MailType MailType;
    public Dictionary<long, string> DailyRewardIcons;
    public Dictionary<long, List<ParcelInfo>> DailyRewards;
}

public class AttendanceHistoryDB
{
    public long ServerId;
    public long AccountServerId;
    public long AttendanceBookUniqueId;
    public Dictionary<long, DateTime> AttendedDay;
    public bool Expired;
}

public class OpenConditionDB
{
    public OpenConditionContent ContentType;
    public bool HideWhenLocked;
    public long AccountLevel;
    public long ScenarioModeId;
    public long CampaignStageUniqueId;
    public MultipleConditionCheckType MultipleConditionCheckType;
    public WeekDay OpenDayOfWeek;
    public long OpenHour;
    public WeekDay CloseDayOfWeek;
    public long CloseHour;
    public long CafeRank;
}

public class PurchaseCountDB
{
    public long ShopCashId;
    public int PurchaseCount;
    public DateTime ResetDate;
    public DateTime? PurchaseDate;
}

public class ParcelInfo
{
    public ParcelKeyPair Key;
    public long Amount;
    public BasisPoint Multiplier;
    public BasisPoint Probability;
}

public class AccountBanByNexonDB
{
    public long AccountId;
    public long Npsn;
    public long AccountBanId;
    public int BanType;
    public int BanDay;
    public DateTime BanStartDate;
    public DateTime BanEndDate;
    public string BanComment;
    public int DeleteFlag;
}

public class AccountCurrencyDB
{
    public long AccountLevel;
    public long AcademyLocationRankSum;
    public Dictionary<CurrencyTypes, long> CurrencyDict;
    public Dictionary<CurrencyTypes, DateTime> UpdateTimeDict;
}

public class CharacterDB : ParcelBase
{
    public ParcelType Type;
    public long ServerId;
    public long UniqueId;
    public int StarGrade;
    public int Level;
    public long Exp;
    public int FavorRank;
    public long FavorExp;
    public int PublicSkillLevel;
    public int ExSkillLevel;
    public int PassiveSkillLevel;
    public int ExtraPassiveSkillLevel;
    public int LeaderSkillLevel;
    public bool IsNew;
    public bool IsLocked;
    public bool IsFavorite;
    public List<long> EquipmentServerIds;
}

public enum Protocol
{
    None = 0,
    System_Version = 1,
    Session_Info = 2,
    NetworkTime_Sync = 3,
    NetworkTime_SyncReply = 4,
    Audit_GachaStatistics = 5,
    Account_Create = 1000,
    Account_Nickname = 1001,
    Account_Auth = 1002,
    Account_CurrencySync = 1003,
    Account_SetRepresentCharacterAndComment = 1004,
    Account_GetTutorial = 1005,
    Account_SetTutorial = 1006,
    Account_PassCheck = 1007,
    Account_VerifyForYostar = 1008,
    Account_CheckYostar = 1009,
    Account_CallName = 1010,
    Account_BirthDay = 1011,
    Account_Auth2 = 1012,
    Account_LinkReward = 1013,
    Account_CheckNexon = 1014,
    Account_DetachNexon = 1015,
    Account_ReportXignCodeCheater = 1016,
    Account_DismissRepurchasablePopup = 1017,
    Account_InvalidateToken = 1018,
    Account_LoginSync = 1019,
    Account_VerifyCheckAdultAgree = 1020,
    Account_SetCheckAdultAgree = 1021,
    Character_List = 2000,
    Character_Transcendence = 2001,
    Character_ExpGrowth = 2002,
    Character_FavorGrowth = 2003,
    Character_UpdateSkillLevel = 2004,
    Character_UnlockWeapon = 2005,
    Character_WeaponExpGrowth = 2006,
    Character_WeaponTranscendence = 2007,
    Character_SetFavorites = 2008,
    Equipment_List = 3000,
    Equipment_Sell = 3001,
    Equipment_Equip = 3002,
    Equipment_LevelUp = 3003,
    Equipment_TierUp = 3004,
    Equipment_Lock = 3005,
    Equipment_BatchGrowth = 3006,
    Item_List = 4000,
    Item_Sell = 4001,
    Item_Consume = 4002,
    Item_Lock = 4003,
    Item_BulkConsume = 4004,
    Item_SelectTicket = 4005,
    Echelon_List = 5000,
    Echelon_Save = 5001,
    Echelon_PresetList = 5002,
    Echelon_PresetSave = 5003,
    Echelon_PresetGroupRename = 5004,
    Campaign_List = 6000,
    Campaign_EnterMainStage = 6001,
    Campaign_ConfirmMainStage = 6002,
    Campaign_DeployEchelon = 6003,
    Campaign_WithdrawEchelon = 6004,
    Campaign_MapMove = 6005,
    Campaign_EndTurn = 6006,
    Campaign_EnterTactic = 6007,
    Campaign_TacticResult = 6008,
    Campaign_Retreat = 6009,
    Campaign_ChapterClearReward = 6010,
    Campaign_Heal = 6011,
    Campaign_EnterSubStage = 6012,
    Campaign_SubStageResult = 6013,
    Campaign_Portal = 6014,
    Campaign_ConfirmTutorialStage = 6015,
    Campaign_PurchasePlayCountHardStage = 6016,
    Campaign_EnterTutorialStage = 6017,
    Campaign_TutorialStageResult = 6018,
    Campaign_RestartMainStage = 6019,
    Mail_List = 7000,
    Mail_Check = 7001,
    Mail_Receive = 7002,
    Mission_List = 8000,
    Mission_Reward = 8001,
    Mission_MultipleReward = 8002,
    Mission_GuideReward = 8003,
    Mission_MultipleGuideReward = 8004,
    Mission_Sync = 8005,
    Attendance_List = 9000,
    Attendance_Check = 9001,
    Attendance_Reward = 9002,
    Shop_BuyMerchandise = 10000,
    Shop_BuyGacha = 10001,
    Shop_List = 10002,
    Shop_Refresh = 10003,
    Shop_BuyEligma = 10004,
    Shop_BuyGacha2 = 10005,
    Shop_GachaRecruitList = 10006,
    Shop_BuyRefreshMerchandise = 10007,
    Shop_BuyGacha3 = 10008,
    Shop_BuyAP = 10009,
    Recipe_Craft = 11000,
    MemoryLobby_List = 12000,
    MemoryLobby_SetMain = 12001,
    MemoryLobby_UpdateLobbyMode = 12002,
    MemoryLobby_Interact = 12003,
    CumulativeTimeReward_List = 13000,
    CumulativeTimeReward_Reward = 13001,
    OpenCondition_List = 15000,
    OpenCondition_Set = 15001,
    OpenCondition_EventList = 15002,
    Toast_List = 16000,
    Raid_List = 17000,
    Raid_CompleteList = 17001,
    Raid_Detail = 17002,
    Raid_Search = 17003,
    Raid_CreateBattle = 17004,
    Raid_EnterBattle = 17005,
    Raid_BattleUpdate = 17006,
    Raid_EndBattle = 17007,
    Raid_Reward = 17008,
    Raid_RewardAll = 17009,
    Raid_Revive = 17010,
    Raid_Share = 17011,
    Raid_SeasonInfo = 17012,
    Raid_SeasonReward = 17013,
    Raid_Lobby = 17014,
    Raid_GiveUp = 17015,
    Raid_OpponentList = 17016,
    Raid_RankingReward = 17017,
    Raid_Login = 17018,
    Raid_Sweep = 17019,
    Raid_GetBestTeam = 17020,
    SkipHistory_List = 18000,
    SkipHistory_Save = 18001,
    Scenario_List = 19000,
    Scenario_Clear = 19001,
    Scenario_GroupHistoryUpdate = 19002,
    Scenario_Skip = 19003,
    Scenario_Select = 19004,
    Scenario_AccountStudentChange = 19005,
    Scenario_LobbyStudentChange = 19006,
    Scenario_SpecialLobbyChange = 19007,
    Scenario_Enter = 19008,
    Scenario_EnterMainStage = 19009,
    Scenario_ConfirmMainStage = 19010,
    Scenario_DeployEchelon = 19011,
    Scenario_WithdrawEchelon = 19012,
    Scenario_MapMove = 19013,
    Scenario_EndTurn = 19014,
    Scenario_EnterTactic = 19015,
    Scenario_TacticResult = 19016,
    Scenario_Retreat = 19017,
    Scenario_Portal = 19018,
    Scenario_RestartMainStage = 19019,
    Scenario_SkipMainStage = 19020,
    Cafe_Get = 20000,
    Cafe_Ack = 20001,
    Cafe_Deploy = 20002,
    Cafe_Relocate = 20003,
    Cafe_Remove = 20004,
    Cafe_RemoveAll = 20005,
    Cafe_Interact = 20006,
    Cafe_ListPreset = 20007,
    Cafe_RenamePreset = 20008,
    Cafe_ClearPreset = 20009,
    Cafe_UpdatePresetFurniture = 20010,
    Cafe_ApplyPreset = 20011,
    Cafe_RankUp = 20012,
    Cafe_ReceiveCurrency = 20013,
    Cafe_GiveGift = 20014,
    Cafe_SummonCharacter = 20015,
    Cafe_TrophyHistory = 20016,
    Craft_List = 21000,
    Craft_SelectNode = 21001,
    Craft_UpdateNodeLevel = 21002,
    Craft_BeginProcess = 21003,
    Craft_CompleteProcess = 21004,
    Craft_Reward = 21005,
    Craft_HistoryList = 21006,
    Craft_ShiftingBeginProcess = 21007,
    Craft_ShiftingCompleteProcess = 21008,
    Craft_ShiftingReward = 21009,
    Arena_EnterLobby = 22000,
    Arena_Login = 22001,
    Arena_SettingChange = 22002,
    Arena_OpponentList = 22003,
    Arena_EnterBattle = 22004,
    Arena_EnterBattlePart1 = 22005,
    Arena_EnterBattlePart2 = 22006,
    Arena_BattleResult = 22007,
    Arena_CumulativeTimeReward = 22008,
    Arena_DailyReward = 22009,
    Arena_RankList = 22010,
    Arena_History = 22011,
    Arena_RecordSync = 22012,
    Arena_TicketPurchase = 22013,
    Arena_DamageReport = 22014,
    Arena_CheckSeasonCloseReward = 22015,
    Arena_SyncEchelonSettingTime = 22016,
    WeekDungeon_List = 23000,
    WeekDungeon_EnterBattle = 23001,
    WeekDungeon_BattleResult = 23002,
    WeekDungeon_Retreat = 23003,
    Academy_GetInfo = 24000,
    Academy_AttendSchedule = 24001,
    Academy_AttendFavorSchedule = 24002,
    Event_GetList = 25000,
    Event_GetImage = 25001,
    Event_UseCoupon = 25002,
    Event_RewardIncrease = 25003,
    ContentSave_Get = 26000,
    ContentSave_Discard = 26001,
    ContentSweep_Request = 27000,
    Clan_Lobby = 28000,
    Clan_Login = 28001,
    Clan_Search = 28002,
    Clan_Create = 28003,
    Clan_Member = 28004,
    Clan_Applicant = 28005,
    Clan_Join = 28006,
    Clan_Quit = 28007,
    Clan_Permit = 28008,
    Clan_Kick = 28009,
    Clan_Setting = 28010,
    Clan_Confer = 28011,
    Clan_Dismiss = 28012,
    Clan_AutoJoin = 28013,
    Clan_MemberList = 28014,
    Clan_CancelApply = 28015,
    Clan_MyAssistList = 28016,
    Clan_SetAssist = 28017,
    Clan_ChatLog = 28018,
    Clan_Check = 28019,
    Clan_AllAssistList = 28020,
    Billing_TransactionStartByYostar = 29000,
    Billing_TransactionEndByYostar = 29001,
    Billing_PurchaseListByYostar = 29002,
    Billing_PurchaseCashShopVerifyByNexon = 29003,
    Billing_CheckConditionCashShopGoods = 29004,
    Billing_PurchaseListByNexon = 29005,
    Billing_ValidateByNexon = 29006,
    Billing_FinishByNexon = 29007,
    EventContent_AdventureList = 30000,
    EventContent_EnterMainStage = 30001,
    EventContent_ConfirmMainStage = 30002,
    EventContent_EnterTactic = 30003,
    EventContent_TacticResult = 30004,
    EventContent_EnterSubStage = 30005,
    EventContent_SubStageResult = 30006,
    EventContent_DeployEchelon = 30007,
    EventContent_WithdrawEchelon = 30008,
    EventContent_MapMove = 30009,
    EventContent_EndTurn = 30010,
    EventContent_Retreat = 30011,
    EventContent_Portal = 30012,
    EventContent_PurchasePlayCountHardStage = 30013,
    EventContent_ShopList = 30014,
    EventContent_ShopRefresh = 30015,
    EventContent_ReceiveStageTotalReward = 30016,
    EventContent_EnterMainGroundStage = 30017,
    EventContent_MainGroundStageResult = 30018,
    EventContent_ShopBuyMerchandise = 30019,
    EventContent_ShopBuyRefreshMerchandise = 30020,
    EventContent_SelectBuff = 30021,
    EventContent_BoxGachaShopList = 30022,
    EventContent_BoxGachaShopPurchase = 30023,
    EventContent_BoxGachaShopRefresh = 30024,
    EventContent_CollectionList = 30025,
    EventContent_CollectionForMission = 30026,
    EventContent_ScenarioGroupHistoryUpdate = 30027,
    EventContent_CardShopList = 30028,
    EventContent_CardShopShuffle = 30029,
    EventContent_CardShopPurchase = 30030,
    EventContent_RestartMainStage = 30031,
    EventContent_LocationGetInfo = 30032,
    EventContent_LocationAttendSchedule = 30033,
    EventContent_FortuneGachaPurchase = 30034,
    EventContent_SubEventLobby = 30035,
    EventContent_EnterStoryStage = 30036,
    EventContent_StoryStageResult = 30037,
    EventContent_DiceRaceLobby = 30038,
    EventContent_DiceRaceRoll = 30039,
    EventContent_DiceRaceLapReward = 30040,
    EventContent_PermanentList = 30041,
    TTS_GetFile = 31000,
    TTS_GetKana = 31001,
    ContentLog_UIOpenStatistics = 32000,
    MomoTalk_OutLine = 33000,
    MomoTalk_MessageList = 33001,
    MomoTalk_Read = 33002,
    MomoTalk_Reply = 33003,
    MomoTalk_FavorSchedule = 33004,
    ClearDeck_List = 34000,
    MiniGame_StageList = 35000,
    MiniGame_EnterStage = 35001,
    MiniGame_Result = 35002,
    MiniGame_MissionList = 35003,
    MiniGame_MissionReward = 35004,
    MiniGame_MissionMultipleReward = 35005,
    Notification_LobbyCheck = 36000,
    Notification_EventContentReddotCheck = 36001,
    ProofToken_RequestQuestion = 37000,
    ProofToken_Submit = 37001,
    SchoolDungeon_List = 38000,
    SchoolDungeon_EnterBattle = 38001,
    SchoolDungeon_BattleResult = 38002,
    SchoolDungeon_Retreat = 38003,
    TimeAttackDungeon_Lobby = 39000,
    TimeAttackDungeon_CreateBattle = 39001,
    TimeAttackDungeon_EnterBattle = 39002,
    TimeAttackDungeon_EndBattle = 39003,
    TimeAttackDungeon_Sweep = 39004,
    TimeAttackDungeon_GiveUp = 39005,
    TimeAttackDungeon_Login = 39006,
    WorldRaid_Lobby = 40000,
    WorldRaid_BossList = 40001,
    WorldRaid_EnterBattle = 40002,
    WorldRaid_BattleResult = 40003,
    WorldRaid_ReceiveReward = 40004,
    ResetableContent_Get = 41000,
    Conquest_GetInfo = 42000,
    Conquest_Conquer = 42001,
    Conquest_ConquerWithBattleStart = 42002,
    Conquest_ConquerWithBattleResult = 42003,
    Conquest_DeployEchelon = 42004,
    Conquest_ManageBase = 42005,
    Conquest_UpgradeBase = 42006,
    Conquest_TakeEventObject = 42007,
    Conquest_EventObjectBattleStart = 42008,
    Conquest_EventObjectBattleResult = 42009,
    Conquest_ReceiveCalculateRewards = 42010,
    Conquest_NormalizeEchelon = 42011,
    Conquest_Check = 42012,
    Friend_List = 43000,
    Friend_Remove = 43001,
    Friend_GetFriendDetailedInfo = 43002,
    Friend_GetIdCard = 43003,
    Friend_SetIdCard = 43004,
    Friend_Search = 43005,
    Friend_SendFriendRequest = 43006,
    Friend_AcceptFriendRequest = 43007,
    Friend_DeclineFriendRequest = 43008,
    Friend_CancelFriendRequest = 43009,
    Friend_Check = 43010,
    CharacterGear_List = 44000,
    CharacterGear_Unlock = 44001,
    CharacterGear_TierUp = 44002,
    Queuing_GetTicket = 50000,
    Queuing_GetTicketGL = 50001,
    Management_BannerList = 100000,
    Management_ContentsLockList = 100001,
    Common_Cheat = -9999,
    Error = -1,
}

public class EquipmentItemListResponse : ResponsePacket
{
    public Protocol Protocol;
    public List<EquipmentDB> EquipmentDBs;
}

public class CraftInfoListResponse : ResponsePacket
{
    public Protocol Protocol;
    public List<CraftInfoDB> CraftInfos;
    public List<ShiftingCraftInfoDB> ShiftingCraftInfos;
}

public class WeaponDB : ParcelBase
{
    public ParcelType Type;
    public long UniqueId;
    public int Level;
    public long Exp;
    public int StarGrade;
    public long BoundCharacterServerId;
    public bool IsLocked;
}

public class ParcelResultDB
{
    public AccountDB AccountDB;
    public List<AcademyLocationDB> AcademyLocationDBs;
    public AccountCurrencyDB AccountCurrencyDB;
    public List<CharacterDB> CharacterDBs;
    public List<WeaponDB> WeaponDBs;
    public List<CharacterDB> TSSCharacterDBs;
    public Dictionary<long, EquipmentDB> EquipmentDBs;
    public List<long> RemovedEquipmentIds;
    public Dictionary<long, ItemDB> ItemDBs;
    public List<long> RemovedItemIds;
    public Dictionary<long, FurnitureDB> FurnitureDBs;
    public List<long> RemovedFurnitureIds;
    public Dictionary<long, IdCardBackgroundDB> IdCardBackgroundDBs;
    public List<MemoryLobbyDB> MemoryLobbyDBs;
    public List<long> CharacterNewUniqueIds;
    public Dictionary<long, int> SecretStoneCharacterIdAndCounts;
    public List<ParcelInfo> DisplaySequence;
    public List<ParcelInfo> ParcelForMission;
    public List<ParcelResultStepInfo> ParcelResultStepInfoList;
    public long BaseAccountExp;
    public long AdditionalAccountExp;
}

public class ConsumeRequestDB
{
    public Dictionary<long, long> ConsumeServerIdAndCounts;
}

public class ConsumeResultDB
{
    public List<long> RemovedServerIds;
    public Dictionary<long, long> UsedServerIdAndRemainingCounts;
}

public class ItemDB : ConsumableItemBaseDB
{
    public ParcelType Type;
    public bool IsNew;
    public bool IsLocked;
}

public class EquipmentBatchGrowthRequestDB
{
    public long TargetServerId;
    public List<ConsumeRequestDB> ConsumeRequestDBs;
    public long AfterTier;
    public long AfterLevel;
    public long AfterExp;
}

public class EquipmentDB : ConsumableItemBaseDB
{
    public ParcelType Type;
    public int Level;
    public long Exp;
    public int Tier;
    public long BoundCharacterServerId;
    public bool IsNew;
    public bool IsLocked;
}

public class EchelonDB
{
    public long AccountServerId;
    public EchelonType EchelonType;
    public long EchelonNumber;
    public long LeaderServerId;
    public List<long> MainSlotServerIds;
    public List<long> SupportSlotServerIds;
    public long TSSServerId;
    public EchelonStatusFlag UsingFlag;
    public List<long> SkillCardMulliganCharacterIds;
}

public class ClanAssistUseInfo
{
    public long CharacterAccountId;
    public long CharacterDBId;
    public EchelonType EchelonType;
    public int SlotNumber;
    public AssistRelation AssistRelation;
    public int EchelonSlotType;
    public int EchelonSlotIndex;
    public bool IsMulligan;
}

public class EchelonPresetGroupDB
{
    public int GroupIndex;
    public string GroupLabel;
    public Dictionary<int, EchelonPresetDB> PresetDBs;
    public EchelonPresetDB Item;
}

public class EchelonPresetDB
{
    public int GroupIndex;
    public int Index;
    public string Label;
    public long LeaderUniqueId;
    public Int64[] StrikerUniqueIds;
    public Int64[] SpecialUniqueIds;
    public List<long> MulliganUniqueIds;
}

public class CampaignChapterClearRewardHistoryDB
{
    public long AccountServerId;
    public long ChapterUniqueId;
    public StageDifficulty RewardType;
    public DateTime ReceiveDate;
}

public class CampaignStageHistoryDB
{
    public long AccountServerId;
    public long StoryUniqueId;
    public long ChapterUniqueId;
    public long StageUniqueId;
    public long TacticClearCountWithRankSRecord;
    public long ClearTurnRecord;
    public bool Star1Flag;
    public bool Star2Flag;
    public bool Star3Flag;
    public DateTime LastPlay;
    public long TodayPlayCount;
    public long TodayPurchasePlayCountHardStage;
    public DateTime? FirstClearRewardReceive;
    public DateTime? StarRewardReceive;
    public long TodayPlayCountForUI;
}

public class StrategyObjectHistoryDB
{
    public long AccountId;
    public long StrategyObjectId;
}

public class DailyResetCountDB
{
    public long AccountServerId;
    public Dictionary<ResetContentType, long> ResetCount;
}

public class CampaignMainStageSaveDB : ContentSaveDB
{
    public ContentType ContentType;
    public CampaignState CampaignState;
    public int CurrentTurn;
    public int EnemyClearCount;
    public int LastEnemyEntityId;
    public int TacticRankSCount;
    public Dictionary<long, HexaUnit> EnemyInfos;
    public Dictionary<long, HexaUnit> EchelonInfos;
    public Dictionary<long, List<long>> WithdrawInfos;
    public Dictionary<long, Strategy> StrategyObjects;
    public Dictionary<long, List<ParcelInfo>> StrategyObjectRewards;
    public List<long> StrategyObjectHistory;
    public Dictionary<long, List<long>> ActivatedHexaEventsAndConditions;
    public Dictionary<long, List<long>> HexaEventDelayedExecutions;
    public Dictionary<int, HexaTileState> TileMapStates;
    public List<HexaDisplayInfo> DisplayInfos;
    public List<HexaUnit> DeployedEchelonInfos;
}

public class HexaUnit
{
    public long EntityId;
    public Dictionary<long, long> HpInfos;
    public Dictionary<long, long> DyingInfos;
    public Dictionary<long, int> BuffInfos;
    public int ActionCountMax;
    public int ActionCount;
    public int Mobility;
    public int StrategySightRange;
    public long Id;
    public Vector3 Rotate;
    public HexLocation Location;
    public HexLocation AIDestination;
    public bool IsActionComplete;
    public bool IsPlayer;
    public bool IsFixedEchelon;
    public int MovementOrder;
    public int MaxTSSCount;
    public int RemainTSSCount;
    public Dictionary<TacticEntityType, List<ParcelInfo>> RewardParcelInfosWithDropTacticEntityType;
    public SkillCardHand SkillCardHand;
    public bool PlayAnimation;
}

public struct HexLocation
{
    public int x;
    public int y;
    public int z;
}

public class Strategy
{
    public long EntityId;
    public Vector3 Rotate;
    public long Id;
    public HexLocation Location;
    public bool PlayAnimation;
    public bool Activated;
    public List<int> Values;
    public int Index;
}

public class BattleSummary
{
    public long HashKey;
    public bool IsBossBattle;
    public BattleTypes BattleType;
    public long StageId;
    public long GroundId;
    public GroupTag Winner;
    public BattleEndType EndType;
    public int EndFrame;
    public GroupSummary Group01Summary;
    public GroupSummary Group02Summary;
    public WeekDungeonSummary WeekDungeonSummary;
    public RaidSummary RaidSummary;
    public ArenaSummary ArenaSummary;
    public int ContinueCount;
    public float ElapsedRealtime;
}

public class SkillCardHand
{
    public float Cost;
    public List<SkillCardInfo> SkillCardsInHand;
}

public class TacticSkipSummary
{
    public long StageId;
    public long Group01HexaUnitId;
    public long Group02HexaUnitId;
}

public enum StageDifficulty
{
    None = 0,
    Normal = 1,
    Hard = 2,
    VeryHard = 3,
}

public class CampaignSubStageSaveDB : ContentSaveDB
{
    public ContentType ContentType;
}

public class CampaignTutorialStageSaveDB : ContentSaveDB
{
    public ContentType ContentType;
}

public class MailDB
{
    public long ServerId;
    public long AccountServerId;
    public MailType Type;
    public long UniqueId;
    public string Sender;
    public Dictionary<Language, string> LocalizedSender;
    public string Comment;
    public Dictionary<Language, string> LocalizedComment;
    public DateTime SendDate;
    public DateTime? ReceiptDate;
    public DateTime? ExpireDate;
    public List<ParcelInfo> ParcelInfos;
    public List<ParcelInfo> RemainParcelInfos;
}

public class MissionHistoryDB
{
    public long MissionUniqueId;
    public DateTime CompleteTime;
    public bool Expired;
}

public class MissionProgressDB
{
    public long MissionUniqueId;
    public bool Complete;
    public DateTime StartTime;
    public Dictionary<long, long> ProgressParameters;
}

public class GuideMissionSeasonDB
{
    public long SeasonId;
    public long LoginCount;
    public DateTime StartDate;
    public DateTime LoginDate;
    public bool IsComplete;
}

public class MissionInfo
{
    public long Id;
    public MissionCategory Category;
    public MissionResetType ResetType;
    public MissionToastDisplayConditionType ToastDisplayType;
    public string Description;
    public bool IsVisible;
    public DateTime StartDate;
    public DateTime StartableEndDate;
    public DateTime EndDate;
    public long EndDday;
    public AccountState AccountState;
    public long AccountLevel;
    public List<long> PreMissionIds;
    public long NextMissionId;
    public SuddenMissionContentType[] SuddenMissionContentTypes;
    public MissionCompleteConditionType CompleteConditionType;
    public long CompleteConditionCount;
    public List<long> CompleteConditionParameters;
    public string RewardIcon;
    public List<ParcelInfo> Rewards;
    public string ToastImagePath;
    public long DisplayOrder;
    public bool HasFollowingMission;
    public String[] Shortcuts;
}

public enum MissionCategory
{
    Challenge = 0,
    Daily = 1,
    Weekly = 2,
    Achievement = 3,
    GuideMission = 4,
    All = 5,
    MiniGameScore = 6,
    MiniGameEvent = 7,
    EventAchievement = 8,
    DailySudden = 9,
    DailyFixed = 10,
    EventFixed = 11,
}

public class ShopProductDB
{
    public long EventContentId;
    public long ShopExcelId;
    public ShopCategoryType Category;
    public long DisplayOrder;
    public long PurchaseCount;
    public long PurchaseCountLimit;
    public long Price;
    public ShopProductType ProductType;
}

public enum ShopCategoryType
{
    General = 0,
    SecretStone = 1,
    Raid = 2,
    Gold = 3,
    Ap = 4,
    PickupGacha = 5,
    NormalGacha = 6,
    PointGacha = 7,
    EventGacha = 8,
    ArenaTicket = 9,
    Arena = 10,
    TutoGacha = 11,
    RecruitSellection = 12,
    EventContent_0 = 13,
    EventContent_1 = 14,
    EventContent_2 = 15,
    EventContent_3 = 16,
    EventContent_4 = 17,
    _Obsolete = 18,
    LimitedGacha = 19,
    MasterCoin = 20,
    SecretStoneGrowth = 21,
    TicketGacha = 22,
    DirectPayGacha_DontUseGlobal = 23,
    FesGacha = 24,
    TimeAttack = 25,
    GlobalSpecialGacha = 26,
    Chaser = 27,
}

public class ShopInfoDB
{
    public long EventContentId;
    public ShopCategoryType Category;
    public Int64? ManualRefreshCount;
    public bool IsRefresh;
    public DateTime? NextAutoRefreshDate;
    public DateTime? LastAutoRefreshDate;
    public List<ShopProductDB> ShopProductList;
}

public class ShopEligmaHistoryDB
{
    public long CharacterUniqueId;
    public long PurchaseCount;
}

public class GachaResult
{
    public long CharacterId;
    public CharacterDB Character;
    public ItemDB Stone;
}

public class ShopRecruitDB
{
    public long Id;
    public DateTime SalesStartDate;
    public DateTime SalesEndDate;
    public DateTime UpdateDate;
}

public class ShopFreeRecruitHistoryDB
{
    public long UniqueId;
    public int RecruitCount;
    public DateTime LastUpdateDate;
}

public class ParcelCost
{
    public List<ParcelInfo> ParcelInfos;
    public CurrencyTransaction Currency;
    public List<EquipmentDB> EquipmentDBs;
    public List<ItemDB> ItemDBs;
    public List<FurnitureDB> FurnitureDBs;
    public ConsumeCondition ConsumeCondition;
}

public class MemoryLobbyDB : ParcelBase
{
    public ParcelType Type;
    public long AccountServerId;
    public long MemoryLobbyUniqueId;
}

public class CumulativeTimeRewardInfo
{
    public long UniqueId;
    public DateTime StartDate;
    public DateTime EndDate;
    public Dictionary<long, ParcelInfo> RewardsByTimeCondition;
}

public class CumulativeTimeRewardDB
{
    public long ServerId;
    public long AccountServerId;
    public long RewardUniqueId;
    public long TimeCondition;
    public DateTime RewardDate;
}

public enum OpenConditionContent
{
    Shop = 0,
    Gacha = 1,
    LobbyIllust = 2,
    Raid = 3,
    Cafe = 4,
    Unit_Growth_Skill = 5,
    Unit_Growth_LevelUp = 6,
    Unit_Growth_Transcendence = 7,
    Arena = 8,
    Academy = 9,
    Equip = 10,
    Item = 11,
    Favor = 12,
    Prologue = 13,
    Mission = 14,
    WeekDungeon_Chase = 15,
    __Deprecated_WeekDungeon_FindGift = 16,
    __Deprecated_WeekDungeon_Blood = 17,
    Story_Sub = 18,
    Story_Replay = 19,
    WeekDungeon = 20,
    None = 21,
    Shop_Gem = 22,
    Craft = 23,
    Student = 24,
    GuideMission = 25,
    Clan = 26,
    Echelon = 27,
    Campaign = 28,
    EventContent = 29,
    Guild = 30,
    EventStage_1 = 31,
    EventStage_2 = 32,
    Talk = 33,
    Billing = 34,
    Schedule = 35,
    Story = 36,
    Tactic_Speed = 37,
    Cafe_Invite = 38,
    EventMiniGame_1 = 39,
    SchoolDungeon = 40,
    TimeAttackDungeon = 41,
    ShiftingCraft = 42,
    WorldRaid = 43,
    Tactic_Skip = 44,
    Mulligan = 45,
    EventPermanent = 46,
}

public class ConquestTileDB
{
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public TileState TileState;
    public long Level;
    public Boolean[] StarFlags;
    public bool IsThreeStarClear;
    public bool IsAnyStarClear;
}

public class WorldRaidLocalBossDB
{
    public long SeasonId;
    public long GroupId;
    public long UniqueId;
    public bool IsScenario;
    public bool IsCleardEver;
    public long TacticMscSum;
    public RaidBattleDB RaidBattleDB;
}

public class ToastDB
{
    public long UniqueId;
    public string Text;
    public Dictionary<Language, string> LocalizeText;
    public string ToastId;
    public DateTime BeginDate;
    public DateTime EndDate;
    public int LifeTime;
    public int Delay;
}

public enum Difficulty
{
    Normal = 0,
    Hard = 1,
    VeryHard = 2,
    Hardcore = 3,
    Extreme = 4,
    Insane = 5,
}

public enum RaidRoomSortOption
{
    HPHigh = 0,
    HPLow = 1,
    RemainTimeHigh = 2,
    RemainTimeLow = 3,
}

public class RaidDB
{
    public RaidMemberDescription Owner;
    public long ServerId;
    public long UniqueId;
    public long SeasonId;
    public DateTime Begin;
    public DateTime End;
    public long PlayerCount;
    public List<int> Tags;
    public string SecretCode;
    public RaidStatus RaidState;
    public bool IsPractice;
    public List<RaidBossDB> RaidBossDBs;
    public Dictionary<long, List<long>> ParticipateCharacterServerIds;
    public bool IsEnterRoom;
    public long AccountLevelWhenCreateDB;
    public bool ClanAssistUsed;
}

public class RaidDetailDB
{
    public long RaidUniqueId;
    public DateTime EndDate;
    public List<RaidPlayerInfoDB> DamageTable;
}

public class RaidBattleDB
{
    public long RaidUniqueId;
    public int RaidBossIndex;
    public long CurrentBossHP;
    public long CurrentBossGroggy;
    public long CurrentBossAIPhase;
    public string BIEchelon;
    public bool IsClear;
    public RaidMemberCollection RaidMembers;
    public RaidDebuffCollection DebuffDescriptions;
    public List<long> SubPartsHPs;
}

public class AssistCharacterDB : CharacterDB
{
    public EchelonType EchelonType;
    public int SlotNumber;
    public long AccountId;
    public AssistRelation AssistRelation;
    public long AssistCharacterServerId;
    public string NickName;
    public List<EquipmentDB> EquipmentDBs;
    public WeaponDB WeaponDB;
    public GearDB GearDB;
    public bool IsMulligan;
}

public enum RaidSeasonType
{
    None = 0,
    Open = 1,
    Close = 2,
    Settlement = 3,
}

public class RaidGiveUpDB
{
    public long Ranking;
    public long RankingPoint;
    public long BestRankingPoint;
}

public class RaidLobbyInfoDB
{
    public long SeasonId;
    public int Tier;
    public long Ranking;
    public long BestRankingPoint;
    public long TotalRankingPoint;
    public long ReceivedRankingRewardId;
    public bool CanReceiveRankingReward;
    public RaidDB PlayingRaidDB;
    public List<long> ReceiveRewardIds;
    public List<long> ParticipateCharacterServerIds;
    public Dictionary<string, Difficulty> PlayableHighestDifficulty;
    public Dictionary<long, long> SweepPointByRaidUniqueId;
    public DateTime SeasonStartDate;
    public DateTime SeasonEndDate;
    public DateTime SettlementEndDate;
    public long NextSeasonId;
    public DateTime NextSeasonStartDate;
    public DateTime NextSeasonEndDate;
    public DateTime NextSettlementEndDate;
}

public enum RankingSearchType
{
    None = 0,
    Rank = 1,
    Score = 2,
}

public class RaidUserDB
{
    public long AccountId;
    public long RepresentCharacterUniqueId;
    public long Level;
    public string Nickname;
    public int Tier;
    public long Rank;
    public long BestRankingPoint;
    public double BestRankingPointDetail;
    public RaidTeamSettingDB RaidTeamSettingDB;
}

public class RaidTeamSettingDB
{
    public long AccountId;
    public long TryNumber;
    public EchelonType EchelonType;
    public IList<RaidCharacterDB> MainCharacterDBs;
    public IList<RaidCharacterDB> SupportCharacterDBs;
}

public class SkipHistoryDB
{
    public long AccountServerId;
    public int Prologue;
    public Dictionary<int, int> Tutorial;
}

public class ScenarioHistoryDB
{
    public long AccountServerId;
    public long ScenarioUniqueId;
    public DateTime ClearDateTime;
}

public class ScenarioGroupHistoryDB
{
    public long AccountServerId;
    public long ScenarioGroupUqniueId;
    public long ScenarioType;
    public Int64? EventContentId;
    public DateTime ClearDateTime;
    public bool IsReturn;
}

public class StoryStrategyStageSaveDB : CampaignMainStageSaveDB
{
    public ContentType ContentType;
}

public class CafeDB
{
    public long CafeDBId;
    public long AccountId;
    public int CafeRank;
    public DateTime LastUpdate;
    public DateTime? LastSummonDate;
    public bool IsNew;
    public Dictionary<long, CafeCharacterDB> CafeVisitCharacterDBs;
    public List<FurnitureDB> FurnitureDBs;
    public Dictionary<CurrencyTypes, long> CurrencyDict;
    public Dictionary<CurrencyTypes, DateTime> UpdateTimeDict;
}

public class CafePresetDB
{
    public long ServerId;
    public int SlotId;
    public string PresetName;
    public List<int> ReserveTimeIndex;
    public bool IsEmpty;
}

public class RaidSeasonRankingHistoryDB
{
    public long AccountId;
    public long SeasonId;
    public long Ranking;
    public long BestRankingPoint;
    public int Tier;
    public DateTime ReceivedDate;
}

public class CraftNodeDB
{
    public CraftNodeTier NodeTier;
    public long SlotSequence;
    public long NodeId;
    public long NodeQuality;
    public long NodeLevel;
    public int NodeRandomSeed;
    public int NodeRandomSequence;
    public List<long> LeafNodeIds;
    public long ResultId;
    public ParcelInfo RewardParcelInfo;
}

public enum CraftNodeTier
{
    Base = 0,
    Node01 = 1,
    Node02 = 2,
    Node03 = 3,
}

public class CraftInfoDB
{
    public long SlotSequence;
    public DateTime StartTime;
    public DateTime EndTime;
    public DateTime CraftSlotOpenDate;
    public List<CraftNodeDB> Nodes;
}

public class ShiftingCraftInfoDB
{
    public long SlotSequence;
    public long CraftRecipeId;
    public long CraftAmount;
    public DateTime StartTime;
    public DateTime EndTime;
}

public class ArenaPlayerInfoDB
{
    public long CurrentSeasonId;
    public long PlayerGroupId;
    public long CurrentRank;
    public long SeasonRecord;
    public long AllTimeRecord;
    public long CumulativeTimeReward;
    public DateTime TimeRewardLastUpdateTime;
    public DateTime BattleEnterActiveTime;
    public DateTime DailyRewardActiveTime;
}

public class ArenaUserDB
{
    public long AccountServerId;
    public long RepresentCharacterUniqueId;
    public string NickName;
    public long Rank;
    public long Level;
    public long Exp;
    public ArenaTeamSettingDB TeamSettingDB;
    public string UserName;
}

public class ArenaBattleDB
{
    public long ArenaBattleServerId;
    public long Season;
    public long Group;
    public DateTime BattleStartTime;
    public DateTime BattleEndTime;
    public long Seed;
    public ArenaUserDB AttackingUserDB;
    public ArenaUserDB DefendingUserDB;
    public BattleSummary BattleSummary;
}

public class ArenaHistoryDB
{
    public ArenaBattleDB ArenaBattleDB;
}

public class ArenaDamageReportDB
{
    public long ArenaBattleServerId;
    public long WinnerAccountServerId;
    public ArenaUserDB AttackerUserDB;
    public ArenaUserDB DefenderUserDB;
    public DateTime BattleEndTime;
    public Dictionary<long, long> AttackerDamageReport;
    public Dictionary<long, long> DefenderDamageReport;
}

public class WeekDungeonStageHistoryDB
{
    public long AccountServerId;
    public long StageUniqueId;
    public Dictionary<StarGoalType, long> StarGoalRecord;
}

public class AcademyDB
{
    public long AccountId;
    public DateTime LastUpdate;
    public Dictionary<long, List<VisitingCharacterDB>> ZoneVisitCharacterDBs;
    public Dictionary<long, List<long>> ZoneScheduleGroupRecords;
}

public class AcademyLocationDB
{
    public long AccountId;
    public long LocationId;
    public long Rank;
    public long Exp;
}

public class EventRewardIncreaseDB
{
    public EventTargetType EventTargetType;
    public BasisPoint Multiplier;
    public DateTime BeginDate;
    public DateTime EndDate;
}

public class ContentSaveDB
{
    public long AccountServerId;
    public DateTime CreateTime;
    public long StageUniqueId;
    public long LastEnterStageEchelonNumber;
    public List<ParcelInfo> StageEntranceFee;
    public long TacticClearTimeMscSum;
    public long AccountLevelWhenCreateDB;
    public string BIEchelon;
    public string BIEchelon1;
    public string BIEchelon2;
    public string BIEchelon3;
    public string BIEchelon4;
}

public class EventContentChangeDB
{
    public long AccountId;
    public long EventContentId;
    public long UseAmount;
    public long ChangeCount;
    public long AccumulateChangeCount;
    public DateTime LastUpdateDate;
    public bool ChangeFlag;
}

public enum ContentType
{
    None = 0,
    CampaignMainStage = 1,
    CampaignSubStage = 2,
    WeekDungeon = 3,
    EventContentMainStage = 4,
    EventContentSubStage = 5,
    CampaignTutorialStage = 6,
    EventContentMainGroundStage = 7,
    SchoolDungeon = 8,
    TimeAttackDungeon = 9,
    Raid = 10,
    Conquest = 11,
    EventContentStoryStage = 12,
    CampaignExtraStage = 13,
    StoryStrategyStage = 14,
}

public class IrcServerConfig
{
    public string HostAddress;
    public int Port;
    public string Password;
}

public class ClanDB
{
    public long ClanDBId;
    public string ClanName;
    public string ClanChannelName;
    public string ClanPresidentNickName;
    public long ClanPresidentRepresentCharacterUniqueId;
    public string ClanNotice;
    public long ClanMemberCount;
    public ClanJoinOption ClanJoinOption;
}

public class ClanMemberDB
{
    public long AccountId;
    public long AccountLevel;
    public string AccountNickName;
    public long ClanDBId;
    public long RepresentCharacterUniqueId;
    public long AttendanceCount;
    public ClanSocialGrade ClanSocialGrade;
    public DateTime JoinDate;
    public DateTime SocialGradeUpdateTime;
    public DateTime LastLoginDate;
    public DateTime AppliedDate;
    public ClanMemberDescriptionDB ClanMemberDescriptionDB;
}

public enum ClanJoinOption
{
    Free = 0,
    Permission = 1,
    All = 2,
}

public class ClanMemberDescriptionDB
{
    public long Exp;
    public string Comment;
    public int CollectedCharactersCount;
    public long ArenaSeasonBestRanking;
    public long ArenaSeasonCurrentRanking;
}

public enum ClanSocialGrade
{
    None = 0,
    President = 1,
    Manager = 2,
    Member = 3,
    Applicant = 4,
    Refused = 5,
    Kicked = 6,
    Quit = 7,
    VicePredisident = 8,
}

public class ClanAssistSlotDB
{
    public EchelonType EchelonType;
    public long SlotNumber;
    public long CharacterDBId;
    public DateTime DeployDate;
    public long TotalRentCount;
}

public enum EchelonType
{
    None = 0,
    Adventure = 1,
    Raid = 2,
    ArenaAttack = 3,
    ArenaDefence = 4,
    WeekDungeonChaserA = 5,
    Scenario = 6,
    WeekDungeonBlood = 7,
    WeekDungeonChaserB = 8,
    WeekDungeonChaserC = 9,
    WeekDungeonFindGift = 10,
    EventContent = 11,
    SchoolDungeonA = 12,
    SchoolDungeonB = 13,
    SchoolDungeonC = 14,
    TimeAttack = 15,
    WorldRaid = 16,
    Conquest = 17,
    ConquestManage = 18,
    StoryStrategyStage = 19,
}

public class ClanAssistRewardInfo
{
    public long CharacterDBId;
    public DateTime DeployDate;
    public long RentCount;
    public List<ParcelInfo> CumultativeRewardParcels;
    public List<ParcelInfo> RentRewardParcels;
}

public class ClanAssistRentHistoryDB
{
    public long AssistCharacterAccountId;
    public long AssistCharacterDBId;
    public DateTime RentDate;
}

public enum PurchaseServerTag
{
    Audit = 0,
    PreAudit = 1,
    Production = 2,
    Hotfix = 3,
    Temp1 = 4,
    Temp2 = 5,
    Temp3 = 6,
    Temp4 = 7,
    Temp5 = 8,
    Test = 9,
    TestIn = 10,
}

public enum BillingTransactionEndType
{
    None = 0,
    Success = 1,
    Cancel = 2,
}

public class MonthlyProductPurchaseDB
{
    public long ProductId;
    public DateTime PurchaseDate;
    public DateTime? LastDailyRewardDate;
    public DateTime? RewardEndDate;
    public ProductTagType ProductTagType;
}

public class PurchaseOrderDB
{
    public long ShopCashId;
    public PurchaseStatusCode StatusCode;
    public long PurchaseOrderId;
}

public class BlockedProductDB
{
    public long CashProductId;
    public ShopCashBlockType MarketBlockType;
    public DateTime BeginDate;
    public DateTime EndDate;
}

public class EventContentBonusRewardDB
{
    public long EventContentId;
    public long EventStageUniqueId;
    public ParcelInfo BonusParcelInfo;
}

public class EventContentMainStageSaveDB : CampaignMainStageSaveDB
{
    public ContentType ContentType;
    public Dictionary<long, long> SelectedBuffDict;
    public long CurrentAppearedBuffGroupId;
}

public class EventContentCollectionDB
{
    public long EventContentId;
    public long GroupId;
    public long UniqueId;
    public DateTime ReceiveDate;
}

public class EventContentSubStageSaveDB : CampaignSubStageSaveDB
{
    public ContentType ContentType;
}

public class EventContentMainGroundStageSaveDB : CampaignSubStageSaveDB
{
    public ContentType ContentType;
}

public class EventContentBoxGachaDB
{
    public long AccountId;
    public long EventContentId;
    public long Seed;
    public long Round;
    public int PurchaseCount;
}

public class EventContentBoxGachaElement
{
    public long EventContentId;
    public long VariationId;
    public long Round;
    public long GroupId;
    public long UniqueId;
    public bool IsPrize;
    public List<ParcelInfo> Rewards;
}

public class CardShopElementDB
{
    public long EventContentId;
    public int SlotNumber;
    public long CardShopElementId;
    public bool SoldOut;
}

public class CardShopPurchaseHistoryDB
{
    public long EventContentId;
    public Rarity Rarity;
    public long PurchaseCount;
}

public class EventContentLocationDB
{
    public long AccountId;
    public long LocationId;
    public long Rank;
    public long Exp;
    public long ScheduleCount;
    public Dictionary<long, List<VisitingCharacterDB>> ZoneVisitCharacterDBs;
}

public class EventContentStoryStageSaveDB : CampaignSubStageSaveDB
{
    public ContentType ContentType;
}

public class EventContentDiceRaceDB
{
    public long EventContentId;
    public long Node;
    public long LapCount;
    public long DiceRollCount;
    public long ReceiveRewardLapCount;
}

public class EventContentDiceResult
{
    public int Index;
    public int MoveAmount;
    public List<ParcelInfo> Rewards;
}

public class EventContentPermanentDB
{
    public long EventContentId;
    public bool IsStageAllClear;
}

public class MomoTalkOutLineDB
{
    public long CharacterDBId;
    public long CharacterId;
    public long LatestMessageGroupId;
    public Int64? ChosenMessageId;
    public DateTime LastUpdateDate;
}

public class MomoTalkChoiceDB
{
    public long CharacterDBId;
    public long MessageGroupId;
    public long ChosenMessageId;
    public DateTime ChosenDate;
}

public class ClearDeckDB
{
    public long StageId;
    public string NickName;
    public int AccountLevel;
    public int Score;
    public bool Star1Flag;
    public bool Star2Flag;
    public bool Star3Flag;
    public int StarCount;
    public List<ClearDeckEchelonDB> ClearDeckEchelonDBs;
    public int TotalCharacterCount;
    public int TotalCharacterLevelSum;
}

public class MiniGameHistoryDB
{
    public long EventContentId;
    public long UniqueId;
    public long HighScore;
    public long AccumulatedScore;
    public DateTime ClearDate;
    public bool IsFullCombo;
}

public class MinigameRhythmSummary
{
    public string MusicTitle;
    public int PatternDifficulty;
    public bool IsSpecial;
    public int TotalNoteCount;
    public int CriticalCount;
    public int AttackCount;
    public int MissCount;
    public bool IsFullCombo;
    public int MaxCombo;
    public long FinalScore;
    public long HPBonusScore;
    public DateTime GameStartTime;
    public DateTime GameEndTime;
    public float RhythmGamePlayTime;
    public float StdDev;
    public MinigameJudgeRecord[] MinigameJudgeRecords;
    public bool IsAutoPlay;
}

public enum NotificationEventReddot
{
    StagePointReward = 0,
    MissionComplete = 1,
    MiniGameMissionComplete = 2,
    WorldRaidReward = 3,
    ConquestCalculateReward = 4,
    DiceRaceLapReward = 5,
}

public class SchoolDungeonStageHistoryDB
{
    public long AccountServerId;
    public long StageUniqueId;
    public Boolean[] StarFlags;
}

public class TimeAttackDungeonRoomDB
{
    public long AccountId;
    public long SeasonId;
    public long RoomId;
    public DateTime CreateDate;
    public DateTime RewardDate;
    public bool IsPractice;
    public List<DateTime> SweepHistoryDates;
    public List<TimeAttackDungeonBattleHistoryDB> BattleHistoryDBs;
}

public class WorldRaidClearHistoryDB
{
    public long SeasonId;
    public long GroupId;
    public DateTime RewardReceiveDate;
}

public class WorldRaidBossGroup : ContentsValueChangeDB
{
    public ContentsChangeType ContentsChangeType;
    public long GroupId;
    public DateTime BossSpawnTime;
    public DateTime EliminateTime;
}

public class WorldRaidBossListInfoDB
{
    public long GroupId;
    public WorldRaidWorldBossDB WorldBossDB;
    public List<WorldRaidLocalBossDB> LocalBossDBs;
}

public class ResetableContentValueDB
{
    public ResetContentType ContentType;
    public long ContentValue;
    public DateTime LastUpdateTime;
}

public class ConquestInfoDB
{
    public long AccountId;
    public long EventContentId;
    public int EventGauge;
    public int EventSpawnCount;
    public int EchelonChangeCount;
    public int TodayConquestRentCount;
    public int TodayOperationRentCount;
    public long CalculateRewardConditionValue;
    public long ReceivedCalculateRewardConditionAmount;
}

public class ConquestEventObjectDB
{
    public long ConquestObjectDBId;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public long ObjectId;
    public ConquestEventObjectType ObjectType;
    public bool IsAlive;
}

public class ConquestEchelonDB
{
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public EchelonDB EchelonDB;
    public long AssistCharacterUniqueId;
    public ClanAssistUseInfo AssistUseInfo;
}

public class ConquestDisplayInfo
{
    public ConquestDisplayType Type;
    public long EntityId;
    public long TileUniqueId;
}

public class ConquestStageSaveDB : ContentSaveDB
{
    public ContentType ContentType;
    public Int64? ConquestEventObjectDBId;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public ConquestTileType ConquestTileType;
    public AssistCharacterDB AssistCharacterDB;
    public int EchelonSlotType;
    public int EchelonSlotIndex;
}

public class FriendIdCardDB
{
    public int Level;
    public string FriendCode;
    public string Comment;
    public DateTime LastConnectTime;
    public long RepresentCharacterUniqueId;
    public bool SearchPermission;
    public bool AutoAcceptFriendRequest;
    public long CardBackgroundId;
    public bool ShowAccountLevel;
    public bool ShowFriendCode;
    public bool ShowRaidRanking;
    public bool ShowArenaRanking;
    public Int64? RaidRanking;
    public Int64? ArenaRanking;
    public Int32? RaidTier;
}

public class IdCardBackgroundDB : ParcelBase
{
    public ParcelType Type;
    public long ServerId;
    public long UniqueId;
}

public class FriendDB
{
    public long AccountId;
    public int Level;
    public string Nickname;
    public DateTime LastConnectTime;
    public long RepresentCharacterUniqueId;
    public long FriendCount;
}

public enum FriendSearchLevelOption
{
    Recommend = 0,
    All = 1,
    Level1To30 = 2,
    Level31To40 = 3,
    Level41To50 = 4,
    Level51To60 = 5,
    Level61To70 = 6,
    Level71To80 = 7,
    Level81To90 = 8,
    Level91To100 = 9,
}

public class GearDB : ParcelBase
{
    public ParcelType Type;
    public long ServerId;
    public long UniqueId;
    public int Level;
    public long Exp;
    public int Tier;
    public long SlotIndex;
    public long BoundCharacterServerId;
}

public class BannerDB
{
    public long BannerOrder;
    public DateTime StartDate;
    public DateTime EndDate;
    public string Url;
    public string FileName;
    public int LinkedLobbyBannerId;
    public EventContentType BannerType;
    public BannerDisplayType BannerDisplayType;
}

public class ContentsLockDB
{
    public int ContentId;
    public DateTime BeginDate;
    public DateTime EndDate;
    public DateTime CreateDate;
}

public class CheatCharacterCustomPreset
{
    public long UniqueId;
    public int StarGrade;
    public int Level;
    public int ExSkillLevel;
    public int PublicSkillLevel;
    public int PassiveSkillLevel;
    public int ExPassiveSkillLevel;
    public CheatEquipmentCustomPreset[] Equipments;
    public CheatWeaponCustomPreset Weapon;
}

public enum CheatFlags
{
    None = 0,
    Conquest = 1,
    Mission = 2,
}

public class BasePacket
{
    public SessionKey SessionKey;
    public long AccountId;
}

public enum ServerNotificationFlag
{
    None = 0,
    NewMailArrived = 4,
    HasUnreadMail = 8,
    NewToastDetected = 16,
    CanReceiveArenaDailyReward = 32,
    CanReceiveRaidReward = 64,
    ServerMaintenance = 256,
    CannotReceiveMail = 512,
    InventoryFullRewardMail = 1024,
    CanReceiveClanAttendanceReward = 2048,
    HasClanApplicant = 4096,
    HasFriendRequest = 8192,
    CheckConquest = 16384,
}

public enum OpenConditionLockReason
{
    None = 0,
    Level = 1,
    StageClear = 2,
    Time = 4,
    Day = 8,
    CafeRank = 16,
    ScenarioModeClear = 32,
}

public class SessionKey
{
    public long AccountServerId;
    public string MxToken;
}

public enum AccountState
{
    WaitingSignIn = 0,
    Normal = 1,
    Dormant = 2,
    Comeback = 3,
    Newbie = 4,
}

public enum AttendanceType
{
    Basic = 0,
    Event = 1,
    Newbie = 2,
    EventCountDown = 3,
    Event20Days = 4,
}

public enum AttendanceCountRule
{
    Accumulation = 0,
    Date = 1,
}

public enum AttendanceResetType
{
    User = 0,
    Server = 1,
}

public enum MailType
{
    System = 0,
    Attendance = 1,
    Event = 2,
    MassTrade = 3,
    InventoryFull = 4,
    ArenaDefenseVictory = 5,
    CouponUsageReward = 6,
    ArenaSeasonClose = 7,
    ProductReward = 8,
    MonthlyProductReward = 9,
    ExpiryChangeItem = 10,
    ClanAttendance = 11,
    AccountLink = 12,
    NewUserBonus = 13,
    LeftClanAssistReward = 14,
    CashShopBuy = 15,
    MonthlyProductPackage = 16,
    WebEventReward = 17,
    AttendanceImmediately = 18,
    WeeklyProductReward = 19,
    BiweeklyProductReward = 20,
}

public enum MultipleConditionCheckType
{
    And = 0,
    Or = 1,
}

public enum WeekDay
{
    Sunday = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    All = 7,
}

public struct ParcelKeyPair
{
    public ParcelType Type;
    public long Id;
}

public struct BasisPoint
{
}

public enum CurrencyTypes
{
    Invalid = 0,
    Gold = 1,
    GemPaid = 2,
    GemBonus = 3,
    Gem = 4,
    ActionPoint = 5,
    AcademyTicket = 6,
    ArenaTicket = 7,
    RaidTicket = 8,
    WeekDungeonChaserATicket = 9,
    WeekDungeonFindGiftTicket = 10,
    WeekDungeonBloodTicket = 11,
    WeekDungeonChaserBTicket = 12,
    WeekDungeonChaserCTicket = 13,
    SchoolDungeonATicket = 14,
    SchoolDungeonBTicket = 15,
    SchoolDungeonCTicket = 16,
    TimeAttackDungeonTicket = 17,
    MasterCoin = 18,
    WorldRaidTicketA = 19,
    WorldRaidTicketB = 20,
    WorldRaidTicketC = 21,
    Max = 22,
}

public class ParcelBase
{
}

public enum ParcelType
{
    None = 0,
    Character = 1,
    Currency = 2,
    Equipment = 3,
    Item = 4,
    GachaGroup = 5,
    Product = 6,
    Shop = 7,
    MemoryLobby = 8,
    AccountExp = 9,
    CharacterExp = 10,
    FavorExp = 11,
    TSS = 12,
    Furniture = 13,
    ShopRefresh = 14,
    LocationExp = 15,
    Recipe = 16,
    CharacterWeapon = 17,
    ProductMonthly = 18,
    CharacterGear = 19,
    IdCardBackground = 20,
}

public class FurnitureDB : ConsumableItemBaseDB
{
    public ParcelType Type;
    public FurnitureLocation Location;
    public float PositionX;
    public float PositionY;
    public float Rotation;
    public long ItemDeploySequence;
}

public class ParcelResultStepInfo
{
    public ParcelProcessActionType ParcelProcessActionType;
    public List<ParcelDetail> StepParcelDetails;
}

public class ConsumableItemBaseDB : ParcelBase
{
    public long ServerId;
    public long UniqueId;
    public long StackCount;
}

public enum EchelonStatusFlag
{
    None = 0,
    BeforeDeploy = 1,
    OnDuty = 2,
}

public enum AssistRelation
{
    None = 0,
    Clan = 1,
    Friend = 2,
}

public enum ResetContentType
{
    None = 0,
    HardStagePlay = 1,
    StarategyMapHeal = 2,
    ShopRefresh = 3,
    ArenaDefenseVictoryReward = 4,
    WeeklyMasterCoin = 5,
    WorldRaidGemEnterCount = 6,
}

public enum CampaignState
{
    BeforeStart = 0,
    BeginPlayerPhase = 1,
    PlayerPhase = 2,
    EndPlayerPhase = 3,
    BeginEnemyPhase = 4,
    EnemyPhase = 5,
    EndEnemyPhase = 6,
    Win = 7,
    Lose = 8,
}

public class HexaTileState
{
    public int Id;
    public bool IsHide;
    public bool IsFog;
    public bool CanNotMove;
}

public class HexaDisplayInfo
{
    public HexaDisplayType Type;
    public long EntityId;
    public long UniqueId;
    public HexLocation Location;
    public long Parameter;
    public StrategyClearRewardInfo StageRewardInfo;
}

public enum TacticEntityType
{
    None = 0,
    Student = 1,
    Minion = 2,
    Elite = 4,
    Champion = 8,
    Boss = 16,
    Obstacle = 32,
    Servant = 64,
    Vehicle = 128,
    Summoned = 256,
    Hallucination = 512,
    DestructibleProjectile = 1024,
}

public enum BattleTypes
{
    None = 0,
    Adventure = 1,
    ScenarioMode = 2,
    WeekDungeonChaserA = 4,
    WeekDungeonBlood = 8,
    WeekDungeonChaserB = 16,
    WeekDungeonChaserC = 32,
    WeekDungeonFindGift = 64,
    WeekDungeon = 124,
    EventContent = 128,
    TutorialAdventure = 256,
    SingleRaid = 2048,
    MultiRaid = 4096,
    PracticeRaid = 8192,
    Raid = 14336,
    Arena = 2097152,
    PvP = 2097152,
    TimeAttack = 8388608,
    SchoolDungeonA = 33554432,
    SchoolDungeonB = 67108864,
    SchoolDungeonC = 134217728,
    SchoolDungeon = 234881024,
    WorldRaid = 268435456,
    Conquest = 536870912,
    PvE = 771752447,
    All = -1,
}

public enum GroupTag
{
    None = 0,
    Group01 = 1,
    Group02 = 2,
    Group03 = 4,
    Group04 = 8,
    Group05 = 16,
    Group06 = 32,
    Group07 = 64,
    Group08 = 128,
    Group09 = 256,
    Group10 = 512,
    Group11 = 1024,
    Group12 = 2048,
    Group13 = 4096,
    Group14 = 8192,
    Group15 = 16384,
    Group16 = 32768,
}

public enum BattleEndType
{
    None = 0,
    AllNearlyDead = 1,
    TimeOut = 2,
    EscortFailed = 3,
    Clear = 4,
}

public class GroupSummary
{
    public long TeamId;
    public EntityId LeaderEntityId;
    public KeyedCollection<EntityId, HeroSummary> Heroes;
    public KeyedCollection<EntityId, HeroSummary> Supporters;
    public KeyedCollection<EntityId, HeroSummary> TSSs;
    public bool UseAutoSkill;
    public int TssUseCount;
    public SkillCostSummary SkillCostSummary;
}

public class WeekDungeonSummary
{
    public WeekDungeonType DungeonType;
    public List<FindGiftSummary> FindGifts;
}

public class RaidSummary
{
    public long RaidSeasonId;
    public RaidBossResultCollection RaidBossResults;
}

public class ArenaSummary
{
    public long ArenaMapId;
    public long EnemyAccountId;
    public long EnemyAccountLevel;
}

public struct SkillCardInfo
{
    public long CharacterId;
    public int HandIndex;
    public int SkillId;
    public int RemainCoolTime;
}

public enum Language
{
    Kr = 0,
    Jp = 1,
    Th = 2,
    Tw = 3,
    En = 4,
}

public enum MissionResetType
{
    None = 0,
    Daily = 1,
    Weekly = 2,
}

public enum MissionToastDisplayConditionType
{
    Always = 0,
    Complete = 1,
    Never = 2,
}

public enum SuddenMissionContentType
{
    OrdinaryState = 0,
    CampaignNormalStage = 1,
    CampaignHardStage = 2,
    EventStage = 3,
    WeekDungeon = 4,
    Chaser = 5,
    SchoolDungeon = 6,
    TimeAttackDungeon = 7,
    Raid = 8,
}

public enum MissionCompleteConditionType
{
    None = 0,
    DailyLogin = 1,
    DailyLoginCount = 2,
    CompleteMission = 3,
    EquipmentLevelUpCount = 4,
    EquipmentTierUpCount = 5,
    CharacterLevelUpCount = 6,
    CharacterTranscendenceCount = 7,
    ClearTaticBattleCount = 8,
    ClearCampaignStageCount = 9,
    KillSpecificEnemyCount = 10,
    KillEnemyWithTagCount = 11,
    GetCharacterCount = 12,
    GetCharacterWithTagCount = 13,
    GetSpecificCharacterCount = 14,
    AccountLevelUp = 15,
    GetEquipmentCount = 16,
    GachaCount = 17,
    UseGem = 18,
    GetGem = 19,
    GetGemPaid = 20,
    GetGold = 21,
    GetItem = 22,
    GetFavorLevel = 23,
    __Deprecated_EquipmentAtSpecificLevelCount = 24,
    EquipmentAtSpecificTierUpCount = 25,
    CharacterAtSpecificLevelCount = 26,
    CharacterAtSpecificTranscendenceCount = 27,
    CharacterSkillLevelUpCount = 28,
    CharacterAtSpecificSkillLevelCount = 29,
    CompleteScheduleCount = 30,
    CompleteScheduleGroupCount = 31,
    AcademyLocationRankSum = 32,
    CraftCount = 33,
    GetComfortPoint = 34,
    GetWeaponCount = 35,
    EquipWeaponCount_Obsolete = 36,
    CompleteScheduleWithSpecificCharacter = 37,
    CafeInteractionCount = 38,
    SpecificCharacterAtSpecificLevel = 39,
    SpecificCharacterAtSpecificTranscendence = 40,
    LobbyInteraction = 41,
    ClearWeekDungeonCount = 42,
    ClearSpecificWeekDungeonCount = 43,
    JoinRaidCount = 44,
    JoinSpecificRaidCount = 45,
    JoinArenaCount = 46,
    ArenaVictoryCount = 47,
    RaidDamageAmountOnOneBattle = 48,
    ClearEventStageCount = 49,
    UseSpecificCharacterCount = 50,
    UseGold = 51,
    UseTiket = 52,
    ShopBuyCount = 53,
    ShopBuyActionPointCount = 54,
    SpecificCharacterAtSpecificFavorRank = 55,
    ClearSpecificScenario = 56,
    GetSpecificItemCount = 57,
    TotalGetClearStarCount = 58,
    CompleteCampaignStageMinimumTurn = 59,
    TotalLoginCount = 60,
    LoginAtSpecificTime = 61,
    CompleteFavorSchedule = 62,
    CompleteFavorScheduleAtSpecificCharacter = 63,
    GetMemoryLobbyCount = 64,
    GetFurnitureGroupCount = 65,
    AcademyLocationAtSpecificRank = 66,
    ClearCampaignStageDifficultyNormal = 67,
    ClearCampaignStageDifficultyHard = 68,
    ClearChaserDungeonCount = 69,
    ClearSpecificChaserDungeonCount = 70,
    GetCafeRank = 71,
    SpecificStarCharacterCount = 72,
    EventClearCampaignStageCount = 73,
    EventClearSpecificCampaignStageCount = 74,
    EventCompleteCampaignStageMinimumTurn = 75,
    EventClearCampaignStageDifficultyNormal = 76,
    EventClearCampaignStageDifficultyHard = 77,
    ClearSpecificCampaignStageCount = 78,
    GetItemWithTagCount = 79,
    GetFurnitureWithTagCount = 80,
    GetEquipmentWithTagCount = 81,
    ClearCampaignStageTimeLimitFromSecond = 82,
    ClearEventStageTimeLimitFromSecond = 83,
    ClearRaidTimeLimitFromSecond = 84,
    ClearBattleWithTagCount = 85,
    ClearWeekDungeonTimeLimitFromSecond = 86,
    CompleteScheduleWithTagCount = 87,
    ClearChaserDungeonTimeLimitFromSecond = 88,
    GetTotalScoreRhythm = 89,
    GetBestScoreRhythm = 90,
    GetSpecificScoreRhythm = 91,
    ClearStageRhythm = 92,
    GetComboCountRhythm = 93,
    GetFullComboRhythm = 94,
    GetFeverCountRhythm = 95,
    UseActionPoint = 96,
    ClearSchoolDungeonCount = 97,
    ClearSchoolDungeonTimeLimitFromSecond = 98,
    ClearSpecificSchoolDungeonCount = 99,
    GetCriticalCountRhythm = 100,
    WeaponTranscendenceCount = 101,
    WeaponLevelUpCount = 102,
    WeaponAtSpecificTranscendenceCount = 103,
    WeaponAtSpecificLevelUpCount = 104,
    BuyShopGoods = 105,
    ClanLogin = 106,
    AssistCharacterSetting = 107,
    DailyMissionFulfill = 108,
    SelectedMissionFulfill = 109,
    TotalDamageToWorldRaid = 110,
    JoinWorldRaidTypeNumber = 111,
    JoinWorldRaidBattleWithTagCount = 112,
    ClearWorldRaidTimeLimitFromSecond = 113,
    KillEnemyWithDecagrammatonSPOTagCount = 114,
    ConquerTileCount = 115,
    ConquerSpecificStepTileCount = 116,
    ConquerSpecificStepTileAll = 117,
    UpgradeConquestBaseTileCount = 118,
    KillConquestBoss = 119,
    ClearEventConquestTileTimeLimitFromSecond = 120,
    DiceRaceUseDiceCount = 121,
    DiceRaceFinishLapCount = 122,
}

public enum ShopProductType
{
    None = 0,
    General = 1,
    Refresh = 2,
}

public class CurrencyTransaction : ParcelBase
{
    public long Gold;
    public long Gem;
    public long GemBonus;
    public long GemPaid;
    public long ActionPoint;
    public long ArenaTicket;
    public long RaidTicket;
    public long WeekDungeonChaserATicket;
    public long WeekDungeonChaserBTicket;
    public long WeekDungeonChaserCTicket;
    public long WeekDungeonFindGiftTicket;
    public long WeekDungeonBloodTicket;
    public long AcademyTicket;
    public long SchoolDungeonATicket;
    public long SchoolDungeonBTicket;
    public long SchoolDungeonCTicket;
    public long TimeAttackDungeonTicket;
    public long MasterCoin;
    public long WorldRaidTicketA;
    public long WorldRaidTicketB;
    public long WorldRaidTicketC;
}

public enum ConsumeCondition
{
    And = 0,
    Or = 1,
}

public enum TileState
{
    None = 0,
    PartiallyConquested = 1,
    FullyConquested = 2,
}

public class RaidMemberDescription
{
    public long AccountId;
    public string AccountName;
    public long CharacterId;
    public RaidDamageCollection DamageCollection;
}

public enum RaidStatus
{
    None = 0,
    Playing = 1,
    Clear = 2,
    Close = 3,
}

public class RaidBossDB
{
    public int BossIndex;
    public long BossCurrentHP;
    public long BossGroggyPoint;
}

public class RaidPlayerInfoDB
{
    public long RaidServerId;
    public long AccountId;
    public DateTime JoinDate;
    public long DamageAmount;
    public int RaidEndRewardFlag;
    public int RaidPlayCount;
    public string Nickname;
    public long CharacterId;
    public Int64? AccountLevel;
}

public class RaidMemberCollection : Dictionary<long, RaidMemberDescription>
{
    public long TotalDamage;
    public List<RaidDamage> RaidDamages;
}

public class RaidDebuffCollection : Dictionary<string, DebuffDescription>
{
}

public class RaidCharacterDB
{
    public long ServerId;
    public long UniqueId;
    public int StarGrade;
    public int Level;
    public int SlotIndex;
    public long AccountId;
    public bool IsAssist;
    public bool HasWeapon;
}

public class CafeCharacterDB : VisitingCharacterDB
{
    public bool IsSummon;
    public DateTime LastInteractTime;
}

public class ArenaTeamSettingDB
{
    public EchelonType EchelonType;
    public long LeaderCharacterId;
    public IList<ArenaCharacterDB> MainCharacters;
    public IList<ArenaCharacterDB> SupportCharacters;
    public ArenaCharacterDB TSSCharacterDB;
    public long MapId;
}

public enum StarGoalType
{
    None = 0,
    AllAlive = 1,
    Clear = 2,
    GetBoxes = 3,
    ClearTimeInSec = 4,
}

public class VisitingCharacterDB
{
    public long UniqueId;
    public long ServerId;
}

public enum EventTargetType
{
    WeekDungeon = 0,
    Chaser = 1,
    Campaign_Normal = 2,
    Campaign_Hard = 3,
    SchoolDungeon = 4,
    AcademySchedule = 5,
    TimeAttackDungeon = 6,
    AccountLevelExpIncrease = 7,
    Raid = 8,
}

public enum ProductTagType
{
    Monthly = 0,
    Weekly = 1,
    Biweekly = 2,
}

public enum PurchaseStatusCode
{
    None = 0,
    Start = 1,
    PublishSuccess = 2,
    End = 3,
    Error = 4,
    DuplicateOrder = 5,
    Refund = 6,
}

public enum ShopCashBlockType
{
    None = -9999,
    GooglePlay = -3,
    AppStore = -2,
    All = -1,
}

public enum Rarity
{
    N = 0,
    R = 1,
    SR = 2,
    SSR = 3,
}

public class ClearDeckEchelonDB
{
    public long CharacterIdLeader;
    public List<ClearDeckCharacter> MainCharacters;
    public List<ClearDeckCharacter> SupporterCharacters;
    public ClearDeckCharacter TSSCharacter;
    public int MainCharacterCount;
    public int SupporterCharacterCount;
    public int TotalCharacterCount;
    public int MainCharacterLevelSum;
    public int SupporterCharacterLevelSum;
    public int TotalCharacterLevelSum;
}

public class MinigameJudgeRecord
{
    public int NoteIndex;
    public float TimingError;
    public int CurrentCombo;
    public JudgeGrade JudgeGradeOfThisNote;
    public bool IsFeverOn;
}

public class TimeAttackDungeonBattleHistoryDB
{
    public TimeAttackDungeonType DungeonType;
    public long GeasId;
    public long DefaultPoint;
    public long ClearTimePoint;
    public long EndFrame;
    public List<TimeAttackDungeonCharacterDB> MainCharacterDBs;
    public List<TimeAttackDungeonCharacterDB> SupportCharacterDBs;
}

public class ContentsValueChangeDB
{
}

public enum ContentsChangeType
{
    None = 0,
    WorldRaidBossDamageRatio = 1,
    WorldRaidBossGroupDate = 2,
}

public class WorldRaidWorldBossDB
{
    public long GroupId;
    public long HP;
    public long Participants;
}

public enum ConquestEventObjectType
{
    None = 0,
    UnexpectedEnemy = 1,
    TreasureBox = 2,
    Eroison = 3,
    End = 4,
}

public enum ConquestDisplayType
{
    None = 0,
    TileConquered = 1,
    TileUpgraded = 2,
    SuddenEventOccured = 3,
    BossOpen = 4,
}

public enum ConquestTileType
{
    None = 0,
    Start = 1,
    Normal = 2,
    Battle = 3,
    Base = 4,
}

public enum EventContentType
{
    Stage = 0,
    Gacha = 1,
    Mission = 2,
    Shop = 3,
    Raid = 4,
    Arena = 5,
    BoxGacha = 6,
    Collection = 7,
    Recollection = 8,
    MiniGameRhythm = 9,
    CardShop = 10,
    EventLocation = 11,
    MinigameRhythmEvent = 12,
    FortuneGachaShop = 13,
    SubEvent = 14,
    EventMeetup = 15,
    BoxGachaResult = 16,
    Conquest = 17,
    WorldRaid = 18,
    DiceRace = 19,
    MiniGameRhythmMission = 20,
}

public enum BannerDisplayType
{
    Lobby = 0,
    Gacha = 1,
}

public class CheatEquipmentCustomPreset
{
    public int Tier;
    public int Level;
}

public class CheatWeaponCustomPreset
{
    public int StarGrade;
    public int Level;
}

public enum FurnitureLocation
{
    None = 0,
    Inventory = 1,
    Floor = 2,
    WallLeft = 3,
    WallRight = 4,
}

public enum ParcelProcessActionType
{
    None = 0,
    Cost = 1,
    Reward = 2,
}

public class ParcelDetail
{
    public ParcelInfo OriginParcel;
    public ParcelInfo MailSendParcel;
    public List<ParcelInfo> ConvertedParcelInfos;
    public ParcelChangeType ParcelChangeType;
}

public enum HexaDisplayType
{
    None = 0,
    EndBattle = 1,
    PlayScenario = 2,
    SpawnUnitFromUniqueId = 3,
    StatBuff = 4,
    DieUnit = 5,
    HideStrategy = 6,
    SpawnUnit = 7,
    SpawnStrategy = 8,
    SpawnTile = 9,
    HideTile = 10,
    ClearFogOfWar = 11,
    MoveUnit = 12,
    WarpUnit = 13,
    SetTileMovablity = 14,
    WarpUnitFromHideTile = 15,
    BossExile = 16,
}

public class StrategyClearRewardInfo
{
    public List<ParcelInfo> FirstClearReward;
    public List<ParcelInfo> ThreeStarReward;
    public Dictionary<long, List<ParcelInfo>> StrategyObjectRewards;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> EventContentBonusReward;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
}

public struct EntityId
{
}

public class HeroSummary
{
    public long ServerId;
    public EntityId BattleEntityId;
    public long HeroId;
    public int Grade;
    public int Level;
    public int ExSkillLevel;
    public int PublicSkillLevel;
    public int PassiveSkillLevel;
    public int ExtraPassiveSkillLevel;
    public StatSnapshotCollection StatSnapshotCollection;
    public long HPRateBefore;
    public long HPRateAfter;
    public int CrowdControlCount;
    public int CrowdControlDuration;
    public int EvadeCount;
    public int DamageImmuneCount;
    public int CrowdControlImmuneCount;
    public int DeadFrame;
    public TacticEntityType TacticEntityType;
    public List<BattleNumericLog> GivenNumericLogs;
    public List<BattleNumericLog> TakenNumericLogs;
    public List<BattleNumericLog> ObstacleBattleNumericLogs;
    public List<EquipmentSetting> Equipments;
    public WeaponSetting? CharacterWeapon;
    public IDictionary<SkillSlot, int> SkillCount;
    public KillLogCollection KillLog;
}

public class SkillCostSummary
{
    public float InitialCost;
    public CostRegenSnapshotCollection CostPerFrameSnapshots;
    public List<SkillCostAddSnapshot> CostAddSnapshots;
    public List<SkillCostUseSnapshot> CostUseSnapshots;
}

public enum WeekDungeonType
{
    None = 0,
    ChaserA = 1,
    ChaserB = 2,
    ChaserC = 3,
    FindGift = 4,
    Blood = 5,
}

public class FindGiftSummary
{
    public string UniqueName;
    public int ClearCount;
}

public class RaidBossResultCollection : Dictionary<int, RaidBossResult>
{
}

public class RaidDamageCollection : Dictionary<int, RaidDamage>
{
}

public struct RaidDamage
{
    public int Index;
    public long GivenDamage;
    public long GivenGroggyPoint;
}

public struct DebuffDescription
{
    public long AccountId;
    public string LogicEffectTemplateId;
    public string LogicEffectGroupId;
    public int LogicEffectLevel;
    public int DurationFrame;
    public SkillSlot SkillSlot;
    public int IssuedTimestamp;
}

public class ArenaCharacterDB
{
    public long ServerId;
    public long UniqueId;
    public int StarGrade;
    public int Level;
    public int PublicSkillLevel;
    public int ExSkillLevel;
    public int PassiveSkillLevel;
    public int ExtraPassiveSkillLevel;
    public int LeaderSkillLevel;
    public List<EquipmentDB> EquipmentDBs;
    public Dictionary<long, long> FavorRankInfo;
    public WeaponDB WeaponDB;
    public GearDB GearDB;
}

public class ClearDeckCharacter
{
    public long CharacterId;
    public int Level;
    public int StarGrade;
}

public enum JudgeGrade
{
    None = 0,
    Miss = 1,
    Attack = 2,
    Critical = 3,
}

public enum TimeAttackDungeonType
{
    None = 0,
    Defense = 1,
    Shooting = 2,
    Destruction = 3,
}

public class TimeAttackDungeonCharacterDB
{
    public long ServerId;
    public long UniqueId;
    public int StarGrade;
    public int Level;
    public bool HasWeapon;
    public bool IsAssist;
}

public enum ParcelChangeType
{
    NoChange = 0,
    Terminated = 1,
    MailSend = 2,
    Converted = 3,
}

public class StatSnapshotCollection : Dictionary<StatType, StatSnapshot>
{
}

public class BattleNumericLog
{
    public BattleEntityType EntityType;
    public BattleLogCategory Category;
    public BattleLogSourceType Source;
    public long CalculatedSum;
    public long AppliedSum;
    public long Count;
    public long CriticalMultiplierMax;
    public long CriticalCount;
    public long CalculatedMin;
    public long CalculatedMax;
    public long AppliedMin;
    public long AppliedMax;
}

public struct EquipmentSetting
{
    public long ServerId;
    public long UniqueId;
    public int Level;
    public int Tier;
}

public struct WeaponSetting
{
    public long UniqueId;
    public int StarGrade;
    public int Level;
}

public enum SkillSlot
{
    None = 0,
    NormalAttack01 = 1,
    NormalAttack02 = 2,
    NormalAttack03 = 3,
    NormalAttack04 = 4,
    NormalAttack05 = 5,
    NormalAttack06 = 6,
    NormalAttack07 = 7,
    NormalAttack08 = 8,
    NormalAttack09 = 9,
    NormalAttack10 = 10,
    ExSkill01 = 11,
    ExSkill02 = 12,
    ExSkill03 = 13,
    ExSkill04 = 14,
    ExSkill05 = 15,
    ExSkill06 = 16,
    ExSkill07 = 17,
    ExSkill08 = 18,
    ExSkill09 = 19,
    ExSkill10 = 20,
    Passive01 = 21,
    Passive02 = 22,
    Passive03 = 23,
    Passive04 = 24,
    Passive05 = 25,
    Passive06 = 26,
    Passive07 = 27,
    Passive08 = 28,
    Passive09 = 29,
    Passive10 = 30,
    ExtraPassive01 = 31,
    ExtraPassive02 = 32,
    ExtraPassive03 = 33,
    ExtraPassive04 = 34,
    ExtraPassive05 = 35,
    ExtraPassive06 = 36,
    ExtraPassive07 = 37,
    ExtraPassive08 = 38,
    ExtraPassive09 = 39,
    ExtraPassive10 = 40,
    Support01 = 41,
    Support02 = 42,
    Support03 = 43,
    Support04 = 44,
    Support05 = 45,
    Support06 = 46,
    Support07 = 47,
    Support08 = 48,
    Support09 = 49,
    Support10 = 50,
    EnterBattleGround = 51,
    LeaderSkill01 = 52,
    LeaderSkill02 = 53,
    LeaderSkill03 = 54,
    LeaderSkill04 = 55,
    LeaderSkill05 = 56,
    LeaderSkill06 = 57,
    LeaderSkill07 = 58,
    LeaderSkill08 = 59,
    LeaderSkill09 = 60,
    LeaderSkill10 = 61,
    Equipment01 = 62,
    Equipment02 = 63,
    Equipment03 = 64,
    Equipment04 = 65,
    Equipment05 = 66,
    Equipment06 = 67,
    Equipment07 = 68,
    Equipment08 = 69,
    Equipment09 = 70,
    Equipment10 = 71,
    PublicSkill01 = 72,
    PublicSkill02 = 73,
    PublicSkill03 = 74,
    PublicSkill04 = 75,
    PublicSkill05 = 76,
    PublicSkill06 = 77,
    PublicSkill07 = 78,
    PublicSkill08 = 79,
    PublicSkill09 = 80,
    PublicSkill10 = 81,
    GroupBuff01 = 82,
    HexaBuff01 = 83,
    EventBuff01 = 84,
    EventBuff02 = 85,
    EventBuff03 = 86,
    MoveAttack01 = 87,
    MetamorphNormalAttack = 88,
    GroundPassive01 = 89,
    GroundPassive02 = 90,
    GroundPassive03 = 91,
    GroundPassive04 = 92,
    GroundPassive05 = 93,
    GroundPassive06 = 94,
    GroundPassive07 = 95,
    GroundPassive08 = 96,
    GroundPassive09 = 97,
    GroundPassive10 = 98,
    Count = 99,
}

public class KillLogCollection : Collection<KillLog>
{
}

public class CostRegenSnapshotCollection : Dictionary<long, SkillCostRegenSnapshot>
{
}

public struct SkillCostAddSnapshot
{
    public long Frame;
    public float Added;
}

public struct SkillCostUseSnapshot
{
    public long Frame;
    public float Used;
    public long CharId;
    public int Level;
}

public struct RaidBossResult
{
    public RaidDamage RaidDamage;
    public long EndHpRateRawValue;
    public long GroggyRateRawValue;
    public int GroggyCount;
    public List<long> SubPartsHPs;
    public long AIPhase;
}

public enum StatType
{
    None = 0,
    MaxHP = 1,
    AttackPower = 2,
    DefensePower = 3,
    HealPower = 4,
    AccuracyPoint = 5,
    AccuracyRate = 6,
    DodgePoint = 7,
    DodgeRate = 8,
    CriticalPoint = 9,
    CriticalChanceRate = 10,
    CriticalResistChanceRate = 11,
    CriticalDamageRate = 12,
    MoveSpeed = 13,
    SightRange = 14,
    ActiveGauge = 15,
    StabilityPoint = 16,
    StabilityRate = 17,
    ReloadTime = 18,
    MaxBulletCount = 19,
    IgnoreDelayCount = 20,
    WeaponRange = 21,
    BlockRate = 22,
    BodyRadius = 23,
    ActionCount = 24,
    StrategyMobility = 25,
    StrategySightRange = 26,
    StreetBattleAdaptation = 27,
    OutdoorBattleAdaptation = 28,
    IndoorBattleAdaptation = 29,
    HealEffectivenessRate = 30,
    CriticalChanceResistPoint = 31,
    CriticalDamageResistRate = 32,
    LifeRecoverOnHit = 33,
    NormalAttackSpeed = 34,
    AmmoCost = 35,
    GroggyGauge = 36,
    GroggyTime = 37,
    DamageRatio = 38,
    DamagedRatio = 39,
    OppressionPower = 40,
    OppressionResist = 41,
    RegenCost = 42,
    InitialWeaponRangeRate = 43,
    DefensePenetration = 44,
    DefensePenetrationResisit = 45,
    ExtendBuffDuration = 46,
    ExtendDebuffDuration = 47,
    ExtendCrowdControlDuration = 48,
    EnhanceExplosionRate = 49,
    EnhancePierceRate = 50,
    EnhanceMysticRate = 51,
    EnhanceLightArmorRate = 52,
    EnhanceHeavyArmorRate = 53,
    EnhanceUnarmedRate = 54,
    EnhanceSiegeRate = 55,
    EnhanceNormalRate = 56,
    EnhanceStructureRate = 57,
    EnhanceNormalArmorRate = 58,
    Max = 59,
}

public class StatSnapshot
{
    public StatType Stat;
    public long Start;
    public long End;
}

public enum BattleEntityType
{
    None = 0,
    Character = 16777216,
    SkillActor = 33554432,
    Obstacle = 67108864,
    Point = 134217728,
    Projectile = 268435456,
    EffectArea = 536870912,
    Supporter = 1073741824,
    BattleItem = -2147483648,
}

public enum BattleLogCategory
{
    None = 0,
    Damage = 1,
    Heal = 2,
}

public enum BattleLogSourceType
{
    None = 0,
    Normal = 1,
    Ex = 2,
    Public = 3,
    Passive = 4,
    ExtraPassive = 5,
    Etc = 6,
}

public struct KillLog
{
    public int Frame;
    public EntityId EntityId;
}

public struct SkillCostRegenSnapshot
{
    public long Frame;
    public float Regen;
}

public static class ProtoDefine
{
    public static readonly Dictionary<Protocol, Type> requestType = new Dictionary<Protocol, Type>()
    {
        [Protocol.System_Version] = typeof(SystemVersionRequest),
        [Protocol.Session_Info] = typeof(SessionInfoRequest),
        [Protocol.NetworkTime_Sync] = typeof(NetworkTimeSyncRequest),
        [Protocol.Audit_GachaStatistics] = typeof(AuditGachaStatisticsRequest),
        [Protocol.Account_Create] = typeof(AccountCreateRequest),
        [Protocol.Account_Nickname] = typeof(AccountNicknameRequest),
        [Protocol.Account_Auth] = typeof(AccountAuthRequest),
        [Protocol.Account_CurrencySync] = typeof(AccountCurrencySyncRequest),
        [Protocol.Account_SetRepresentCharacterAndComment] = typeof(AccountSetRepresentCharacterAndCommentRequest),
        [Protocol.Account_GetTutorial] = typeof(AccountGetTutorialRequest),
        [Protocol.Account_SetTutorial] = typeof(AccountSetTutorialRequest),
        [Protocol.Account_PassCheck] = typeof(AccountPassCheckRequest),
        [Protocol.Account_CheckYostar] = typeof(AccountCheckYostarRequest),
        [Protocol.Account_CallName] = typeof(AccountCallNameRequest),
        [Protocol.Account_BirthDay] = typeof(AccountBirthDayRequest),
        [Protocol.Account_Auth2] = typeof(AccountAuth2Request),
        [Protocol.Account_LinkReward] = typeof(AccountLinkRewardRequest),
        [Protocol.Account_CheckNexon] = typeof(AccountCheckNexonRequest),
        [Protocol.Account_DetachNexon] = typeof(AccountDetachNexonRequest),
        [Protocol.Account_ReportXignCodeCheater] = typeof(AccountReportXignCodeCheaterRequest),
        [Protocol.Account_DismissRepurchasablePopup] = typeof(AccountDismissRepurchasablePopupRequest),
        [Protocol.Account_InvalidateToken] = typeof(AccountInvalidateTokenRequest),
        [Protocol.Account_LoginSync] = typeof(AccountLoginSyncRequest),
        [Protocol.Account_VerifyCheckAdultAgree] = typeof(AccountVerifyAdultCheckRequest),
        [Protocol.Character_List] = typeof(CharacterListRequest),
        [Protocol.Character_Transcendence] = typeof(CharacterTranscendenceRequest),
        [Protocol.Character_ExpGrowth] = typeof(CharacterExpGrowthRequest),
        [Protocol.Character_FavorGrowth] = typeof(CharacterFavorGrowthRequest),
        [Protocol.Character_UnlockWeapon] = typeof(CharacterUnlockWeaponRequest),
        [Protocol.Character_WeaponExpGrowth] = typeof(CharacterWeaponExpGrowthRequest),
        [Protocol.Character_WeaponTranscendence] = typeof(CharacterWeaponTranscendenceRequest),
        [Protocol.Character_SetFavorites] = typeof(CharacterSetFavoritesRequest),
        [Protocol.Equipment_BatchGrowth] = typeof(EquipmentBatchGrowthRequest),
        [Protocol.Item_List] = typeof(ItemListRequest),
        [Protocol.Item_Sell] = typeof(ItemSellRequest),
        [Protocol.Item_Consume] = typeof(ItemConsumeRequest),
        [Protocol.Item_Lock] = typeof(ItemLockRequest),
        [Protocol.Item_BulkConsume] = typeof(ItemBulkConsumeRequest),
        [Protocol.Item_SelectTicket] = typeof(ItemSelectTicketRequest),
        [Protocol.Echelon_List] = typeof(EchelonListRequest),
        [Protocol.Echelon_Save] = typeof(EchelonSaveRequest),
        [Protocol.Echelon_PresetList] = typeof(EchelonPresetListRequest),
        [Protocol.Echelon_PresetSave] = typeof(EchelonPresetSaveRequest),
        [Protocol.Echelon_PresetGroupRename] = typeof(EchelonPresetGroupRenameRequest),
        [Protocol.Campaign_List] = typeof(CampaignListRequest),
        [Protocol.Campaign_EnterMainStage] = typeof(CampaignEnterMainStageRequest),
        [Protocol.Campaign_ConfirmMainStage] = typeof(CampaignConfirmMainStageRequest),
        [Protocol.Campaign_DeployEchelon] = typeof(CampaignDeployEchelonRequest),
        [Protocol.Campaign_WithdrawEchelon] = typeof(CampaignWithdrawEchelonRequest),
        [Protocol.Campaign_MapMove] = typeof(CampaignMapMoveRequest),
        [Protocol.Campaign_EndTurn] = typeof(CampaignEndTurnRequest),
        [Protocol.Campaign_EnterTactic] = typeof(CampaignEnterTacticRequest),
        [Protocol.Campaign_TacticResult] = typeof(CampaignTacticResultRequest),
        [Protocol.Campaign_Retreat] = typeof(CampaignRetreatRequest),
        [Protocol.Campaign_ChapterClearReward] = typeof(CampaignChapterClearRewardRequest),
        [Protocol.Campaign_Heal] = typeof(CampaignHealRequest),
        [Protocol.Campaign_EnterSubStage] = typeof(CampaignEnterSubStageRequest),
        [Protocol.Campaign_SubStageResult] = typeof(CampaignSubStageResultRequest),
        [Protocol.Campaign_Portal] = typeof(CampaignPortalRequest),
        [Protocol.Campaign_ConfirmTutorialStage] = typeof(CampaignConfirmTutorialStageRequest),
        [Protocol.Campaign_PurchasePlayCountHardStage] = typeof(CampaignPurchasePlayCountHardStageRequest),
        [Protocol.Campaign_EnterTutorialStage] = typeof(CampaignEnterTutorialStageRequest),
        [Protocol.Campaign_TutorialStageResult] = typeof(CampaignTutorialStageResultRequest),
        [Protocol.Campaign_RestartMainStage] = typeof(CampaignRestartMainStageRequest),
        [Protocol.Mail_List] = typeof(MailListRequest),
        [Protocol.Mail_Check] = typeof(MailCheckRequest),
        [Protocol.Mail_Receive] = typeof(MailReceiveRequest),
        [Protocol.Mission_List] = typeof(MissionListRequest),
        [Protocol.Mission_Reward] = typeof(MissionRewardRequest),
        [Protocol.Mission_MultipleReward] = typeof(MissionMultipleRewardRequest),
        [Protocol.Mission_Sync] = typeof(MissionSyncRequest),
        [Protocol.Attendance_Reward] = typeof(AttendanceRewardRequest),
        [Protocol.Shop_BuyMerchandise] = typeof(ShopBuyMerchandiseRequest),
        [Protocol.Shop_BuyGacha] = typeof(ShopBuyGachaRequest),
        [Protocol.Shop_List] = typeof(ShopListRequest),
        [Protocol.Shop_Refresh] = typeof(ShopRefreshRequest),
        [Protocol.Shop_BuyEligma] = typeof(ShopBuyEligmaRequest),
        [Protocol.Shop_BuyGacha2] = typeof(ShopBuyGacha2Request),
        [Protocol.Shop_GachaRecruitList] = typeof(ShopGachaRecruitListRequest),
        [Protocol.Shop_BuyRefreshMerchandise] = typeof(ShopBuyRefreshMerchandiseRequest),
        [Protocol.Shop_BuyGacha3] = typeof(ShopBuyGacha3Request),
        [Protocol.Shop_BuyAP] = typeof(ShopBuyAPRequest),
        [Protocol.Recipe_Craft] = typeof(RecipeCraftRequest),
        [Protocol.MemoryLobby_List] = typeof(MemoryLobbyListRequest),
        [Protocol.MemoryLobby_SetMain] = typeof(MemoryLobbySetMainRequest),
        [Protocol.MemoryLobby_UpdateLobbyMode] = typeof(MemoryLobbyUpdateLobbyModeRequest),
        [Protocol.MemoryLobby_Interact] = typeof(MemoryLobbyInteractRequest),
        [Protocol.CumulativeTimeReward_List] = typeof(CumulativeTimeRewardListRequest),
        [Protocol.OpenCondition_List] = typeof(OpenConditionListRequest),
        [Protocol.OpenCondition_Set] = typeof(OpenConditionSetRequest),
        [Protocol.OpenCondition_EventList] = typeof(OpenConditionEventListRequest),
        [Protocol.Toast_List] = typeof(ToastListRequest),
        [Protocol.Raid_List] = typeof(RaidListRequest),
        [Protocol.Raid_CompleteList] = typeof(RaidCompleteListRequest),
        [Protocol.Raid_Detail] = typeof(RaidDetailRequest),
        [Protocol.Raid_Search] = typeof(RaidSearchRequest),
        [Protocol.Raid_CreateBattle] = typeof(RaidCreateBattleRequest),
        [Protocol.Raid_EnterBattle] = typeof(RaidEnterBattleRequest),
        [Protocol.Raid_BattleUpdate] = typeof(RaidBattleUpdateRequest),
        [Protocol.Raid_EndBattle] = typeof(RaidEndBattleRequest),
        [Protocol.Raid_Reward] = typeof(RaidRewardRequest),
        [Protocol.Raid_RewardAll] = typeof(RaidRewardAllRequest),
        [Protocol.Raid_Share] = typeof(RaidShareRequest),
        [Protocol.Raid_SeasonReward] = typeof(RaidSeasonRewardRequest),
        [Protocol.Raid_Lobby] = typeof(RaidLobbyRequest),
        [Protocol.Raid_GiveUp] = typeof(RaidGiveUpRequest),
        [Protocol.Raid_OpponentList] = typeof(RaidOpponentListRequest),
        [Protocol.Raid_RankingReward] = typeof(RaidRankingRewardRequest),
        [Protocol.Raid_Login] = typeof(RaidLoginRequest),
        [Protocol.Raid_Sweep] = typeof(RaidSweepRequest),
        [Protocol.Raid_GetBestTeam] = typeof(RaidGetBestTeamRequest),
        [Protocol.SkipHistory_List] = typeof(SkipHistoryListRequest),
        [Protocol.SkipHistory_Save] = typeof(SkipHistorySaveRequest),
        [Protocol.Scenario_List] = typeof(ScenarioListRequest),
        [Protocol.Scenario_Clear] = typeof(ScenarioClearRequest),
        [Protocol.Scenario_GroupHistoryUpdate] = typeof(ScenarioGroupHistoryUpdateRequest),
        [Protocol.Scenario_Skip] = typeof(ScenarioSkipRequest),
        [Protocol.Scenario_Select] = typeof(ScenarioSelectRequest),
        [Protocol.Scenario_AccountStudentChange] = typeof(ScenarioAccountStudentChangeRequest),
        [Protocol.Scenario_LobbyStudentChange] = typeof(ScenarioLobbyStudentChangeRequest),
        [Protocol.Scenario_SpecialLobbyChange] = typeof(ScenarioSpecialLobbyChangeRequest),
        [Protocol.Scenario_Enter] = typeof(ScenarioEnterRequest),
        [Protocol.Scenario_EnterMainStage] = typeof(ScenarioEnterMainStageRequest),
        [Protocol.Scenario_ConfirmMainStage] = typeof(ScenarioConfirmMainStageRequest),
        [Protocol.Scenario_DeployEchelon] = typeof(ScenarioDeployEchelonRequest),
        [Protocol.Scenario_WithdrawEchelon] = typeof(ScenarioWithdrawEchelonRequest),
        [Protocol.Scenario_MapMove] = typeof(ScenarioMapMoveRequest),
        [Protocol.Scenario_EndTurn] = typeof(ScenarioEndTurnRequest),
        [Protocol.Scenario_EnterTactic] = typeof(ScenarioEnterTacticRequest),
        [Protocol.Scenario_TacticResult] = typeof(ScenarioTacticResultRequest),
        [Protocol.Scenario_Retreat] = typeof(ScenarioRetreatRequest),
        [Protocol.Scenario_Portal] = typeof(ScenarioPortalRequest),
        [Protocol.Scenario_RestartMainStage] = typeof(ScenarioRestartMainStageRequest),
        [Protocol.Scenario_SkipMainStage] = typeof(ScenarioSkipMainStageRequest),
        [Protocol.Cafe_Get] = typeof(CafeGetInfoRequest),
        [Protocol.Cafe_Ack] = typeof(CafeAckRequest),
        [Protocol.Cafe_ListPreset] = typeof(CafeListPresetRequest),
        [Protocol.Cafe_RenamePreset] = typeof(CafeRenamePresetRequest),
        [Protocol.Cafe_ClearPreset] = typeof(CafeClearPresetRequest),
        [Protocol.Cafe_UpdatePresetFurniture] = typeof(CafeUpdatePresetFurnitureRequest),
        [Protocol.Cafe_ApplyPreset] = typeof(CafeApplyPresetRequest),
        [Protocol.Cafe_RankUp] = typeof(CafeRankUpRequest),
        [Protocol.Cafe_ReceiveCurrency] = typeof(CafeReceiveCurrencyRequest),
        [Protocol.Cafe_GiveGift] = typeof(CafeGiveGiftRequest),
        [Protocol.Cafe_SummonCharacter] = typeof(CafeSummonCharacterRequest),
        [Protocol.Cafe_TrophyHistory] = typeof(CafeTrophyHistoryRequest),
        [Protocol.Craft_SelectNode] = typeof(CraftSelectNodeRequest),
        [Protocol.Craft_UpdateNodeLevel] = typeof(CraftUpdateNodeLevelRequest),
        [Protocol.Craft_BeginProcess] = typeof(CraftBeginProcessRequest),
        [Protocol.Craft_CompleteProcess] = typeof(CraftCompleteProcessRequest),
        [Protocol.Craft_Reward] = typeof(CraftRewardRequest),
        [Protocol.Craft_ShiftingBeginProcess] = typeof(CraftShiftingBeginProcessRequest),
        [Protocol.Craft_ShiftingCompleteProcess] = typeof(CraftShiftingCompleteProcessRequest),
        [Protocol.Craft_ShiftingReward] = typeof(CraftShiftingRewardRequest),
        [Protocol.Arena_EnterLobby] = typeof(ArenaEnterLobbyRequest),
        [Protocol.Arena_Login] = typeof(ArenaLoginRequest),
        [Protocol.Arena_SettingChange] = typeof(ArenaSettingChangeRequest),
        [Protocol.Arena_OpponentList] = typeof(ArenaOpponentListRequest),
        [Protocol.Arena_EnterBattle] = typeof(ArenaEnterBattleRequest),
        [Protocol.Arena_EnterBattlePart1] = typeof(ArenaEnterBattlePart1Request),
        [Protocol.Arena_EnterBattlePart2] = typeof(ArenaEnterBattlePart2Request),
        [Protocol.Arena_BattleResult] = typeof(ArenaBattleResultRequest),
        [Protocol.Arena_CumulativeTimeReward] = typeof(ArenaCumulativeTimeRewardRequest),
        [Protocol.Arena_DailyReward] = typeof(ArenaDailyRewardRequest),
        [Protocol.Arena_RankList] = typeof(ArenaRankListRequest),
        [Protocol.Arena_History] = typeof(ArenaHistoryRequest),
        [Protocol.Arena_CheckSeasonCloseReward] = typeof(ArenaCheckSeasonCloseRewardRequest),
        [Protocol.Arena_SyncEchelonSettingTime] = typeof(ArenaSyncEchelonSettingTimeRequest),
        [Protocol.WeekDungeon_List] = typeof(WeekDungeonListRequest),
        [Protocol.WeekDungeon_EnterBattle] = typeof(WeekDungeonEnterBattleRequest),
        [Protocol.WeekDungeon_BattleResult] = typeof(WeekDungeonBattleResultRequest),
        [Protocol.WeekDungeon_Retreat] = typeof(WeekDungeonRetreatRequest),
        [Protocol.Academy_GetInfo] = typeof(AcademyGetInfoRequest),
        [Protocol.Academy_AttendSchedule] = typeof(AcademyAttendScheduleRequest),
        [Protocol.Event_RewardIncrease] = typeof(EventRewardIncreaseRequest),
        [Protocol.ContentSave_Get] = typeof(ContentSaveGetRequest),
        [Protocol.ContentSave_Discard] = typeof(ContentSaveDiscardRequest),
        [Protocol.Clan_Lobby] = typeof(ClanLobbyRequest),
        [Protocol.Clan_Login] = typeof(ClanLoginRequest),
        [Protocol.Clan_Search] = typeof(ClanSearchRequest),
        [Protocol.Clan_Create] = typeof(ClanCreateRequest),
        [Protocol.Clan_Member] = typeof(ClanMemberRequest),
        [Protocol.Clan_Applicant] = typeof(ClanApplicantRequest),
        [Protocol.Clan_Join] = typeof(ClanJoinRequest),
        [Protocol.Clan_Quit] = typeof(ClanQuitRequest),
        [Protocol.Clan_Permit] = typeof(ClanPermitRequest),
        [Protocol.Clan_Kick] = typeof(ClanKickRequest),
        [Protocol.Clan_Setting] = typeof(ClanSettingRequest),
        [Protocol.Clan_Confer] = typeof(ClanConferRequest),
        [Protocol.Clan_Dismiss] = typeof(ClanDismissRequest),
        [Protocol.Clan_AutoJoin] = typeof(ClanAutoJoinRequest),
        [Protocol.Clan_MemberList] = typeof(ClanMemberListRequest),
        [Protocol.Clan_CancelApply] = typeof(ClanCancelApplyRequest),
        [Protocol.Clan_MyAssistList] = typeof(ClanMyAssistListRequest),
        [Protocol.Clan_SetAssist] = typeof(ClanSetAssistRequest),
        [Protocol.Clan_ChatLog] = typeof(ClanChatLogRequest),
        [Protocol.Clan_Check] = typeof(ClanCheckRequest),
        [Protocol.Clan_AllAssistList] = typeof(ClanAllAssistListRequest),
        [Protocol.Billing_TransactionStartByYostar] = typeof(BillingTransactionStartByYostarRequest),
        [Protocol.Billing_TransactionEndByYostar] = typeof(BillingTransactionEndByYostarRequest),
        [Protocol.Billing_PurchaseListByYostar] = typeof(BillingPurchaseListByYostarRequest),
        [Protocol.Billing_PurchaseCashShopVerifyByNexon] = typeof(BillingPurchaseCashShopVerifyByNexonRequest),
        [Protocol.Billing_PurchaseListByNexon] = typeof(BillingPurchaseListByNexonRequest),
        [Protocol.EventContent_AdventureList] = typeof(EventContentAdventureListRequest),
        [Protocol.EventContent_EnterMainStage] = typeof(EventContentEnterMainStageRequest),
        [Protocol.EventContent_ConfirmMainStage] = typeof(EventContentConfirmMainStageRequest),
        [Protocol.EventContent_EnterTactic] = typeof(EventContentEnterTacticRequest),
        [Protocol.EventContent_TacticResult] = typeof(EventContentTacticResultRequest),
        [Protocol.EventContent_EnterSubStage] = typeof(EventContentEnterSubStageRequest),
        [Protocol.EventContent_SubStageResult] = typeof(EventContentSubStageResultRequest),
        [Protocol.EventContent_DeployEchelon] = typeof(EventContentDeployEchelonRequest),
        [Protocol.EventContent_WithdrawEchelon] = typeof(EventContentWithdrawEchelonRequest),
        [Protocol.EventContent_MapMove] = typeof(EventContentMapMoveRequest),
        [Protocol.EventContent_EndTurn] = typeof(EventContentEndTurnRequest),
        [Protocol.EventContent_Retreat] = typeof(EventContentRetreatRequest),
        [Protocol.EventContent_Portal] = typeof(EventContentPortalRequest),
        [Protocol.EventContent_PurchasePlayCountHardStage] = typeof(EventContentPurchasePlayCountHardStageRequest),
        [Protocol.EventContent_ShopList] = typeof(EventContentShopListRequest),
        [Protocol.EventContent_ShopRefresh] = typeof(EventContentShopRefreshRequest),
        [Protocol.EventContent_ReceiveStageTotalReward] = typeof(EventContentReceiveStageTotalRewardRequest),
        [Protocol.EventContent_EnterMainGroundStage] = typeof(EventContentEnterMainGroundStageRequest),
        [Protocol.EventContent_MainGroundStageResult] = typeof(EventContentMainGroundStageResultRequest),
        [Protocol.EventContent_ShopBuyMerchandise] = typeof(EventContentShopBuyMerchandiseRequest),
        [Protocol.EventContent_ShopBuyRefreshMerchandise] = typeof(EventContentShopBuyRefreshMerchandiseRequest),
        [Protocol.EventContent_SelectBuff] = typeof(EventContentSelectBuffRequest),
        [Protocol.EventContent_BoxGachaShopList] = typeof(EventContentBoxGachaShopListRequest),
        [Protocol.EventContent_BoxGachaShopPurchase] = typeof(EventContentBoxGachaShopPurchaseRequest),
        [Protocol.EventContent_BoxGachaShopRefresh] = typeof(EventContentBoxGachaShopRefreshRequest),
        [Protocol.EventContent_CollectionList] = typeof(EventContentCollectionListRequest),
        [Protocol.EventContent_CollectionForMission] = typeof(EventContentCollectionForMissionRequest),
        [Protocol.EventContent_ScenarioGroupHistoryUpdate] = typeof(EventContentScenarioGroupHistoryUpdateRequest),
        [Protocol.EventContent_CardShopList] = typeof(EventContentCardShopListRequest),
        [Protocol.EventContent_CardShopShuffle] = typeof(EventContentCardShopShuffleRequest),
        [Protocol.EventContent_CardShopPurchase] = typeof(EventContentCardShopPurchaseRequest),
        [Protocol.EventContent_RestartMainStage] = typeof(EventContentRestartMainStageRequest),
        [Protocol.EventContent_LocationGetInfo] = typeof(EventContentLocationGetInfoRequest),
        [Protocol.EventContent_LocationAttendSchedule] = typeof(EventContentLocationAttendScheduleRequest),
        [Protocol.EventContent_FortuneGachaPurchase] = typeof(EventContentFortuneGachaPurchaseRequest),
        [Protocol.EventContent_SubEventLobby] = typeof(EventContentSubEventLobbyRequest),
        [Protocol.EventContent_EnterStoryStage] = typeof(EventContentEnterStoryStageRequest),
        [Protocol.EventContent_StoryStageResult] = typeof(EventContentStoryStageResultRequest),
        [Protocol.EventContent_DiceRaceLobby] = typeof(EventContentDiceRaceLobbyRequest),
        [Protocol.EventContent_DiceRaceRoll] = typeof(EventContentDiceRaceRollRequest),
        [Protocol.EventContent_DiceRaceLapReward] = typeof(EventContentDiceRaceLapRewardRequest),
        [Protocol.EventContent_PermanentList] = typeof(EventContentPermanentListRequest),
        [Protocol.TTS_GetFile] = typeof(TTSGetFileRequest),
        [Protocol.TTS_GetKana] = typeof(TTSGetKanaRequest),
        [Protocol.ContentLog_UIOpenStatistics] = typeof(ContentLogUIOpenStatisticsRequest),
        [Protocol.MomoTalk_OutLine] = typeof(MomoTalkOutLineRequest),
        [Protocol.MomoTalk_MessageList] = typeof(MomoTalkMessageListRequest),
        [Protocol.MomoTalk_Read] = typeof(MomoTalkReadRequest),
        [Protocol.MomoTalk_FavorSchedule] = typeof(MomoTalkFavorScheduleRequest),
        [Protocol.ClearDeck_List] = typeof(ClearDeckListRequest),
        [Protocol.MiniGame_StageList] = typeof(MiniGameStageListRequest),
        [Protocol.MiniGame_EnterStage] = typeof(MiniGameEnterStageRequest),
        [Protocol.MiniGame_Result] = typeof(MiniGameResultRequest),
        [Protocol.MiniGame_MissionList] = typeof(MiniGameMissionListRequest),
        [Protocol.MiniGame_MissionReward] = typeof(MiniGameMissionRewardRequest),
        [Protocol.MiniGame_MissionMultipleReward] = typeof(MiniGameMissionMultipleRewardRequest),
        [Protocol.Notification_LobbyCheck] = typeof(NotificationLobbyCheckRequest),
        [Protocol.Notification_EventContentReddotCheck] = typeof(NotificationEventContentReddotRequest),
        [Protocol.ProofToken_RequestQuestion] = typeof(ProofTokenRequestQuestionRequest),
        [Protocol.ProofToken_Submit] = typeof(ProofTokenSubmitRequest),
        [Protocol.SchoolDungeon_List] = typeof(SchoolDungeonListRequest),
        [Protocol.SchoolDungeon_EnterBattle] = typeof(SchoolDungeonEnterBattleRequest),
        [Protocol.SchoolDungeon_BattleResult] = typeof(SchoolDungeonBattleResultRequest),
        [Protocol.SchoolDungeon_Retreat] = typeof(SchoolDungeonRetreatRequest),
        [Protocol.TimeAttackDungeon_Lobby] = typeof(TimeAttackDungeonLobbyRequest),
        [Protocol.TimeAttackDungeon_CreateBattle] = typeof(TimeAttackDungeonCreateBattleRequest),
        [Protocol.TimeAttackDungeon_EnterBattle] = typeof(TimeAttackDungeonEnterBattleRequest),
        [Protocol.TimeAttackDungeon_EndBattle] = typeof(TimeAttackDungeonEndBattleRequest),
        [Protocol.TimeAttackDungeon_Sweep] = typeof(TimeAttackDungeonSweepRequest),
        [Protocol.TimeAttackDungeon_GiveUp] = typeof(TimeAttackDungeonGiveUpRequest),
        [Protocol.TimeAttackDungeon_Login] = typeof(TimeAttackDungeonLoginRequest),
        [Protocol.WorldRaid_Lobby] = typeof(WorldRaidLobbyRequest),
        [Protocol.WorldRaid_BossList] = typeof(WorldRaidBossListRequest),
        [Protocol.WorldRaid_EnterBattle] = typeof(WorldRaidEnterBattleRequest),
        [Protocol.WorldRaid_BattleResult] = typeof(WorldRaidBattleResultRequest),
        [Protocol.WorldRaid_ReceiveReward] = typeof(WorldRaidReceiveRewardRequest),
        [Protocol.ResetableContent_Get] = typeof(ResetableContentGetRequest),
        [Protocol.Conquest_GetInfo] = typeof(ConquestGetInfoRequest),
        [Protocol.Conquest_Conquer] = typeof(ConquestConquerRequest),
        [Protocol.Conquest_ConquerWithBattleStart] = typeof(ConquestConquerWithBattleStartRequest),
        [Protocol.Conquest_ConquerWithBattleResult] = typeof(ConquestConquerWithBattleResultRequest),
        [Protocol.Conquest_ManageBase] = typeof(ConquestManageBaseRequest),
        [Protocol.Conquest_UpgradeBase] = typeof(ConquestUpgradeBaseRequest),
        [Protocol.Conquest_TakeEventObject] = typeof(ConquestTakeEventObjectRequest),
        [Protocol.Conquest_EventObjectBattleStart] = typeof(ConquestEventObjectBattleStartRequest),
        [Protocol.Conquest_EventObjectBattleResult] = typeof(ConquestEventObjectBattleResultRequest),
        [Protocol.Conquest_NormalizeEchelon] = typeof(ConquestNormalizeEchelonRequest),
        [Protocol.Conquest_Check] = typeof(ConquestCheckRequest),
        [Protocol.Friend_List] = typeof(FriendListRequest),
        [Protocol.Friend_Remove] = typeof(FriendRemoveRequest),
        [Protocol.Friend_GetFriendDetailedInfo] = typeof(FriendGetFriendDetailedInfoRequest),
        [Protocol.Friend_GetIdCard] = typeof(FriendGetIdCardRequest),
        [Protocol.Friend_SetIdCard] = typeof(FriendSetIdCardRequest),
        [Protocol.Friend_Search] = typeof(FriendSearchRequest),
        [Protocol.Friend_SendFriendRequest] = typeof(FriendSendFriendRequestRequest),
        [Protocol.Friend_AcceptFriendRequest] = typeof(FriendAcceptFriendRequestRequest),
        [Protocol.Friend_DeclineFriendRequest] = typeof(FriendDeclineFriendRequestRequest),
        [Protocol.Friend_CancelFriendRequest] = typeof(FriendCancelFriendRequestRequest),
        [Protocol.Friend_Check] = typeof(FriendCheckRequest),
        [Protocol.CharacterGear_List] = typeof(CharacterGearListRequest),
        [Protocol.CharacterGear_Unlock] = typeof(CharacterGearUnlockRequest),
        [Protocol.CharacterGear_TierUp] = typeof(CharacterGearTierUpRequest),
        [Protocol.Queuing_GetTicket] = typeof(QueuingGetTicketRequest),
        [Protocol.Management_BannerList] = typeof(ManagementBannerListRequest),
        [Protocol.Management_ContentsLockList] = typeof(ManagementContentsLockListRequest),
        [Protocol.Common_Cheat] = typeof(CommonCheatRequest),
    };
    public static readonly Dictionary<Protocol, Type> responseType = new Dictionary<Protocol, Type>()
    {
        [Protocol.System_Version] = typeof(SystemVersionResponse),
        [Protocol.Session_Info] = typeof(SessionInfoResponse),
        [Protocol.NetworkTime_SyncReply] = typeof(NetworkTimeSyncResponse),
        [Protocol.Audit_GachaStatistics] = typeof(AuditGachaStatisticsResponse),
        [Protocol.Account_Create] = typeof(AccountCreateResponse),
        [Protocol.Account_Nickname] = typeof(AccountNicknameResponse),
        [Protocol.Account_Auth] = typeof(AccountAuthResponse),
        [Protocol.Account_CurrencySync] = typeof(AccountCurrencySyncResponse),
        [Protocol.Account_SetRepresentCharacterAndComment] = typeof(AccountSetRepresentCharacterAndCommentResponse),
        [Protocol.Account_GetTutorial] = typeof(AccountGetTutorialResponse),
        [Protocol.Account_SetTutorial] = typeof(AccountSetTutorialResponse),
        [Protocol.Account_PassCheck] = typeof(AccountPassCheckResponse),
        [Protocol.Account_CheckYostar] = typeof(AccountCheckYostarResponse),
        [Protocol.Account_CallName] = typeof(AccountCallNameResponse),
        [Protocol.Account_BirthDay] = typeof(AccountBirthDayResponse),
        [Protocol.Account_Auth2] = typeof(AccountAuth2Response),
        [Protocol.Account_LinkReward] = typeof(AccountLinkRewardResponse),
        [Protocol.Account_CheckNexon] = typeof(AccountCheckNexonResponse),
        [Protocol.Account_DetachNexon] = typeof(AccountDetachNexonResponse),
        [Protocol.Account_ReportXignCodeCheater] = typeof(AccountReportXignCodeCheaterResponse),
        [Protocol.Account_DismissRepurchasablePopup] = typeof(AccountDismissRepurchasablePopupResponse),
        [Protocol.Account_InvalidateToken] = typeof(AccountInvalidateTokenResponse),
        [Protocol.Account_LoginSync] = typeof(AccountLoginSyncResponse),
        [Protocol.Account_VerifyCheckAdultAgree] = typeof(AccountVerifyAdultCheckResponse),
        [Protocol.Character_List] = typeof(CharacterListResponse),
        [Protocol.Character_Transcendence] = typeof(CharacterTranscendenceResponse),
        [Protocol.Character_ExpGrowth] = typeof(CharacterExpGrowthResponse),
        [Protocol.Character_FavorGrowth] = typeof(CharacterFavorGrowthResponse),
        [Protocol.Character_UnlockWeapon] = typeof(CharacterUnlockWeaponResponse),
        [Protocol.Character_WeaponExpGrowth] = typeof(CharacterWeaponExpGrowthResponse),
        [Protocol.Character_WeaponTranscendence] = typeof(CharacterWeaponTranscendenceResponse),
        [Protocol.Character_SetFavorites] = typeof(CharacterSetFavoritesResponse),
        [Protocol.Equipment_BatchGrowth] = typeof(EquipmentBatchGrowthResponse),
        [Protocol.Item_List] = typeof(ItemListResponse),
        [Protocol.Item_Sell] = typeof(ItemSellResponse),
        [Protocol.Item_Consume] = typeof(ItemConsumeResponse),
        [Protocol.Item_Lock] = typeof(ItemLockResponse),
        [Protocol.Item_BulkConsume] = typeof(ItemBulkConsumeResponse),
        [Protocol.Item_SelectTicket] = typeof(ItemSelectTicketResponse),
        [Protocol.Echelon_List] = typeof(EchelonListResponse),
        [Protocol.Echelon_Save] = typeof(EchelonSaveResponse),
        [Protocol.Echelon_PresetList] = typeof(EchelonPresetListResponse),
        [Protocol.Echelon_PresetSave] = typeof(EchelonPresetSaveResponse),
        [Protocol.Echelon_PresetGroupRename] = typeof(EchelonPresetGroupRenameResponse),
        [Protocol.Campaign_List] = typeof(CampaignListResponse),
        [Protocol.Campaign_EnterMainStage] = typeof(CampaignEnterMainStageResponse),
        [Protocol.Campaign_ConfirmMainStage] = typeof(CampaignConfirmMainStageResponse),
        [Protocol.Campaign_DeployEchelon] = typeof(CampaignDeployEchelonResponse),
        [Protocol.Campaign_WithdrawEchelon] = typeof(CampaignWithdrawEchelonResponse),
        [Protocol.Campaign_MapMove] = typeof(CampaignMapMoveResponse),
        [Protocol.Campaign_EndTurn] = typeof(CampaignEndTurnResponse),
        [Protocol.Campaign_EnterTactic] = typeof(CampaignEnterTacticResponse),
        [Protocol.Campaign_TacticResult] = typeof(CampaignTacticResultResponse),
        [Protocol.Campaign_Retreat] = typeof(CampaignRetreatResponse),
        [Protocol.Campaign_ChapterClearReward] = typeof(CampaignChapterClearRewardResponse),
        [Protocol.Campaign_Heal] = typeof(CampaignHealResponse),
        [Protocol.Campaign_EnterSubStage] = typeof(CampaignEnterSubStageResponse),
        [Protocol.Campaign_SubStageResult] = typeof(CampaignSubStageResultResponse),
        [Protocol.Campaign_Portal] = typeof(CampaignPortalResponse),
        [Protocol.Campaign_ConfirmTutorialStage] = typeof(CampaignConfirmTutorialStageResponse),
        [Protocol.Campaign_PurchasePlayCountHardStage] = typeof(CampaignPurchasePlayCountHardStageResponse),
        [Protocol.Campaign_EnterTutorialStage] = typeof(CampaignEnterTutorialStageResponse),
        [Protocol.Campaign_TutorialStageResult] = typeof(CampaignTutorialStageResultResponse),
        [Protocol.Campaign_RestartMainStage] = typeof(CampaignRestartMainStageResponse),
        [Protocol.Mail_List] = typeof(MailListResponse),
        [Protocol.Mail_Check] = typeof(MailCheckResponse),
        [Protocol.Mail_Receive] = typeof(MailReceiveResponse),
        [Protocol.Mission_List] = typeof(MissionListResponse),
        [Protocol.Mission_Reward] = typeof(MissionRewardResponse),
        [Protocol.Mission_MultipleReward] = typeof(MissionMultipleRewardResponse),
        [Protocol.Mission_Sync] = typeof(MissionSyncResponse),
        [Protocol.Attendance_Reward] = typeof(AttendanceRewardResponse),
        [Protocol.Shop_BuyMerchandise] = typeof(ShopBuyMerchandiseResponse),
        [Protocol.Shop_BuyGacha] = typeof(ShopBuyGachaResponse),
        [Protocol.Shop_List] = typeof(ShopListResponse),
        [Protocol.Shop_Refresh] = typeof(ShopRefreshResponse),
        [Protocol.Shop_BuyEligma] = typeof(ShopBuyEligmaResponse),
        [Protocol.Shop_BuyGacha2] = typeof(ShopBuyGacha2Response),
        [Protocol.Shop_GachaRecruitList] = typeof(ShopGachaRecruitListResponse),
        [Protocol.Shop_BuyRefreshMerchandise] = typeof(ShopBuyRefreshMerchandiseResponse),
        [Protocol.Shop_BuyGacha3] = typeof(ShopBuyGacha3Response),
        [Protocol.Shop_BuyAP] = typeof(ShopBuyAPResponse),
        [Protocol.Recipe_Craft] = typeof(RecipeCraftResponse),
        [Protocol.MemoryLobby_List] = typeof(MemoryLobbyListResponse),
        [Protocol.MemoryLobby_SetMain] = typeof(MemoryLobbySetMainResponse),
        [Protocol.MemoryLobby_UpdateLobbyMode] = typeof(MemoryLobbyUpdateLobbyModeResponse),
        [Protocol.MemoryLobby_Interact] = typeof(MemoryLobbyInteractResponse),
        [Protocol.CumulativeTimeReward_List] = typeof(CumulativeTimeRewardListResponse),
        [Protocol.OpenCondition_List] = typeof(OpenConditionListResponse),
        [Protocol.OpenCondition_Set] = typeof(OpenConditionSetResponse),
        [Protocol.OpenCondition_EventList] = typeof(OpenConditionEventListResponse),
        [Protocol.Toast_List] = typeof(ToastListResponse),
        [Protocol.Raid_List] = typeof(RaidListResponse),
        [Protocol.Raid_CompleteList] = typeof(RaidCompleteListResponse),
        [Protocol.Raid_Detail] = typeof(RaidDetailResponse),
        [Protocol.Raid_Search] = typeof(RaidSearchResponse),
        [Protocol.Raid_CreateBattle] = typeof(RaidCreateBattleResponse),
        [Protocol.Raid_EnterBattle] = typeof(RaidEnterBattleResponse),
        [Protocol.Raid_BattleUpdate] = typeof(RaidBattleUpdateResponse),
        [Protocol.Raid_EndBattle] = typeof(RaidEndBattleResponse),
        [Protocol.Raid_Reward] = typeof(RaidRewardResponse),
        [Protocol.Raid_RewardAll] = typeof(RaidRewardAllResponse),
        [Protocol.Raid_Share] = typeof(RaidShareResponse),
        [Protocol.Raid_SeasonReward] = typeof(RaidSeasonRewardResponse),
        [Protocol.Raid_Lobby] = typeof(RaidLobbyResponse),
        [Protocol.Raid_GiveUp] = typeof(RaidGiveUpResponse),
        [Protocol.Raid_OpponentList] = typeof(RaidOpponentListResponse),
        [Protocol.Raid_RankingReward] = typeof(RaidRankingRewardResponse),
        [Protocol.Raid_Login] = typeof(RaidLoginResponse),
        [Protocol.Raid_Sweep] = typeof(RaidSweepResponse),
        [Protocol.Raid_GetBestTeam] = typeof(RaidGetBestTeamResponse),
        [Protocol.SkipHistory_List] = typeof(SkipHistoryListResponse),
        [Protocol.SkipHistory_Save] = typeof(SkipHistorySaveResponse),
        [Protocol.Scenario_List] = typeof(ScenarioListResponse),
        [Protocol.Scenario_Clear] = typeof(ScenarioClearResponse),
        [Protocol.Scenario_GroupHistoryUpdate] = typeof(ScenarioGroupHistoryUpdateResponse),
        [Protocol.Scenario_Skip] = typeof(ScenarioSkipResponse),
        [Protocol.Scenario_Select] = typeof(ScenarioSelectResponse),
        [Protocol.Scenario_AccountStudentChange] = typeof(ScenarioAccountStudentChangeResponse),
        [Protocol.Scenario_LobbyStudentChange] = typeof(ScenarioLobbyStudentChangeResponse),
        [Protocol.Scenario_SpecialLobbyChange] = typeof(ScenarioSpecialLobbyChangeResponse),
        [Protocol.Scenario_Enter] = typeof(ScenarioEnterResponse),
        [Protocol.Scenario_EnterMainStage] = typeof(ScenarioEnterMainStageResponse),
        [Protocol.Scenario_ConfirmMainStage] = typeof(ScenarioConfirmMainStageResponse),
        [Protocol.Scenario_DeployEchelon] = typeof(ScenarioDeployEchelonResponse),
        [Protocol.Scenario_WithdrawEchelon] = typeof(ScenarioWithdrawEchelonResponse),
        [Protocol.Scenario_MapMove] = typeof(ScenarioMapMoveResponse),
        [Protocol.Scenario_EndTurn] = typeof(ScenarioEndTurnResponse),
        [Protocol.Scenario_EnterTactic] = typeof(ScenarioEnterTacticResponse),
        [Protocol.Scenario_TacticResult] = typeof(ScenarioTacticResultResponse),
        [Protocol.Scenario_Retreat] = typeof(ScenarioRetreatResponse),
        [Protocol.Scenario_Portal] = typeof(ScenarioPortalResponse),
        [Protocol.Scenario_RestartMainStage] = typeof(ScenarioRestartMainStageResponse),
        [Protocol.Scenario_SkipMainStage] = typeof(ScenarioSkipMainStageResponse),
        [Protocol.Cafe_Get] = typeof(CafeGetInfoResponse),
        [Protocol.Cafe_Ack] = typeof(CafeAckResponse),
        [Protocol.Cafe_ListPreset] = typeof(CafeListPresetResponse),
        [Protocol.Cafe_RenamePreset] = typeof(CafeRenamePresetResponse),
        [Protocol.Cafe_ClearPreset] = typeof(CafeClearPresetResponse),
        [Protocol.Cafe_UpdatePresetFurniture] = typeof(CafeUpdatePresetFurnitureResponse),
        [Protocol.Cafe_ApplyPreset] = typeof(CafeApplyPresetResponse),
        [Protocol.Cafe_RankUp] = typeof(CafeRankUpResponse),
        [Protocol.Cafe_ReceiveCurrency] = typeof(CafeReceiveCurrencyResponse),
        [Protocol.Cafe_GiveGift] = typeof(CafeGiveGiftResponse),
        [Protocol.Cafe_SummonCharacter] = typeof(CafeSummonCharacterResponse),
        [Protocol.Cafe_TrophyHistory] = typeof(CafeTrophyHistoryResponse),
        [Protocol.Craft_SelectNode] = typeof(CraftSelectNodeResponse),
        [Protocol.Craft_UpdateNodeLevel] = typeof(CraftUpdateNodeLevelResponse),
        [Protocol.Craft_BeginProcess] = typeof(CraftBeginProcessResponse),
        [Protocol.Craft_CompleteProcess] = typeof(CraftCompleteProcessResponse),
        [Protocol.Craft_Reward] = typeof(CraftRewardResponse),
        [Protocol.Craft_ShiftingBeginProcess] = typeof(CraftShiftingBeginProcessResponse),
        [Protocol.Craft_ShiftingCompleteProcess] = typeof(CraftShiftingCompleteProcessResponse),
        [Protocol.Craft_ShiftingReward] = typeof(CraftShiftingRewardResponse),
        [Protocol.Arena_EnterLobby] = typeof(ArenaEnterLobbyResponse),
        [Protocol.Arena_Login] = typeof(ArenaLoginResponse),
        [Protocol.Arena_SettingChange] = typeof(ArenaSettingChangeResponse),
        [Protocol.Arena_OpponentList] = typeof(ArenaOpponentListResponse),
        [Protocol.Arena_EnterBattle] = typeof(ArenaEnterBattleResponse),
        [Protocol.Arena_EnterBattlePart1] = typeof(ArenaEnterBattlePart1Response),
        [Protocol.Arena_EnterBattlePart2] = typeof(ArenaEnterBattlePart2Response),
        [Protocol.Arena_BattleResult] = typeof(ArenaBattleResultResponse),
        [Protocol.Arena_CumulativeTimeReward] = typeof(ArenaCumulativeTimeRewardResponse),
        [Protocol.Arena_DailyReward] = typeof(ArenaDailyRewardResponse),
        [Protocol.Arena_RankList] = typeof(ArenaRankListResponse),
        [Protocol.Arena_History] = typeof(ArenaHistoryResponse),
        [Protocol.Arena_CheckSeasonCloseReward] = typeof(ArenaCheckSeasonCloseRewardResponse),
        [Protocol.Arena_SyncEchelonSettingTime] = typeof(ArenaSyncEchelonSettingTimeResponse),
        [Protocol.WeekDungeon_List] = typeof(WeekDungeonListResponse),
        [Protocol.WeekDungeon_EnterBattle] = typeof(WeekDungeonEnterBattleResponse),
        [Protocol.WeekDungeon_BattleResult] = typeof(WeekDungeonBattleResultResponse),
        [Protocol.WeekDungeon_Retreat] = typeof(WeekDungeonRetreatResponse),
        [Protocol.Academy_GetInfo] = typeof(AcademyGetInfoResponse),
        [Protocol.Academy_AttendSchedule] = typeof(AcademyAttendScheduleResponse),
        [Protocol.Event_RewardIncrease] = typeof(EventRewardIncreaseResponse),
        [Protocol.ContentSave_Get] = typeof(ContentSaveGetResponse),
        [Protocol.ContentSave_Discard] = typeof(ContentSaveDiscardResponse),
        [Protocol.Clan_Lobby] = typeof(ClanLobbyResponse),
        [Protocol.Clan_Login] = typeof(ClanLoginResponse),
        [Protocol.Clan_Search] = typeof(ClanSearchResponse),
        [Protocol.Clan_Create] = typeof(ClanCreateResponse),
        [Protocol.Clan_Member] = typeof(ClanMemberResponse),
        [Protocol.Clan_Applicant] = typeof(ClanApplicantResponse),
        [Protocol.Clan_Join] = typeof(ClanJoinResponse),
        [Protocol.Clan_Quit] = typeof(ClanQuitResponse),
        [Protocol.Clan_Permit] = typeof(ClanPermitResponse),
        [Protocol.Clan_Kick] = typeof(ClanKickResponse),
        [Protocol.Clan_Setting] = typeof(ClanSettingResponse),
        [Protocol.Clan_Confer] = typeof(ClanConferResponse),
        [Protocol.Clan_Dismiss] = typeof(ClanDismissResponse),
        [Protocol.Clan_AutoJoin] = typeof(ClanAutoJoinResponse),
        [Protocol.Clan_MemberList] = typeof(ClanMemberListResponse),
        [Protocol.Clan_CancelApply] = typeof(ClanCancelApplyResponse),
        [Protocol.Clan_MyAssistList] = typeof(ClanMyAssistListResponse),
        [Protocol.Clan_SetAssist] = typeof(ClanSetAssistResponse),
        [Protocol.Clan_ChatLog] = typeof(ClanChatLogResponse),
        [Protocol.Clan_Check] = typeof(ClanCheckResponse),
        [Protocol.Clan_AllAssistList] = typeof(ClanAllAssistListResponse),
        [Protocol.Billing_TransactionStartByYostar] = typeof(BillingTransactionStartByYostarResponse),
        [Protocol.Billing_TransactionEndByYostar] = typeof(BillingTransactionEndByYostarResponse),
        [Protocol.Billing_PurchaseListByYostar] = typeof(BillingPurchaseListByYostarResponse),
        [Protocol.Billing_PurchaseCashShopVerifyByNexon] = typeof(BillingPurchaseCashShopVerifyByNexonResponse),
        [Protocol.Billing_PurchaseListByNexon] = typeof(BillingPurchaseListByNexonResponse),
        [Protocol.EventContent_AdventureList] = typeof(EventContentAdventureListResponse),
        [Protocol.EventContent_EnterMainStage] = typeof(EventContentEnterMainStageResponse),
        [Protocol.EventContent_ConfirmMainStage] = typeof(EventContentConfirmMainStageResponse),
        [Protocol.EventContent_EnterTactic] = typeof(EventContentEnterTacticResponse),
        [Protocol.EventContent_TacticResult] = typeof(EventContentTacticResultResponse),
        [Protocol.EventContent_EnterSubStage] = typeof(EventContentEnterSubStageResponse),
        [Protocol.EventContent_SubStageResult] = typeof(EventContentSubStageResultResponse),
        [Protocol.EventContent_DeployEchelon] = typeof(EventContentDeployEchelonResponse),
        [Protocol.EventContent_WithdrawEchelon] = typeof(EventContentWithdrawEchelonResponse),
        [Protocol.EventContent_MapMove] = typeof(EventContentMapMoveResponse),
        [Protocol.EventContent_EndTurn] = typeof(EventContentEndTurnResponse),
        [Protocol.EventContent_Retreat] = typeof(EventContentRetreatResponse),
        [Protocol.EventContent_Portal] = typeof(EventContentPortalResponse),
        [Protocol.EventContent_PurchasePlayCountHardStage] = typeof(EventContentPurchasePlayCountHardStageResponse),
        [Protocol.EventContent_ShopList] = typeof(EventContentShopListResponse),
        [Protocol.EventContent_ShopRefresh] = typeof(EventContentShopRefreshResponse),
        [Protocol.EventContent_ReceiveStageTotalReward] = typeof(EventContentReceiveStageTotalRewardResponse),
        [Protocol.EventContent_EnterMainGroundStage] = typeof(EventContentEnterMainGroundStageResponse),
        [Protocol.EventContent_MainGroundStageResult] = typeof(EventContentMainGroundStageResultResponse),
        [Protocol.EventContent_ShopBuyMerchandise] = typeof(EventContentShopBuyMerchandiseResponse),
        [Protocol.EventContent_ShopBuyRefreshMerchandise] = typeof(EventContentShopBuyRefreshMerchandiseResponse),
        [Protocol.EventContent_SelectBuff] = typeof(EventContentSelectBuffResponse),
        [Protocol.EventContent_BoxGachaShopList] = typeof(EventContentBoxGachaShopListResponse),
        [Protocol.EventContent_BoxGachaShopPurchase] = typeof(EventContentBoxGachaShopPurchaseResponse),
        [Protocol.EventContent_BoxGachaShopRefresh] = typeof(EventContentBoxGachaShopRefreshResponse),
        [Protocol.EventContent_CollectionList] = typeof(EventContentCollectionListResponse),
        [Protocol.EventContent_CollectionForMission] = typeof(EventContentCollectionForMissionResponse),
        [Protocol.EventContent_ScenarioGroupHistoryUpdate] = typeof(EventContentScenarioGroupHistoryUpdateResponse),
        [Protocol.EventContent_CardShopList] = typeof(EventContentCardShopListResponse),
        [Protocol.EventContent_CardShopShuffle] = typeof(EventContentCardShopShuffleResponse),
        [Protocol.EventContent_CardShopPurchase] = typeof(EventContentCardShopPurchaseResponse),
        [Protocol.EventContent_RestartMainStage] = typeof(EventContentRestartMainStageResponse),
        [Protocol.EventContent_LocationGetInfo] = typeof(EventContentLocationGetInfoResponse),
        [Protocol.EventContent_LocationAttendSchedule] = typeof(EventContentLocationAttendScheduleResponse),
        [Protocol.EventContent_FortuneGachaPurchase] = typeof(EventContentFortuneGachaPurchaseResponse),
        [Protocol.EventContent_SubEventLobby] = typeof(EventContentSubEventLobbyResponse),
        [Protocol.EventContent_EnterStoryStage] = typeof(EventContentEnterStoryStageResponse),
        [Protocol.EventContent_StoryStageResult] = typeof(EventContentStoryStageResultResponse),
        [Protocol.EventContent_DiceRaceLobby] = typeof(EventContentDiceRaceLobbyResponse),
        [Protocol.EventContent_DiceRaceRoll] = typeof(EventContentDiceRaceRollResponse),
        [Protocol.EventContent_DiceRaceLapReward] = typeof(EventContentDiceRaceLapRewardResponse),
        [Protocol.EventContent_PermanentList] = typeof(EventContentPermanentListResponse),
        [Protocol.TTS_GetFile] = typeof(TTSGetFileResponse),
        [Protocol.TTS_GetKana] = typeof(TTSGetKanaResponse),
        [Protocol.ContentLog_UIOpenStatistics] = typeof(ContentLogUIOpenStatisticsResponse),
        [Protocol.MomoTalk_OutLine] = typeof(MomoTalkOutLineResponse),
        [Protocol.MomoTalk_MessageList] = typeof(MomoTalkMessageListResponse),
        [Protocol.MomoTalk_Read] = typeof(MomoTalkReadResponse),
        [Protocol.MomoTalk_FavorSchedule] = typeof(MomoTalkFavorScheduleResponse),
        [Protocol.ClearDeck_List] = typeof(ClearDeckListResponse),
        [Protocol.MiniGame_StageList] = typeof(MiniGameStageListResponse),
        [Protocol.MiniGame_EnterStage] = typeof(MiniGameEnterStageResponse),
        [Protocol.MiniGame_Result] = typeof(MiniGameResultResponse),
        [Protocol.MiniGame_MissionList] = typeof(MiniGameMissionListResponse),
        [Protocol.MiniGame_MissionReward] = typeof(MiniGameMissionRewardResponse),
        [Protocol.MiniGame_MissionMultipleReward] = typeof(MiniGameMissionMultipleRewardResponse),
        [Protocol.Notification_LobbyCheck] = typeof(NotificationLobbyCheckResponse),
        [Protocol.Notification_EventContentReddotCheck] = typeof(NotificationEventContentReddotResponse),
        [Protocol.ProofToken_RequestQuestion] = typeof(ProofTokenRequestQuestionResponse),
        [Protocol.ProofToken_Submit] = typeof(ProofTokenSubmitResponse),
        [Protocol.SchoolDungeon_List] = typeof(SchoolDungeonListResponse),
        [Protocol.SchoolDungeon_EnterBattle] = typeof(SchoolDungeonEnterBattleResponse),
        [Protocol.SchoolDungeon_BattleResult] = typeof(SchoolDungeonBattleResultResponse),
        [Protocol.SchoolDungeon_Retreat] = typeof(SchoolDungeonRetreatResponse),
        [Protocol.TimeAttackDungeon_Lobby] = typeof(TimeAttackDungeonLobbyResponse),
        [Protocol.TimeAttackDungeon_CreateBattle] = typeof(TimeAttackDungeonCreateBattleResponse),
        [Protocol.TimeAttackDungeon_EnterBattle] = typeof(TimeAttackDungeonEnterBattleResponse),
        [Protocol.TimeAttackDungeon_EndBattle] = typeof(TimeAttackDungeonEndBattleResponse),
        [Protocol.TimeAttackDungeon_Sweep] = typeof(TimeAttackDungeonSweepResponse),
        [Protocol.TimeAttackDungeon_GiveUp] = typeof(TimeAttackDungeonGiveUpResponse),
        [Protocol.TimeAttackDungeon_Login] = typeof(TimeAttackDungeonLoginResponse),
        [Protocol.WorldRaid_Lobby] = typeof(WorldRaidLobbyResponse),
        [Protocol.WorldRaid_BossList] = typeof(WorldRaidBossListResponse),
        [Protocol.WorldRaid_EnterBattle] = typeof(WorldRaidEnterBattleResponse),
        [Protocol.WorldRaid_BattleResult] = typeof(WorldRaidBattleResultResponse),
        [Protocol.WorldRaid_ReceiveReward] = typeof(WorldRaidReceiveRewardResponse),
        [Protocol.ResetableContent_Get] = typeof(ResetableContentGetResponse),
        [Protocol.Conquest_GetInfo] = typeof(ConquestGetInfoResponse),
        [Protocol.Conquest_Conquer] = typeof(ConquestConquerResponse),
        [Protocol.Conquest_ConquerWithBattleStart] = typeof(ConquestConquerWithBattleStartResponse),
        [Protocol.Conquest_ConquerWithBattleResult] = typeof(ConquestConquerWithBattleResultResponse),
        [Protocol.Conquest_ManageBase] = typeof(ConquestManageBaseResponse),
        [Protocol.Conquest_UpgradeBase] = typeof(ConquestUpgradeBaseResponse),
        [Protocol.Conquest_TakeEventObject] = typeof(ConquestTakeEventObjectResponse),
        [Protocol.Conquest_EventObjectBattleStart] = typeof(ConquestEventObjectBattleStartResponse),
        [Protocol.Conquest_EventObjectBattleResult] = typeof(ConquestEventObjectBattleResultResponse),
        [Protocol.Conquest_NormalizeEchelon] = typeof(ConquestNormalizeEchelonResponse),
        [Protocol.Conquest_Check] = typeof(ConquestCheckResponse),
        [Protocol.Friend_List] = typeof(FriendListResponse),
        [Protocol.Friend_Remove] = typeof(FriendRemoveResponse),
        [Protocol.Friend_GetFriendDetailedInfo] = typeof(FriendGetFriendDetailedInfoResponse),
        [Protocol.Friend_GetIdCard] = typeof(FriendGetIdCardResponse),
        [Protocol.Friend_SetIdCard] = typeof(FriendSetIdCardResponse),
        [Protocol.Friend_Search] = typeof(FriendSearchResponse),
        [Protocol.Friend_SendFriendRequest] = typeof(FriendSendFriendRequestResponse),
        [Protocol.Friend_AcceptFriendRequest] = typeof(FriendAcceptFriendRequestResponse),
        [Protocol.Friend_DeclineFriendRequest] = typeof(FriendDeclineFriendRequestResponse),
        [Protocol.Friend_CancelFriendRequest] = typeof(FriendCancelFriendRequestResponse),
        [Protocol.Friend_Check] = typeof(FriendCheckResponse),
        [Protocol.CharacterGear_List] = typeof(CharacterGearListResponse),
        [Protocol.CharacterGear_Unlock] = typeof(CharacterGearUnlockResponse),
        [Protocol.CharacterGear_TierUp] = typeof(CharacterGearTierUpResponse),
        [Protocol.Queuing_GetTicket] = typeof(QueuingGetTicketResponse),
        [Protocol.Management_BannerList] = typeof(ManagementBannerListResponse),
        [Protocol.Management_ContentsLockList] = typeof(ManagementContentsLockListResponse),
        [Protocol.Common_Cheat] = typeof(CommonCheatResponse),
    };
}
