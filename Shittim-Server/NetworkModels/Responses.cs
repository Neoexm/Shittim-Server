namespace BlueArchiveAPI.NetworkModels;
using System.Collections.ObjectModel;

public class SystemVersionResponse : ResponsePacket, IResponse<SystemVersionRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.System_Version;
    public long CurrentVersion;
    public long MinimumVersion;
    public bool IsDevelopment;
}

public class SessionInfoResponse : ResponsePacket, IResponse<SessionInfoRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Session_Info;
    public SessionDB SessionDB;
}

public class NetworkTimeSyncResponse : ResponsePacket, IResponse<NetworkTimeSyncRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.NetworkTime_SyncReply;
    public long SendTick;
    public long ReceiveTick;
    public long EchoSendTick;
    public long EchoReceiveTick;
}

public class AuditGachaStatisticsResponse : ResponsePacket, IResponse<AuditGachaStatisticsRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Audit_GachaStatistics;
    public Dictionary<long, long> GachaResult;
}

public class AccountCreateResponse : ResponsePacket, IResponse<AccountCreateRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Create;
}

public class AccountNicknameResponse : ResponsePacket, IResponse<AccountNicknameRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Nickname;
    public AccountDB AccountDB;
}

public class AccountAuthResponse : ResponsePacket, IResponse<AccountAuthRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Auth;
    public long CurrentVersion;
    public long MinimumVersion;
    public bool IsDevelopment;
    public bool BattleValidation;
    public bool UpdateRequired;
    public string TTSCdnUri;
    public AccountDB AccountDB;
    public List<AttendanceBookReward> AttendanceBookRewards;
    public List<AttendanceHistoryDB> AttendanceHistoryDBs;
    public List<OpenConditionDB> OpenConditions;
    public List<PurchaseCountDB> RepurchasableMonthlyProductCountDBs;
    public List<ParcelInfo> MonthlyProductParcel;
    public List<ParcelInfo> MonthlyProductMail;
    public List<ParcelInfo> BiweeklyProductParcel;
    public List<ParcelInfo> BiweeklyProductMail;
    public List<ParcelInfo> WeeklyProductParcel;
    public List<ParcelInfo> WeeklyProductMail;
    public string EncryptedUID;
    public List<AccountBanByNexonDB> accountBanByNexonDBs;
    public AccountRestrictionsDB AccountRestrictionsDB;
    public List<IssueAlertInfoDB> IssueAlertInfos;
}

public class AccountCurrencySyncResponse : ResponsePacket, IResponse<AccountCurrencySyncRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CurrencySync;
    public AccountCurrencyDB AccountCurrencyDB;
}

public class AccountSetRepresentCharacterAndCommentResponse : ResponsePacket, IResponse<AccountSetRepresentCharacterAndCommentRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_SetRepresentCharacterAndComment;
    public AccountDB AccountDB;
    public CharacterDB RepresentCharacterDB;
}

public class AccountGetTutorialResponse : ResponsePacket, IResponse<AccountGetTutorialRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_GetTutorial;
    public List<long> TutorialIds;
}

public class AccountSetTutorialResponse : ResponsePacket, IResponse<AccountSetTutorialRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_SetTutorial;
}

public class AccountPassCheckResponse : ResponsePacket, IResponse<AccountPassCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_PassCheck;
}

public class AccountCheckYostarResponse : ResponsePacket, IResponse<AccountCheckYostarRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CheckYostar;
    public int ResultState;
    public string ResultMessag;
    public string Birth;
}

public class AccountCallNameResponse : ResponsePacket, IResponse<AccountCallNameRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CallName;
    public AccountDB AccountDB;
}

public class AccountBirthDayResponse : ResponsePacket, IResponse<AccountBirthDayRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_BirthDay;
    public AccountDB AccountDB;
}

public class AccountAuth2Response : AccountAuthResponse, IResponse<AccountAuth2Request>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Auth2;
}

public class AccountLinkRewardResponse : ResponsePacket, IResponse<AccountLinkRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_LinkReward;
}

public class AccountCheckNexonResponse : ResponsePacket, IResponse<AccountCheckNexonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CheckNexon;
    public int ResultState;
    public string ResultMessage;
}

public class AccountDetachNexonResponse : ResponsePacket, IResponse<AccountDetachNexonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_DetachNexon;
    public int ResultState;
    public string ResultMessage;
}

public class AccountReportXignCodeCheaterResponse : ResponsePacket, IResponse<AccountReportXignCodeCheaterRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_ReportXignCodeCheater;
}

public class AccountDismissRepurchasablePopupResponse : ResponsePacket, IResponse<AccountDismissRepurchasablePopupRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_DismissRepurchasablePopup;
}

public class AccountInvalidateTokenResponse : ResponsePacket, IResponse<AccountInvalidateTokenRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_InvalidateToken;
}

public class AccountLoginSyncResponse : ResponsePacket, IResponse<AccountLoginSyncRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_LoginSync;
    public ResponsePacket Responses;
    public CafeGetInfoResponse CafeGetInfoResponse;
    public AccountCurrencySyncResponse AccountCurrencySyncResponse;
    public CharacterListResponse CharacterListResponse;
    public EquipmentItemListResponse EquipmentItemListResponse;
    public CharacterGearListResponse CharacterGearListResponse;
    public ItemListResponse ItemListResponse;
    public EchelonListResponse EchelonListResponse;
    public MemoryLobbyListResponse MemoryLobbyListResponse;
    public CampaignListResponse CampaignListResponse;
    public ArenaLoginResponse ArenaLoginResponse;
    public RaidLoginResponse RaidLoginResponse;
    public CraftInfoListResponse CraftInfoListResponse;
    public ClanLoginResponse ClanLoginResponse;
    public MomoTalkOutLineResponse MomotalkOutlineResponse;
    public ScenarioListResponse ScenarioListResponse;
    public ShopGachaRecruitListResponse ShopGachaRecruitListResponse;
    public TimeAttackDungeonLoginResponse TimeAttackDungeonLoginResponse;
    public EventContentPermanentListResponse EventContentPermanentListResponse;
    public BillingPurchaseListByNexonResponse BillingPurchaseListByNexonResponse;
}

public class AccountVerifyAdultCheckResponse : ResponsePacket, IResponse<AccountVerifyAdultCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_VerifyCheckAdultAgree;
    public bool CheckAdultAgree;
}

public class CharacterListResponse : ResponsePacket, IResponse<CharacterListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_List;
    public List<CharacterDB> CharacterDBs;
    public List<CharacterDB> TSSCharacterDBs;
    public List<WeaponDB> WeaponDBs;
}

public class CharacterTranscendenceResponse : ResponsePacket, IResponse<CharacterTranscendenceRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_Transcendence;
    public CharacterDB CharacterDB;
    public ParcelResultDB ParcelResultDB;
}

public class CharacterExpGrowthResponse : ResponsePacket, IResponse<CharacterExpGrowthRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_ExpGrowth;
    public CharacterDB CharacterDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
}

public class CharacterFavorGrowthResponse : ResponsePacket, IResponse<CharacterFavorGrowthRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_FavorGrowth;
    public CharacterDB CharacterDB;
    public List<ItemDB> ConsumeStackableItemDBResult;
    public ParcelResultDB ParcelResultDB;
}

public class CharacterUnlockWeaponResponse : ResponsePacket, IResponse<CharacterUnlockWeaponRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_UnlockWeapon;
    public WeaponDB WeaponDB;
}

public class CharacterWeaponExpGrowthResponse : ResponsePacket, IResponse<CharacterWeaponExpGrowthRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_WeaponExpGrowth;
    public ParcelResultDB ParcelResultDB;
}

public class CharacterWeaponTranscendenceResponse : ResponsePacket, IResponse<CharacterWeaponTranscendenceRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_WeaponTranscendence;
    public ParcelResultDB ParcelResultDB;
}

public class CharacterSetFavoritesResponse : ResponsePacket, IResponse<CharacterSetFavoritesRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_SetFavorites;
    public List<CharacterDB> CharacterDBs;
}

public class EquipmentBatchGrowthResponse : ResponsePacket, IResponse<EquipmentBatchGrowthRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Equipment_BatchGrowth;
    public List<EquipmentDB> EquipmentDBs;
    public ParcelResultDB ParcelResultDB;
    public ConsumeResultDB ConsumeResultDB;
}

public class ItemListResponse : ResponsePacket, IResponse<ItemListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_List;
    public List<ItemDB> ItemDBs;
    public List<ItemDB> ExpiryItemDBs;
}

public class ItemSellResponse : ResponsePacket, IResponse<ItemSellRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_Sell;
    public AccountCurrencyDB AccountCurrencyDB;
}

public class ItemConsumeResponse : ResponsePacket, IResponse<ItemConsumeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_Consume;
    public ItemDB UsedItemDB;
    public ParcelResultDB NewParcelResultDB;
}

public class ItemLockResponse : ResponsePacket, IResponse<ItemLockRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_Lock;
    public ItemDB ItemDB;
}

public class ItemBulkConsumeResponse : ResponsePacket, IResponse<ItemBulkConsumeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_BulkConsume;
    public ItemDB UsedItemDB;
    public List<ParcelInfo> ParcelInfosInMailBox;
}

public class ItemSelectTicketResponse : ResponsePacket, IResponse<ItemSelectTicketRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_SelectTicket;
    public ItemDB UsedItemDB;
    public ParcelResultDB ParcelResultDB;
}

public class EchelonListResponse : ResponsePacket, IResponse<EchelonListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_List;
    public List<EchelonDB> EchelonDBs;
    public EchelonDB ArenaEchelonDB;
}

public class EchelonSaveResponse : ResponsePacket, IResponse<EchelonSaveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_Save;
    public EchelonDB EchelonDB;
}

public class EchelonPresetListResponse : ResponsePacket, IResponse<EchelonPresetListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_PresetList;
    public EchelonPresetGroupDB[] PresetGroupDBs;
}

public class EchelonPresetSaveResponse : ResponsePacket, IResponse<EchelonPresetSaveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_PresetSave;
    public EchelonPresetDB PresetDB;
}

public class EchelonPresetGroupRenameResponse : ResponsePacket, IResponse<EchelonPresetGroupRenameRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_PresetGroupRename;
    public EchelonPresetGroupDB PresetGroupDB;
}

public class CampaignListResponse : ResponsePacket, IResponse<CampaignListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_List;
    public List<CampaignChapterClearRewardHistoryDB> CampaignChapterClearRewardHistoryDBs;
    public List<CampaignStageHistoryDB> StageHistoryDBs;
    public List<StrategyObjectHistoryDB> StrategyObjecthistoryDBs;
    public DailyResetCountDB DailyResetCountDB;
}

public class CampaignEnterMainStageResponse : ResponsePacket, IResponse<CampaignEnterMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterMainStage;
    public CampaignMainStageSaveDB SaveDataDB;
}

public class CampaignConfirmMainStageResponse : ResponsePacket, IResponse<CampaignConfirmMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_ConfirmMainStage;
    public ParcelResultDB ParcelResultDB;
    public CampaignMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
}

public class CampaignDeployEchelonResponse : ResponsePacket, IResponse<CampaignDeployEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_DeployEchelon;
    public CampaignMainStageSaveDB SaveDataDB;
}

public class CampaignWithdrawEchelonResponse : ResponsePacket, IResponse<CampaignWithdrawEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_WithdrawEchelon;
    public CampaignMainStageSaveDB SaveDataDB;
    public List<EchelonDB> WithdrawEchelonDBs;
}

public class CampaignMapMoveResponse : ResponsePacket, IResponse<CampaignMapMoveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_MapMove;
    public CampaignMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
    public long EchelonEntityId;
    public Strategy StrategyObject;
    public List<ParcelInfo> StrategyObjectParcelInfos;
}

public class CampaignEndTurnResponse : ResponsePacket, IResponse<CampaignEndTurnRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EndTurn;
    public CampaignMainStageSaveDB SaveDataDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public List<long> ScenarioIds;
}

public class CampaignEnterTacticResponse : ResponsePacket, IResponse<CampaignEnterTacticRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterTactic;
}

public class CampaignTacticResultResponse : ResponsePacket, IResponse<CampaignTacticResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_TacticResult;
    public long TacticRank;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public List<ParcelInfo> FirstClearReward;
    public List<ParcelInfo> ThreeStarReward;
    public Strategy StrategyObject;
    public Dictionary<long, List<ParcelInfo>> StrategyObjectRewards;
    public ParcelResultDB ParcelResultDB;
    public CampaignMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
}

public class CampaignRetreatResponse : ResponsePacket, IResponse<CampaignRetreatRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_Retreat;
    public List<long> ReleasedEchelonNumbers;
    public ParcelResultDB ParcelResultDB;
}

public class CampaignChapterClearRewardResponse : ResponsePacket, IResponse<CampaignChapterClearRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_ChapterClearReward;
    public CampaignChapterClearRewardHistoryDB CampaignChapterClearRewardHistoryDB;
    public ParcelResultDB ParcelResultDB;
}

public class CampaignHealResponse : ResponsePacket, IResponse<CampaignHealRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_Heal;
    public AccountCurrencyDB AccountCurrencyDB;
    public DailyResetCountDB DailyResetCountDB;
    public CampaignMainStageSaveDB SaveDataDB;
}

public class CampaignEnterSubStageResponse : ResponsePacket, IResponse<CampaignEnterSubStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterSubStage;
    public ParcelResultDB ParcelResultDB;
    public CampaignSubStageSaveDB SaveDataDB;
}

public class CampaignSubStageResultResponse : ResponsePacket, IResponse<CampaignSubStageResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_SubStageResult;
    public long TacticRank;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> FirstClearReward;
    public List<long> ScenarioIds;
}

public class CampaignPortalResponse : ResponsePacket, IResponse<CampaignPortalRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_Portal;
    public CampaignMainStageSaveDB CampaignMainStageSaveDB;
    public List<long> ScenarioIds;
}

public class CampaignConfirmTutorialStageResponse : ResponsePacket, IResponse<CampaignConfirmTutorialStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_ConfirmTutorialStage;
    public CampaignMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
}

public class CampaignPurchasePlayCountHardStageResponse : ResponsePacket, IResponse<CampaignPurchasePlayCountHardStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_PurchasePlayCountHardStage;
    public AccountCurrencyDB AccountCurrencyDB;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
}

public class CampaignEnterTutorialStageResponse : ResponsePacket, IResponse<CampaignEnterTutorialStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterTutorialStage;
    public ParcelResultDB ParcelResultDB;
    public CampaignTutorialStageSaveDB SaveDataDB;
}

public class CampaignTutorialStageResultResponse : ResponsePacket, IResponse<CampaignTutorialStageResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_TutorialStageResult;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> ClearReward;
    public List<ParcelInfo> FirstClearReward;
}

public class CampaignRestartMainStageResponse : ResponsePacket, IResponse<CampaignRestartMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_RestartMainStage;
    public ParcelResultDB ParcelResultDB;
    public CampaignMainStageSaveDB SaveDataDB;
}

public class MailListResponse : ResponsePacket, IResponse<MailListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mail_List;
    public List<MailDB> MailDBs;
    public long Count;
}

public class MailCheckResponse : ResponsePacket, IResponse<MailCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mail_Check;
    public long Count;
}

public class MailReceiveResponse : ResponsePacket, IResponse<MailReceiveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mail_Receive;
    public List<long> MailServerIds;
    public ParcelResultDB ParcelResultDB;
}

public class MissionListResponse : ResponsePacket, IResponse<MissionListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_List;
    public List<MissionHistoryDB> HistoryDBs;
    public List<MissionProgressDB> ProgressDBs;
    public List<GuideMissionSeasonDB> GuideMissionSeasonDBs;
    public MissionInfo DailySuddenMissionInfo;
    public List<long> ClearedOrignalMissionIds;
}

public class MissionRewardResponse : ResponsePacket, IResponse<MissionRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_Reward;
    public MissionHistoryDB AddedHistoryDB;
    public ParcelResultDB ParcelResultDB;
}

public class MissionMultipleRewardResponse : ResponsePacket, IResponse<MissionMultipleRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_MultipleReward;
    public List<MissionHistoryDB> AddedHistoryDBs;
    public ParcelResultDB ParcelResultDB;
}

public class MissionSyncResponse : ResponsePacket, IResponse<MissionSyncRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_Sync;
    public List<MissionHistoryDB> HistoryDBs;
    public List<MissionProgressDB> ProgressDBs;
    public Dictionary<long, List<MissionHistoryDB>> EventHistoryDBs;
    public Dictionary<long, List<MissionProgressDB>> EventProgressDBs;
    public Dictionary<long, List<MissionHistoryDB>> MiniGameHistoryDBs;
    public Dictionary<long, List<MissionProgressDB>> MiniGameProgressDBs;
}

public class AttendanceRewardResponse : ResponsePacket, IResponse<AttendanceRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Attendance_Reward;
    public List<AttendanceBookReward> AttendanceBookRewards;
    public List<AttendanceHistoryDB> AttendanceHistoryDBs;
    public ParcelResultDB ParcelResultDB;
}

public class ShopBuyMerchandiseResponse : ResponsePacket, IResponse<ShopBuyMerchandiseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyMerchandise;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
    public ParcelResultDB ParcelResultDB;
    public MailDB MailDB;
    public ShopProductDB ShopProductDB;
}

public class ShopBuyGachaResponse : ResponsePacket, IResponse<ShopBuyGachaRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyGacha;
    public ConsumeResultDB ConsumeResultDB;
    public ParcelResultDB ParcelResultDB;
}

public class ShopListResponse : ResponsePacket, IResponse<ShopListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_List;
    public List<ShopInfoDB> ShopInfos;
    public List<ShopEligmaHistoryDB> ShopEligmaHistoryDBs;
}

public class ShopRefreshResponse : ResponsePacket, IResponse<ShopRefreshRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_Refresh;
    public ParcelResultDB ParcelResultDB;
    public ShopInfoDB ShopInfoDB;
}

public class ShopBuyEligmaResponse : ResponsePacket, IResponse<ShopBuyEligmaRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyEligma;
    public ParcelResultDB ParcelResultDB;
    public ConsumeResultDB ConsumeResultDB;
    public ShopProductDB ShopProductDB;
}

public class ShopBuyGacha2Response : ResponsePacket, IResponse<ShopBuyGacha2Request>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyGacha2;
    public DateTime UpdateTime;
    public long GemBonusRemain;
    public long GemPaidRemain;
    public List<ItemDB> ConsumedItems;
    public List<GachaResult> GachaResults;
    public List<ItemDB> AcquiredItems;
}

public class ShopGachaRecruitListResponse : ResponsePacket, IResponse<ShopGachaRecruitListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_GachaRecruitList;
    public List<ShopRecruitDB> ShopRecruits;
    public List<ShopFreeRecruitHistoryDB> ShopFreeRecruitHistoryDBs;
}

public class ShopBuyRefreshMerchandiseResponse : ResponsePacket, IResponse<ShopBuyRefreshMerchandiseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyRefreshMerchandise;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
    public ParcelResultDB ParcelResultDB;
    public List<ShopProductDB> ShopProductDB;
    public MailDB MailDB;
}

public class ShopBuyGacha3Response : ShopBuyGacha2Response, IResponse<ShopBuyGacha3Request>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyGacha3;
    public ShopFreeRecruitHistoryDB FreeRecruitHistoryDB;
}

public class ShopBuyAPResponse : ResponsePacket, IResponse<ShopBuyAPRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyAP;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
    public ParcelResultDB ParcelResultDB;
    public MailDB MailDB;
    public ShopProductDB ShopProductDB;
}

public class RecipeCraftResponse : ResponsePacket, IResponse<RecipeCraftRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Recipe_Craft;
    public ParcelResultDB ParcelResultDB;
    public ConsumeResultDB EquipmentConsumeResultDB;
    public ConsumeResultDB ItemConsumeResultDB;
}

public class MemoryLobbyListResponse : ResponsePacket, IResponse<MemoryLobbyListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_List;
    public List<MemoryLobbyDB> MemoryLobbyDBs;
}

public class MemoryLobbySetMainResponse : ResponsePacket, IResponse<MemoryLobbySetMainRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_SetMain;
    public AccountDB AccountDB;
}

public class MemoryLobbyUpdateLobbyModeResponse : ResponsePacket, IResponse<MemoryLobbyUpdateLobbyModeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_UpdateLobbyMode;
}

public class MemoryLobbyInteractResponse : ResponsePacket, IResponse<MemoryLobbyInteractRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_Interact;
}

public class CumulativeTimeRewardListResponse : ResponsePacket, IResponse<CumulativeTimeRewardListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CumulativeTimeReward_List;
    public CumulativeTimeRewardInfo RewardInfo;
    public List<CumulativeTimeRewardDB> RewardHistoryDBs;
}

public class OpenConditionListResponse : ResponsePacket, IResponse<OpenConditionListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.OpenCondition_List;
    public List<OpenConditionContent> ConditionContents;
}

public class OpenConditionSetResponse : ResponsePacket, IResponse<OpenConditionSetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.OpenCondition_Set;
    public List<OpenConditionDB> ConditionDBs;
}

public class OpenConditionEventListResponse : ResponsePacket, IResponse<OpenConditionEventListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.OpenCondition_EventList;
    public Dictionary<long, List<ConquestTileDB>> ConquestTiles;
    public Dictionary<long, List<WorldRaidLocalBossDB>> WorldRaidLocalBossDBs;
}

public class ToastListResponse : ResponsePacket, IResponse<ToastListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Toast_List;
    public List<ToastDB> ToastDBs;
}

public class RaidListResponse : ResponsePacket, IResponse<RaidListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_List;
    public List<RaidDB> CreateRaidDBs;
    public List<RaidDB> EnterRaidDBs;
    public List<RaidDB> ListRaidDBs;
}

public class RaidCompleteListResponse : ResponsePacket, IResponse<RaidCompleteListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_CompleteList;
    public List<RaidDB> RaidDBs;
    public long StackedDamage;
    public List<long> ReceiveRewardId;
    public long CurSeasonUniqueId;
}

public class RaidDetailResponse : ResponsePacket, IResponse<RaidDetailRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Detail;
    public RaidDetailDB RaidDetailDB;
    public List<long> ParticipateCharacterServerIds;
}

public class RaidSearchResponse : ResponsePacket, IResponse<RaidSearchRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Search;
    public List<RaidDB> RaidDBs;
}

public class RaidCreateBattleResponse : ResponsePacket, IResponse<RaidCreateBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_CreateBattle;
    public RaidDB RaidDB;
    public RaidBattleDB RaidBattleDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public AssistCharacterDB AssistCharacterDB;
}

public class RaidEnterBattleResponse : ResponsePacket, IResponse<RaidEnterBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_EnterBattle;
    public RaidDB RaidDB;
    public RaidBattleDB RaidBattleDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public AssistCharacterDB AssistCharacterDB;
}

public class RaidBattleUpdateResponse : ResponsePacket, IResponse<RaidBattleUpdateRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_BattleUpdate;
    public RaidBattleDB RaidBattleDB;
}

public class RaidEndBattleResponse : ResponsePacket, IResponse<RaidEndBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_EndBattle;
    public long RankingPoint;
    public long BestRankingPoint;
    public long ClearTimePoint;
    public long HPPercentScorePoint;
    public long DefaultClearPoint;
    public ParcelResultDB ParcelResultDB;
}

public class RaidRewardResponse : ResponsePacket, IResponse<RaidRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Reward;
    public long RankingPoint;
    public long BestRankingPoint;
    public ParcelResultDB ParcelResultDB;
}

public class RaidRewardAllResponse : ResponsePacket, IResponse<RaidRewardAllRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_RewardAll;
    public ParcelResultDB ParcelResultDB;
}

public class RaidShareResponse : ResponsePacket, IResponse<RaidShareRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Share;
    public RaidDB RaidDB;
}

public class RaidSeasonRewardResponse : ResponsePacket, IResponse<RaidSeasonRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_SeasonReward;
    public ParcelResultDB ParcelResultDB;
    public List<long> ReceiveRewardIds;
}

public class RaidLobbyResponse : ResponsePacket, IResponse<RaidLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Lobby;
    public RaidSeasonType SeasonType;
    public RaidGiveUpDB RaidGiveUpDB;
    public RaidLobbyInfoDB RaidLobbyInfoDB;
}

public class RaidGiveUpResponse : ResponsePacket, IResponse<RaidGiveUpRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_GiveUp;
    public int Tier;
    public RaidGiveUpDB RaidGiveUpDB;
}

public class RaidOpponentListResponse : ResponsePacket, IResponse<RaidOpponentListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_OpponentList;
    public List<RaidUserDB> OpponentUserDBs;
}

public class RaidRankingRewardResponse : ResponsePacket, IResponse<RaidRankingRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_RankingReward;
    public long ReceivedRankingRewardId;
    public ParcelResultDB ParcelResultDB;
}

public class RaidLoginResponse : ResponsePacket, IResponse<RaidLoginRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Login;
    public RaidSeasonType SeasonType;
    public bool CanReceiveRankingReward;
}

public class RaidSweepResponse : ResponsePacket, IResponse<RaidSweepRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Sweep;
    public long TotalSeasonPoint;
    public List<List<ParcelInfo>> Rewards;
    public ParcelResultDB ParcelResultDB;
}

public class RaidGetBestTeamResponse : ResponsePacket, IResponse<RaidGetBestTeamRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_GetBestTeam;
    public List<RaidTeamSettingDB> RaidTeamSettingDBs;
}

public class SkipHistoryListResponse : ResponsePacket, IResponse<SkipHistoryListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SkipHistory_List;
    public SkipHistoryDB SkipHistoryDB;
}

public class SkipHistorySaveResponse : ResponsePacket, IResponse<SkipHistorySaveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SkipHistory_Save;
    public SkipHistoryDB SkipHistoryDB;
}

public class ScenarioListResponse : ResponsePacket, IResponse<ScenarioListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_List;
    public List<ScenarioHistoryDB> ScenarioHistoryDBs;
    public List<ScenarioGroupHistoryDB> ScenarioGroupHistoryDBs;
}

public class ScenarioClearResponse : ResponsePacket, IResponse<ScenarioClearRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Clear;
    public ScenarioHistoryDB ScenarioHistoryDB;
    public ParcelResultDB ParcelResultDB;
}

public class ScenarioGroupHistoryUpdateResponse : ResponsePacket, IResponse<ScenarioGroupHistoryUpdateRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_GroupHistoryUpdate;
    public ScenarioGroupHistoryDB ScenarioGroupHistoryDB;
}

public class ScenarioSkipResponse : ResponsePacket, IResponse<ScenarioSkipRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Skip;
}

public class ScenarioSelectResponse : ResponsePacket, IResponse<ScenarioSelectRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Select;
}

public class ScenarioAccountStudentChangeResponse : ResponsePacket, IResponse<ScenarioAccountStudentChangeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_AccountStudentChange;
}

public class ScenarioLobbyStudentChangeResponse : ResponsePacket, IResponse<ScenarioLobbyStudentChangeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_LobbyStudentChange;
}

public class ScenarioSpecialLobbyChangeResponse : ResponsePacket, IResponse<ScenarioSpecialLobbyChangeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_SpecialLobbyChange;
}

public class ScenarioEnterResponse : ResponsePacket, IResponse<ScenarioEnterRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Enter;
}

public class ScenarioEnterMainStageResponse : ResponsePacket, IResponse<ScenarioEnterMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_EnterMainStage;
    public StoryStrategyStageSaveDB SaveDataDB;
}

public class ScenarioConfirmMainStageResponse : ResponsePacket, IResponse<ScenarioConfirmMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_ConfirmMainStage;
    public ParcelResultDB ParcelResultDB;
    public StoryStrategyStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
}

public class ScenarioDeployEchelonResponse : ResponsePacket, IResponse<ScenarioDeployEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_DeployEchelon;
    public StoryStrategyStageSaveDB SaveDataDB;
}

public class ScenarioWithdrawEchelonResponse : ResponsePacket, IResponse<ScenarioWithdrawEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_WithdrawEchelon;
    public StoryStrategyStageSaveDB SaveDataDB;
    public List<EchelonDB> WithdrawEchelonDBs;
}

public class ScenarioMapMoveResponse : ResponsePacket, IResponse<ScenarioMapMoveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_MapMove;
    public StoryStrategyStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
    public long EchelonEntityId;
    public Strategy StrategyObject;
    public List<ParcelInfo> StrategyObjectParcelInfos;
}

public class ScenarioEndTurnResponse : ResponsePacket, IResponse<ScenarioEndTurnRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_EndTurn;
    public StoryStrategyStageSaveDB SaveDataDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public List<long> ScenarioIds;
}

public class ScenarioEnterTacticResponse : ResponsePacket, IResponse<ScenarioEnterTacticRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_EnterTactic;
}

public class ScenarioTacticResultResponse : ResponsePacket, IResponse<ScenarioTacticResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_TacticResult;
    public Strategy StrategyObject;
    public StoryStrategyStageSaveDB SaveDataDB;
    public bool IsPlayerWin;
    public List<long> ScenarioIds;
}

public class ScenarioRetreatResponse : ResponsePacket, IResponse<ScenarioRetreatRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Retreat;
    public List<long> ReleasedEchelonNumbers;
    public ParcelResultDB ParcelResultDB;
}

public class ScenarioPortalResponse : ResponsePacket, IResponse<ScenarioPortalRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Portal;
    public StoryStrategyStageSaveDB StoryStrategyStageSaveDB;
    public List<long> ScenarioIds;
}

public class ScenarioRestartMainStageResponse : ResponsePacket, IResponse<ScenarioRestartMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_RestartMainStage;
    public ParcelResultDB ParcelResultDB;
    public StoryStrategyStageSaveDB SaveDataDB;
}

public class ScenarioSkipMainStageResponse : ResponsePacket, IResponse<ScenarioSkipMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_SkipMainStage;
}

public class CafeGetInfoResponse : ResponsePacket, IResponse<CafeGetInfoRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_Get;
    public CafeDB CafeDB;
}

public class CafeAckResponse : ResponsePacket, IResponse<CafeAckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_Ack;
    public CafeDB CafeDB;
}

public class CafeListPresetResponse : ResponsePacket, IResponse<CafeListPresetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ListPreset;
    public List<CafePresetDB> CafePresetDBs;
}

public class CafeRenamePresetResponse : ResponsePacket, IResponse<CafeRenamePresetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_RenamePreset;
}

public class CafeClearPresetResponse : ResponsePacket, IResponse<CafeClearPresetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ClearPreset;
}

public class CafeUpdatePresetFurnitureResponse : ResponsePacket, IResponse<CafeUpdatePresetFurnitureRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_UpdatePresetFurniture;
}

public class CafeApplyPresetResponse : ResponsePacket, IResponse<CafeApplyPresetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ApplyPreset;
    public CafeDB CafeDB;
}

public class CafeRankUpResponse : ResponsePacket, IResponse<CafeRankUpRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_RankUp;
    public CafeDB CafeDB;
    public ParcelResultDB ParcelResultDB;
    public ConsumeResultDB ConsumeResultDB;
}

public class CafeReceiveCurrencyResponse : ResponsePacket, IResponse<CafeReceiveCurrencyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ReceiveCurrency;
    public CafeDB CafeDB;
    public ParcelResultDB ParcelResultDB;
}

public class CafeGiveGiftResponse : ResponsePacket, IResponse<CafeGiveGiftRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_GiveGift;
    public ParcelResultDB ParcelResultDB;
    public ConsumeResultDB ConsumeResultDB;
}

public class CafeSummonCharacterResponse : ResponsePacket, IResponse<CafeSummonCharacterRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_SummonCharacter;
    public CafeDB CafeDB;
}

public class CafeTrophyHistoryResponse : ResponsePacket, IResponse<CafeTrophyHistoryRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_TrophyHistory;
    public List<RaidSeasonRankingHistoryDB> RaidSeasonRankingHistoryDBs;
}

public class CraftSelectNodeResponse : ResponsePacket, IResponse<CraftSelectNodeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_SelectNode;
    public CraftNodeDB SelectedNodeDB;
}

public class CraftUpdateNodeLevelResponse : ResponsePacket, IResponse<CraftUpdateNodeLevelRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_UpdateNodeLevel;
    public CraftInfoDB CraftInfoDB;
    public CraftNodeDB CraftNodeDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
}

public class CraftBeginProcessResponse : ResponsePacket, IResponse<CraftBeginProcessRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_BeginProcess;
    public CraftInfoDB CraftInfoDB;
}

public class CraftCompleteProcessResponse : ResponsePacket, IResponse<CraftCompleteProcessRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_CompleteProcess;
    public AccountCurrencyDB AccountCurrencyDB;
    public CraftInfoDB CraftInfoDB;
    public ItemDB TicketItemDB;
}

public class CraftRewardResponse : ResponsePacket, IResponse<CraftRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_Reward;
    public ParcelResultDB ParcelResultDB;
    public List<CraftInfoDB> CraftInfos;
}

public class CraftShiftingBeginProcessResponse : ResponsePacket, IResponse<CraftShiftingBeginProcessRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_ShiftingBeginProcess;
    public ShiftingCraftInfoDB CraftInfoDB;
    public ParcelResultDB ParcelResultDB;
}

public class CraftShiftingCompleteProcessResponse : ResponsePacket, IResponse<CraftShiftingCompleteProcessRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_ShiftingCompleteProcess;
    public ShiftingCraftInfoDB CraftInfoDB;
    public ParcelResultDB ParcelResultDB;
}

public class CraftShiftingRewardResponse : ResponsePacket, IResponse<CraftShiftingRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_ShiftingReward;
    public ParcelResultDB ParcelResultDB;
    public List<ShiftingCraftInfoDB> TargetCraftInfos;
}

public class ArenaEnterLobbyResponse : ResponsePacket, IResponse<ArenaEnterLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterLobby;
    public ArenaPlayerInfoDB ArenaPlayerInfoDB;
    public List<ArenaUserDB> OpponentUserDBs;
    public long MapId;
    public DateTime AutoRefreshTime;
}

public class ArenaLoginResponse : ResponsePacket, IResponse<ArenaLoginRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_Login;
    public ArenaPlayerInfoDB ArenaPlayerInfoDB;
}

public class ArenaSettingChangeResponse : ResponsePacket, IResponse<ArenaSettingChangeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_SettingChange;
}

public class ArenaOpponentListResponse : ResponsePacket, IResponse<ArenaOpponentListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_OpponentList;
    public long PlayerRank;
    public List<ArenaUserDB> OpponentUserDBs;
    public DateTime AutoRefreshTime;
}

public class ArenaEnterBattleResponse : ResponsePacket, IResponse<ArenaEnterBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterBattle;
    public AccountCurrencyDB AccountCurrencyDB;
    public ArenaBattleDB ArenaBattleDB;
    public ArenaPlayerInfoDB ArenaPlayerInfoDB;
    public ParcelResultDB VictoryRewards;
    public ParcelResultDB SeasonRewards;
    public ParcelResultDB AllTimeRewards;
}

public class ArenaEnterBattlePart1Response : ResponsePacket, IResponse<ArenaEnterBattlePart1Request>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterBattlePart1;
    public ArenaBattleDB ArenaBattleDB;
}

public class ArenaEnterBattlePart2Response : ResponsePacket, IResponse<ArenaEnterBattlePart2Request>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterBattlePart2;
    public ArenaBattleDB ArenaBattleDB;
    public ArenaPlayerInfoDB ArenaPlayerInfoDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public ParcelResultDB VictoryRewards;
    public ParcelResultDB SeasonRewards;
    public ParcelResultDB AllTimeRewards;
}

public class ArenaBattleResultResponse : ResponsePacket, IResponse<ArenaBattleResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_BattleResult;
}

public class ArenaCumulativeTimeRewardResponse : ResponsePacket, IResponse<ArenaCumulativeTimeRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_CumulativeTimeReward;
    public long TimeRewardAmount;
    public DateTime TimeRewardLastUpdateTime;
    public ParcelResultDB ParcelResult;
}

public class ArenaDailyRewardResponse : ResponsePacket, IResponse<ArenaDailyRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_DailyReward;
    public ParcelResultDB ParcelResult;
    public DateTime DailyRewardActiveTime;
}

public class ArenaRankListResponse : ResponsePacket, IResponse<ArenaRankListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_RankList;
    public List<ArenaUserDB> TopRankedUserDBs;
}

public class ArenaHistoryResponse : ResponsePacket, IResponse<ArenaHistoryRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_History;
    public List<ArenaHistoryDB> ArenaHistoryDBs;
    public List<ArenaDamageReportDB> ArenaDamageReportDB;
}

public class ArenaCheckSeasonCloseRewardResponse : ResponsePacket, IResponse<ArenaCheckSeasonCloseRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_CheckSeasonCloseReward;
}

public class ArenaSyncEchelonSettingTimeResponse : ResponsePacket, IResponse<ArenaSyncEchelonSettingTimeRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_SyncEchelonSettingTime;
    public DateTime EchelonSettingTime;
}

public class WeekDungeonListResponse : ResponsePacket, IResponse<WeekDungeonListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_List;
    public List<long> AdditionalStageIdList;
    public List<WeekDungeonStageHistoryDB> WeekDungeonStageHistoryDBList;
}

public class WeekDungeonEnterBattleResponse : ResponsePacket, IResponse<WeekDungeonEnterBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_EnterBattle;
    public ParcelResultDB ParcelResultDB;
    public int Seed;
    public int Sequence;
}

public class WeekDungeonBattleResultResponse : ResponsePacket, IResponse<WeekDungeonBattleResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_BattleResult;
    public WeekDungeonStageHistoryDB WeekDungeonStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public ParcelResultDB ParcelResultDB;
}

public class WeekDungeonRetreatResponse : ResponsePacket, IResponse<WeekDungeonRetreatRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_Retreat;
    public ParcelResultDB ParcelResultDB;
}

public class AcademyGetInfoResponse : ResponsePacket, IResponse<AcademyGetInfoRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Academy_GetInfo;
    public AcademyDB AcademyDB;
    public List<AcademyLocationDB> AcademyLocationDBs;
}

public class AcademyAttendScheduleResponse : ResponsePacket, IResponse<AcademyAttendScheduleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Academy_AttendSchedule;
    public ParcelResultDB ParcelResultDB;
    public AcademyDB AcademyDB;
    public List<ParcelInfo> ExtraRewards;
}

public class EventRewardIncreaseResponse : ResponsePacket, IResponse<EventRewardIncreaseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Event_RewardIncrease;
    public List<EventRewardIncreaseDB> EventRewardIncreaseDBs;
}

public class ContentSaveGetResponse : ResponsePacket, IResponse<ContentSaveGetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ContentSave_Get;
    public bool HasValidData;
    public ContentSaveDB ContentSaveDB;
    public EventContentChangeDB EventContentChangeDB;
}

public class ContentSaveDiscardResponse : ResponsePacket, IResponse<ContentSaveDiscardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ContentSave_Discard;
    public ParcelResultDB ParcelResultDB;
}

public class ClanLobbyResponse : ResponsePacket, IResponse<ClanLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Lobby;
    public IrcServerConfig IrcConfig;
    public ClanDB AccountClanDB;
    public List<ClanDB> DefaultExposedClanDBs;
    public ClanMemberDB AccountClanMemberDB;
    public List<ClanMemberDB> ClanMemberDBs;
}

public class ClanLoginResponse : ResponsePacket, IResponse<ClanLoginRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Login;
    public ClanDB AccountClanDB;
    public ClanMemberDB AccountClanMemberDB;
}

public class ClanSearchResponse : ResponsePacket, IResponse<ClanSearchRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Search;
    public List<ClanDB> ClanDBs;
}

public class ClanCreateResponse : ResponsePacket, IResponse<ClanCreateRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Create;
    public ClanDB ClanDB;
    public ClanMemberDB ClanMemberDB;
    public AccountCurrencyDB AccountCurrencyDB;
}

public class ClanMemberResponse : ResponsePacket, IResponse<ClanMemberRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Member;
    public ClanDB ClanDB;
    public ClanMemberDB ClanMemberDB;
    public long CollectedCharactersCount;
    public ClanMemberDescriptionDB ClanMemberDescriptionDB;
}

public class ClanApplicantResponse : ResponsePacket, IResponse<ClanApplicantRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Applicant;
    public List<ClanMemberDB> ClanMemberDBs;
}

public class ClanJoinResponse : ResponsePacket, IResponse<ClanJoinRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Join;
    public IrcServerConfig IrcConfig;
    public ClanDB ClanDB;
    public ClanMemberDB ClanMemberDB;
}

public class ClanQuitResponse : ResponsePacket, IResponse<ClanQuitRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Quit;
}

public class ClanPermitResponse : ResponsePacket, IResponse<ClanPermitRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Permit;
    public ClanDB ClanDB;
    public ClanMemberDB ClanMemberDB;
}

public class ClanKickResponse : ResponsePacket, IResponse<ClanKickRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Kick;
}

public class ClanSettingResponse : ResponsePacket, IResponse<ClanSettingRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Setting;
    public ClanDB ClanDB;
}

public class ClanConferResponse : ResponsePacket, IResponse<ClanConferRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Confer;
    public ClanMemberDB ClanMemberDB;
    public ClanMemberDB AccountClanMemberDB;
    public ClanDB ClanDB;
    public ClanMemberDescriptionDB ClanMemberDescriptionDB;
}

public class ClanDismissResponse : ResponsePacket, IResponse<ClanDismissRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Dismiss;
}

public class ClanAutoJoinResponse : ResponsePacket, IResponse<ClanAutoJoinRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_AutoJoin;
    public IrcServerConfig IrcConfig;
    public ClanDB ClanDB;
    public ClanMemberDB ClanMemberDB;
}

public class ClanMemberListResponse : ResponsePacket, IResponse<ClanMemberListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_MemberList;
    public ClanDB ClanDB;
    public List<ClanMemberDB> ClanMemberDBs;
}

public class ClanCancelApplyResponse : ResponsePacket, IResponse<ClanCancelApplyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_CancelApply;
}

public class ClanMyAssistListResponse : ResponsePacket, IResponse<ClanMyAssistListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_MyAssistList;
    public List<ClanAssistSlotDB> ClanAssistSlotDBs;
}

public class ClanSetAssistResponse : ResponsePacket, IResponse<ClanSetAssistRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_SetAssist;
    public ClanAssistSlotDB ClanAssistSlotDB;
    public ParcelResultDB ParcelResultDB;
    public ClanAssistRewardInfo RewardInfo;
}

public class ClanChatLogResponse : ResponsePacket, IResponse<ClanChatLogRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_ChatLog;
    public string ClanChatLog;
}

public class ClanCheckResponse : ResponsePacket, IResponse<ClanCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Check;
}

public class ClanAllAssistListResponse : ResponsePacket, IResponse<ClanAllAssistListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_AllAssistList;
    public List<AssistCharacterDB> AssistCharacterDBs;
    public List<ClanAssistRentHistoryDB> AssistCharacterRentHistoryDBs;
    public long ClanDBId;
}

public class BillingTransactionStartByYostarResponse : ResponsePacket, IResponse<BillingTransactionStartByYostarRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_TransactionStartByYostar;
    public long PurchaseCount;
    public DateTime PurchaseResetDate;
    public long PurchaseOrderId;
    public string MXSeedKey;
    public PurchaseServerTag PurchaseServerTag;
}

public class BillingTransactionEndByYostarResponse : ResponsePacket, IResponse<BillingTransactionEndByYostarRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_TransactionEndByYostar;
    public ParcelResultDB ParcelResult;
    public MailDB MailDB;
    public List<PurchaseCountDB> CountList;
    public int PurchaseCount;
    public List<MonthlyProductPurchaseDB> MonthlyProductList;
}

public class BillingPurchaseListByYostarResponse : ResponsePacket, IResponse<BillingPurchaseListByYostarRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_PurchaseListByYostar;
    public List<PurchaseCountDB> CountList;
    public List<PurchaseOrderDB> OrderList;
    public List<MonthlyProductPurchaseDB> MonthlyProductList;
    public List<BlockedProductDB> BlockedProductDBs;
}

public class BillingPurchaseCashShopVerifyByNexonResponse : ResponsePacket, IResponse<BillingPurchaseCashShopVerifyByNexonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_PurchaseCashShopVerifyByNexon;
    public ParcelResultDB ParcelResult;
    public MailDB MailDB;
    public List<PurchaseCountDB> CountList;
    public int PurchaseCount;
    public List<MonthlyProductPurchaseDB> MonthlyProductList;
    public List<long> ProductMonthlyIdInMailList;
    public List<long> GachaTicketItemIdList;
    public string shopId;
    public double itemPrice;
    public string currency;
}

public class BillingPurchaseListByNexonResponse : ResponsePacket, IResponse<BillingPurchaseListByNexonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_PurchaseListByNexon;
    public List<PurchaseCountDB> CountList;
    public List<PurchaseOrderDB> OrderList;
    public List<MonthlyProductPurchaseDB> MonthlyProductList;
    public List<long> ProductMonthlyIdInMailList;
    public List<long> GachaTicketItemIdList;
    public List<BlockedProductDB> BlockedProductDBs;
}

public class EventContentAdventureListResponse : ResponsePacket, IResponse<EventContentAdventureListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_AdventureList;
    public List<CampaignStageHistoryDB> StageHistoryDBs;
    public List<StrategyObjectHistoryDB> StrategyObjecthistoryDBs;
    public List<EventContentBonusRewardDB> EventContentBonusRewardDBs;
    public List<long> AlreadyReceiveRewardId;
    public long StagePoint;
}

public class EventContentEnterMainStageResponse : ResponsePacket, IResponse<EventContentEnterMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterMainStage;
    public EventContentMainStageSaveDB SaveDataDB;
    public bool IsOnSubEvent;
}

public class EventContentConfirmMainStageResponse : ResponsePacket, IResponse<EventContentConfirmMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ConfirmMainStage;
    public ParcelResultDB ParcelResultDB;
    public EventContentMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
}

public class EventContentEnterTacticResponse : ResponsePacket, IResponse<EventContentEnterTacticRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterTactic;
}

public class EventContentTacticResultResponse : ResponsePacket, IResponse<EventContentTacticResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_TacticResult;
    public long TacticRank;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public List<ParcelInfo> FirstClearReward;
    public Strategy StrategyObject;
    public Dictionary<long, List<ParcelInfo>> StrategyObjectRewards;
    public List<ParcelInfo> BonusReward;
    public ParcelResultDB ParcelResultDB;
    public EventContentMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentEnterSubStageResponse : ResponsePacket, IResponse<EventContentEnterSubStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterSubStage;
    public ParcelResultDB ParcelResultDB;
    public EventContentSubStageSaveDB SaveDataDB;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
}

public class EventContentSubStageResultResponse : ResponsePacket, IResponse<EventContentSubStageResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_SubStageResult;
    public long TacticRank;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> FirstClearReward;
    public List<ParcelInfo> BonusReward;
    public List<long> ScenarioIds;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentDeployEchelonResponse : ResponsePacket, IResponse<EventContentDeployEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DeployEchelon;
    public EventContentMainStageSaveDB SaveDataDB;
}

public class EventContentWithdrawEchelonResponse : ResponsePacket, IResponse<EventContentWithdrawEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_WithdrawEchelon;
    public EventContentMainStageSaveDB SaveDataDB;
    public List<EchelonDB> WithdrawEchelonDBs;
}

public class EventContentMapMoveResponse : ResponsePacket, IResponse<EventContentMapMoveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_MapMove;
    public EventContentMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
    public long EchelonEntityId;
    public Strategy StrategyObject;
    public List<ParcelInfo> StrategyObjectParcelInfos;
}

public class EventContentEndTurnResponse : ResponsePacket, IResponse<EventContentEndTurnRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EndTurn;
    public EventContentMainStageSaveDB SaveDataDB;
    public AccountCurrencyDB AccountCurrencyDB;
    public List<long> ScenarioIds;
}

public class EventContentRetreatResponse : ResponsePacket, IResponse<EventContentRetreatRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_Retreat;
    public List<long> ReleasedEchelonNumbers;
    public ParcelResultDB ParcelResultDB;
}

public class EventContentPortalResponse : ResponsePacket, IResponse<EventContentPortalRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_Portal;
    public EventContentMainStageSaveDB SaveDataDB;
    public List<long> ScenarioIds;
}

public class EventContentPurchasePlayCountHardStageResponse : ResponsePacket, IResponse<EventContentPurchasePlayCountHardStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_PurchasePlayCountHardStage;
    public AccountCurrencyDB AccountCurrencyDB;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
}

public class EventContentShopListResponse : ResponsePacket, IResponse<EventContentShopListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopList;
    public List<ShopInfoDB> ShopInfos;
    public List<ShopEligmaHistoryDB> ShopEligmaHistoryDBs;
}

public class EventContentShopRefreshResponse : ResponsePacket, IResponse<EventContentShopRefreshRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopRefresh;
    public ParcelResultDB ParcelResultDB;
    public ShopInfoDB ShopInfoDB;
}

public class EventContentReceiveStageTotalRewardResponse : ResponsePacket, IResponse<EventContentReceiveStageTotalRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ReceiveStageTotalReward;
    public long EventContentId;
    public List<long> AlreadyReceiveRewardId;
    public ParcelResultDB ParcelResultDB;
}

public class EventContentEnterMainGroundStageResponse : ResponsePacket, IResponse<EventContentEnterMainGroundStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterMainGroundStage;
    public ParcelResultDB ParcelResultDB;
    public EventContentMainGroundStageSaveDB SaveDataDB;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
}

public class EventContentMainGroundStageResultResponse : ResponsePacket, IResponse<EventContentMainGroundStageResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_MainGroundStageResult;
    public long TacticRank;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> FirstClearReward;
    public List<ParcelInfo> BonusReward;
    public List<long> ScenarioIds;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentShopBuyMerchandiseResponse : ResponsePacket, IResponse<EventContentShopBuyMerchandiseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopBuyMerchandise;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
    public ParcelResultDB ParcelResultDB;
    public MailDB MailDB;
    public ShopProductDB ShopProductDB;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentShopBuyRefreshMerchandiseResponse : ResponsePacket, IResponse<EventContentShopBuyRefreshMerchandiseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopBuyRefreshMerchandise;
    public AccountCurrencyDB AccountCurrencyDB;
    public ConsumeResultDB ConsumeResultDB;
    public ParcelResultDB ParcelResultDB;
    public MailDB MailDB;
    public List<ShopProductDB> ShopProductDB;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentSelectBuffResponse : ResponsePacket, IResponse<EventContentSelectBuffRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_SelectBuff;
    public EventContentMainStageSaveDB SaveDataDB;
}

public class EventContentBoxGachaShopListResponse : ResponsePacket, IResponse<EventContentBoxGachaShopListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_BoxGachaShopList;
    public EventContentBoxGachaDB BoxGachaDB;
    public Dictionary<long, long> BoxGachaGroupIdByCount;
}

public class EventContentBoxGachaShopPurchaseResponse : ResponsePacket, IResponse<EventContentBoxGachaShopPurchaseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_BoxGachaShopPurchase;
    public ParcelResultDB ParcelResultDB;
    public EventContentBoxGachaDB BoxGachaDB;
    public Dictionary<long, long> BoxGachaGroupIdByCount;
    public List<EventContentBoxGachaElement> BoxGachaElements;
}

public class EventContentBoxGachaShopRefreshResponse : ResponsePacket, IResponse<EventContentBoxGachaShopRefreshRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_BoxGachaShopRefresh;
    public EventContentBoxGachaDB BoxGachaDB;
    public Dictionary<long, long> BoxGachaGroupIdByCount;
}

public class EventContentCollectionListResponse : ResponsePacket, IResponse<EventContentCollectionListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CollectionList;
    public List<EventContentCollectionDB> EventContentUnlockCGDBs;
}

public class EventContentCollectionForMissionResponse : ResponsePacket, IResponse<EventContentCollectionForMissionRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CollectionForMission;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentScenarioGroupHistoryUpdateResponse : ResponsePacket, IResponse<EventContentScenarioGroupHistoryUpdateRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ScenarioGroupHistoryUpdate;
    public List<ScenarioGroupHistoryDB> ScenarioGroupHistoryDBs;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
    public ParcelResultDB ParcelResultDB;
}

public class EventContentCardShopListResponse : ResponsePacket, IResponse<EventContentCardShopListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CardShopList;
    public List<CardShopElementDB> CardShopElementDBs;
    public Dictionary<long, List<ParcelInfo>> RewardHistory;
}

public class EventContentCardShopShuffleResponse : ResponsePacket, IResponse<EventContentCardShopShuffleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CardShopShuffle;
    public List<CardShopElementDB> CardShopElementDBs;
}

public class EventContentCardShopPurchaseResponse : ResponsePacket, IResponse<EventContentCardShopPurchaseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CardShopPurchase;
    public ParcelResultDB ParcelResultDB;
    public CardShopElementDB CardShopElementDB;
    public List<CardShopPurchaseHistoryDB> CardShopPurchaseHistoryDBs;
}

public class EventContentRestartMainStageResponse : ResponsePacket, IResponse<EventContentRestartMainStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_RestartMainStage;
    public ParcelResultDB ParcelResultDB;
    public EventContentMainStageSaveDB SaveDataDB;
}

public class EventContentLocationGetInfoResponse : ResponsePacket, IResponse<EventContentLocationGetInfoRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_LocationGetInfo;
    public EventContentLocationDB EventContentLocationDB;
}

public class EventContentLocationAttendScheduleResponse : ResponsePacket, IResponse<EventContentLocationAttendScheduleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_LocationAttendSchedule;
    public EventContentLocationDB EventContentLocationDB;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> ExtraRewards;
}

public class EventContentFortuneGachaPurchaseResponse : ResponsePacket, IResponse<EventContentFortuneGachaPurchaseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_FortuneGachaPurchase;
    public long FortuneGachaShopUniqueId;
    public ParcelResultDB ParcelResultDB;
}

public class EventContentSubEventLobbyResponse : ResponsePacket, IResponse<EventContentSubEventLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_SubEventLobby;
    public EventContentChangeDB EventContentChangeDB;
    public bool IsOnSubEvent;
}

public class EventContentEnterStoryStageResponse : ResponsePacket, IResponse<EventContentEnterStoryStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterStoryStage;
    public ParcelResultDB ParcelResultDB;
    public EventContentStoryStageSaveDB SaveDataDB;
}

public class EventContentStoryStageResultResponse : ResponsePacket, IResponse<EventContentStoryStageResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_StoryStageResult;
    public CampaignStageHistoryDB CampaignStageHistoryDB;
    public ParcelResultDB ParcelResultDB;
    public List<ParcelInfo> FirstClearReward;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentDiceRaceLobbyResponse : ResponsePacket, IResponse<EventContentDiceRaceLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DiceRaceLobby;
    public EventContentDiceRaceDB DiceRaceDB;
}

public class EventContentDiceRaceRollResponse : ResponsePacket, IResponse<EventContentDiceRaceRollRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DiceRaceRoll;
    public ParcelResultDB ParcelResultDB;
    public EventContentDiceRaceDB DiceRaceDB;
    public List<EventContentDiceResult> DiceResults;
    public List<EventContentCollectionDB> EventContentCollectionDBs;
}

public class EventContentDiceRaceLapRewardResponse : ResponsePacket, IResponse<EventContentDiceRaceLapRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DiceRaceLapReward;
    public EventContentDiceRaceDB DiceRaceDB;
    public ParcelResultDB ParcelResultDB;
}

public class EventContentPermanentListResponse : ResponsePacket, IResponse<EventContentPermanentListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_PermanentList;
    public List<EventContentPermanentDB> PermanentDBs;
}

public class TTSGetFileResponse : ResponsePacket, IResponse<TTSGetFileRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TTS_GetFile;
    public bool IsFileReady;
    public string TTSFileS3Uri;
}

public class TTSGetKanaResponse : ResponsePacket, IResponse<TTSGetKanaRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TTS_GetKana;
    public string CallName;
    public string ActualCallName;
    public string CallNameKatakana;
}

public class ContentLogUIOpenStatisticsResponse : ResponsePacket, IResponse<ContentLogUIOpenStatisticsRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ContentLog_UIOpenStatistics;
}

public class MomoTalkOutLineResponse : ResponsePacket, IResponse<MomoTalkOutLineRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_OutLine;
    public List<MomoTalkOutLineDB> MomoTalkOutLineDBs;
    public Dictionary<long, List<long>> FavorScheduleRecords;
}

public class MomoTalkMessageListResponse : ResponsePacket, IResponse<MomoTalkMessageListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_MessageList;
    public MomoTalkOutLineDB MomoTalkOutLineDB;
    public List<MomoTalkChoiceDB> MomoTalkChoiceDBs;
}

public class MomoTalkReadResponse : ResponsePacket, IResponse<MomoTalkReadRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_Read;
    public MomoTalkOutLineDB MomoTalkOutLineDB;
    public List<MomoTalkChoiceDB> MomoTalkChoiceDBs;
}

public class MomoTalkFavorScheduleResponse : ResponsePacket, IResponse<MomoTalkFavorScheduleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_FavorSchedule;
    public ParcelResultDB ParcelResultDB;
    public Dictionary<long, List<long>> FavorScheduleRecords;
}

public class ClearDeckListResponse : ResponsePacket, IResponse<ClearDeckListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ClearDeck_List;
    public List<ClearDeckDB> ClearDeckDBs;
}

public class MiniGameStageListResponse : ResponsePacket, IResponse<MiniGameStageListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_StageList;
    public List<MiniGameHistoryDB> MiniGameHistoryDBs;
}

public class MiniGameEnterStageResponse : ResponsePacket, IResponse<MiniGameEnterStageRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_EnterStage;
}

public class MiniGameResultResponse : ResponsePacket, IResponse<MiniGameResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_Result;
}

public class MiniGameMissionListResponse : ResponsePacket, IResponse<MiniGameMissionListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_MissionList;
    public List<MissionHistoryDB> HistoryDBs;
    public List<MissionProgressDB> ProgressDBs;
}

public class MiniGameMissionRewardResponse : ResponsePacket, IResponse<MiniGameMissionRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_MissionReward;
    public MissionHistoryDB AddedHistoryDB;
    public ParcelResultDB ParcelResultDB;
}

public class MiniGameMissionMultipleRewardResponse : ResponsePacket, IResponse<MiniGameMissionMultipleRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_MissionMultipleReward;
    public List<MissionHistoryDB> AddedHistoryDBs;
    public ParcelResultDB ParcelResultDB;
}

public class NotificationLobbyCheckResponse : ResponsePacket, IResponse<NotificationLobbyCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Notification_LobbyCheck;
    public long UnreadMailCount;
    public List<EventRewardIncreaseDB> EventRewardIncreaseDBs;
}

public class NotificationEventContentReddotResponse : ResponsePacket, IResponse<NotificationEventContentReddotRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Notification_EventContentReddotCheck;
    public Dictionary<long, List<NotificationEventReddot>> Reddots;
    public Dictionary<long, List<EventContentCollectionDB>> EventContentUnlockCGDBs;
}

public class ProofTokenRequestQuestionResponse : ResponsePacket, IResponse<ProofTokenRequestQuestionRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ProofToken_RequestQuestion;
    public long Hint;
    public string Question;
}

public class ProofTokenSubmitResponse : ResponsePacket, IResponse<ProofTokenSubmitRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ProofToken_Submit;
}

public class SchoolDungeonListResponse : ResponsePacket, IResponse<SchoolDungeonListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_List;
    public List<SchoolDungeonStageHistoryDB> SchoolDungeonStageHistoryDBList;
}

public class SchoolDungeonEnterBattleResponse : ResponsePacket, IResponse<SchoolDungeonEnterBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_EnterBattle;
    public ParcelResultDB ParcelResultDB;
}

public class SchoolDungeonBattleResultResponse : ResponsePacket, IResponse<SchoolDungeonBattleResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_BattleResult;
    public SchoolDungeonStageHistoryDB SchoolDungeonStageHistoryDB;
    public List<CharacterDB> LevelUpCharacterDBs;
    public List<ParcelInfo> FirstClearReward;
    public List<ParcelInfo> ThreeStarReward;
    public ParcelResultDB ParcelResultDB;
}

public class SchoolDungeonRetreatResponse : ResponsePacket, IResponse<SchoolDungeonRetreatRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_Retreat;
    public ParcelResultDB ParcelResultDB;
}

public class TimeAttackDungeonLobbyResponse : ResponsePacket, IResponse<TimeAttackDungeonLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_Lobby;
    public Dictionary<long, TimeAttackDungeonRoomDB> RoomDBs;
    public TimeAttackDungeonRoomDB PreviousRoomDB;
    public ParcelResultDB ParcelResultDB;
    public bool AchieveSeasonBestRecord;
    public long SeasonBestRecord;
}

public class TimeAttackDungeonCreateBattleResponse : ResponsePacket, IResponse<TimeAttackDungeonCreateBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_CreateBattle;
    public TimeAttackDungeonRoomDB RoomDB;
    public ParcelResultDB ParcelResultDB;
}

public class TimeAttackDungeonEnterBattleResponse : ResponsePacket, IResponse<TimeAttackDungeonEnterBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_EnterBattle;
    public AssistCharacterDB AssistCharacterDB;
}

public class TimeAttackDungeonEndBattleResponse : ResponsePacket, IResponse<TimeAttackDungeonEndBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_EndBattle;
    public TimeAttackDungeonRoomDB RoomDB;
    public long TotalPoint;
    public long DefaultPoint;
    public long TimePoint;
    public ParcelResultDB ParcelResultDB;
}

public class TimeAttackDungeonSweepResponse : ResponsePacket, IResponse<TimeAttackDungeonSweepRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_Sweep;
    public List<List<ParcelInfo>> Rewards;
    public ParcelResultDB ParcelResultDB;
    public TimeAttackDungeonRoomDB RoomDB;
}

public class TimeAttackDungeonGiveUpResponse : ResponsePacket, IResponse<TimeAttackDungeonGiveUpRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_GiveUp;
    public TimeAttackDungeonRoomDB RoomDB;
    public ParcelResultDB ParcelResultDB;
    public bool AchieveSeasonBestRecord;
    public long SeasonBestRecord;
}

public class TimeAttackDungeonLoginResponse : ResponsePacket, IResponse<TimeAttackDungeonLoginRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_Login;
    public TimeAttackDungeonRoomDB PreviousRoomDB;
}

public class WorldRaidLobbyResponse : ResponsePacket, IResponse<WorldRaidLobbyRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_Lobby;
    public List<WorldRaidClearHistoryDB> ClearHistoryDBs;
    public List<WorldRaidLocalBossDB> LocalBossDBs;
    public List<WorldRaidBossGroup> BossGroups;
}

public class WorldRaidBossListResponse : ResponsePacket, IResponse<WorldRaidBossListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_BossList;
    public List<WorldRaidBossListInfoDB> BossListInfoDBs;
}

public class WorldRaidEnterBattleResponse : ResponsePacket, IResponse<WorldRaidEnterBattleRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_EnterBattle;
    public RaidBattleDB RaidBattleDB;
    public List<AssistCharacterDB> AssistCharacterDBs;
}

public class WorldRaidBattleResultResponse : ResponsePacket, IResponse<WorldRaidBattleResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_BattleResult;
    public ParcelResultDB ParcelResultDB;
}

public class WorldRaidReceiveRewardResponse : ResponsePacket, IResponse<WorldRaidReceiveRewardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_ReceiveReward;
    public ParcelResultDB ParcelResultDB;
}

public class ResetableContentGetResponse : ResponsePacket, IResponse<ResetableContentGetRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ResetableContent_Get;
    public List<ResetableContentValueDB> ResetableContentValueDBs;
}

public class ConquestGetInfoResponse : ResponsePacket, IResponse<ConquestGetInfoRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_GetInfo;
    public ConquestInfoDB ConquestInfoDB;
    public List<ConquestTileDB> ConquestedTileDBs;
    public TypedJsonWrapper<List<ConquestEventObjectDB>> ConquestObjectDBsWrapper;
    public List<ConquestEchelonDB> ConquestEchelonDBs;
    public Dictionary<StageDifficulty, int> DifficultyToStepDict;
    public ParcelResultDB InitializeParcelResultDB;
}

public class ConquestConquerResponse : ResponsePacket, IResponse<ConquestConquerRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_Conquer;
    public ParcelResultDB ParcelResultDB;
    public ConquestTileDB ConquestTileDB;
    public ConquestInfoDB ConquestInfoDB;
    public TypedJsonWrapper<List<ConquestEventObjectDB>> ConquestEventObjectDBWrapper;
    public List<ConquestDisplayInfo> DisplayInfos;
}

public class ConquestConquerWithBattleStartResponse : ResponsePacket, IResponse<ConquestConquerWithBattleStartRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_ConquerWithBattleStart;
    public ParcelResultDB ParcelResultDB;
    public ConquestStageSaveDB ConquestStageSaveDB;
}

public class ConquestConquerWithBattleResultResponse : ResponsePacket, IResponse<ConquestConquerWithBattleResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_ConquerWithBattleResult;
    public ParcelResultDB ParcelResultDB;
    public ConquestTileDB ConquestTileDB;
    public ConquestInfoDB ConquestInfoDB;
    public TypedJsonWrapper<List<ConquestEventObjectDB>> ConquestEventObjectDBWrapper;
    public List<ConquestDisplayInfo> DisplayInfos;
    public int StepAfterBattle;
}

public class ConquestManageBaseResponse : ResponsePacket, IResponse<ConquestManageBaseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_ManageBase;
    public List<List<ParcelInfo>> ClearParcels;
    public List<List<ParcelInfo>> ConquerBonusParcels;
    public List<ParcelInfo> BonusParcels;
    public ParcelResultDB ParcelResultDB;
    public ConquestInfoDB ConquestInfoDB;
    public TypedJsonWrapper<List<ConquestEventObjectDB>> ConquestEventObjectDBWrapper;
    public List<ConquestDisplayInfo> DisplayInfos;
}

public class ConquestUpgradeBaseResponse : ResponsePacket, IResponse<ConquestUpgradeBaseRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_UpgradeBase;
    public List<ParcelInfo> UpgradeRewards;
    public ParcelResultDB ParcelResultDB;
    public ConquestTileDB ConquestTileDB;
    public ConquestInfoDB ConquestInfoDB;
    public TypedJsonWrapper<List<ConquestEventObjectDB>> ConquestEventObjectDBWrapper;
    public List<ConquestDisplayInfo> DisplayInfos;
}

public class ConquestTakeEventObjectResponse : ResponsePacket, IResponse<ConquestTakeEventObjectRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_TakeEventObject;
    public ParcelResultDB ParcelResultDB;
    public TypedJsonWrapper<ConquestEventObjectDB> ConquestEventObjectDBWrapper;
}

public class ConquestEventObjectBattleStartResponse : ResponsePacket, IResponse<ConquestEventObjectBattleStartRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_EventObjectBattleStart;
    public ParcelResultDB ParcelResultDB;
    public ConquestStageSaveDB ConquestStageSaveDB;
}

public class ConquestEventObjectBattleResultResponse : ResponsePacket, IResponse<ConquestEventObjectBattleResultRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_EventObjectBattleResult;
    public ParcelResultDB ParcelResultDB;
    public TypedJsonWrapper<ConquestEventObjectDB> ConquestEventObjectDBWrapper;
    public ConquestInfoDB ConquestInfoDB;
    public ConquestTileDB ConquestTileDB;
    public List<ConquestDisplayInfo> DisplayInfos;
}

public class ConquestNormalizeEchelonResponse : ResponsePacket, IResponse<ConquestNormalizeEchelonRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_NormalizeEchelon;
    public ConquestEchelonDB ConquestEchelonDB;
}

public class ConquestCheckResponse : ResponsePacket, IResponse<ConquestCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_Check;
    public bool CanReceiveCalculateReward;
}

public class FriendListResponse : ResponsePacket, IResponse<FriendListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_List;
    public FriendIdCardDB FriendIdCardDB;
    public IdCardBackgroundDB[] IdCardBackgroundDBs;
    public FriendDB[] FriendDBs;
    public FriendDB[] SentRequestFriendDBs;
    public FriendDB[] ReceivedRequestFriendDBs;
}

public class FriendRemoveResponse : ResponsePacket, IResponse<FriendRemoveRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_Remove;
    public FriendDB[] FriendDBs;
    public FriendDB[] SentRequestFriendDBs;
    public FriendDB[] ReceivedRequestFriendDBs;
}

public class FriendGetFriendDetailedInfoResponse : ResponsePacket, IResponse<FriendGetFriendDetailedInfoRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_GetFriendDetailedInfo;
    public string Nickname;
    public long Level;
    public string ClanName;
    public string Comment;
    public long FriendCount;
    public string FriendCode;
    public long RepresentCharacterUniqueId;
    public long CharacterCount;
    public Int64? LastNormalCampaignClearStageId;
    public Int64? LastHardCampaignClearStageId;
    public Int64? ArenaRanking;
    public Int64? RaidRanking;
    public Int32? RaidTier;
    public AssistCharacterDB[] AssistCharacterDBs;
}

public class FriendGetIdCardResponse : ResponsePacket, IResponse<FriendGetIdCardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_GetIdCard;
    public FriendIdCardDB FriendIdCardDB;
}

public class FriendSetIdCardResponse : ResponsePacket, IResponse<FriendSetIdCardRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_SetIdCard;
}

public class FriendSearchResponse : ResponsePacket, IResponse<FriendSearchRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_Search;
    public FriendDB[] SearchResult;
}

public class FriendSendFriendRequestResponse : ResponsePacket, IResponse<FriendSendFriendRequestRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_SendFriendRequest;
    public FriendDB[] FriendDBs;
    public FriendDB[] SentRequestFriendDBs;
    public FriendDB[] ReceivedRequestFriendDBs;
}

public class FriendAcceptFriendRequestResponse : ResponsePacket, IResponse<FriendAcceptFriendRequestRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_AcceptFriendRequest;
    public FriendDB[] FriendDBs;
    public FriendDB[] SentRequestFriendDBs;
    public FriendDB[] ReceivedRequestFriendDBs;
}

public class FriendDeclineFriendRequestResponse : ResponsePacket, IResponse<FriendDeclineFriendRequestRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_DeclineFriendRequest;
    public FriendDB[] FriendDBs;
    public FriendDB[] SentRequestFriendDBs;
    public FriendDB[] ReceivedRequestFriendDBs;
}

public class FriendCancelFriendRequestResponse : ResponsePacket, IResponse<FriendCancelFriendRequestRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_CancelFriendRequest;
    public FriendDB[] FriendDBs;
    public FriendDB[] SentRequestFriendDBs;
    public FriendDB[] ReceivedRequestFriendDBs;
}

public class FriendCheckResponse : ResponsePacket, IResponse<FriendCheckRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_Check;
}

public class CharacterGearListResponse : ResponsePacket, IResponse<CharacterGearListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CharacterGear_List;
    public List<GearDB> GearDBs;
}

public class CharacterGearUnlockResponse : ResponsePacket, IResponse<CharacterGearUnlockRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CharacterGear_Unlock;
    public GearDB GearDB;
    public CharacterDB CharacterDB;
}

public class CharacterGearTierUpResponse : ResponsePacket, IResponse<CharacterGearTierUpRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CharacterGear_TierUp;
    public GearDB GearDB;
    public ParcelResultDB ParcelResultDB;
    public ConsumeResultDB ConsumeResultDB;
}

public class QueuingGetTicketResponse : ResponsePacket, IResponse<QueuingGetTicketRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Queuing_GetTicketGL;
    public string WaitingTicket;
    public string EnterTicket;
    public long TicketSequence;
    public long AllowedSequence;
    public long RequiredSecondsPerUser;
    public string Birth;
    public string ServerSeed;
}

public class ManagementBannerListResponse : ResponsePacket, IResponse<ManagementBannerListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Management_BannerList;
    public List<BannerDB> BannerDBs;
}

public class ManagementContentsLockListResponse : ResponsePacket, IResponse<ManagementContentsLockListRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Management_ContentsLockList;
    public List<ContentsLockDB> ContentsLockDBs;
}

public class CommonCheatResponse : ResponsePacket, IResponse<CommonCheatRequest>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Common_Cheat;
    public AccountDB Account;
    public AccountCurrencyDB AccountCurrency;
    public List<CharacterDB> CharacterDBs;
    public List<EquipmentDB> EquipmentDBs;
    public List<WeaponDB> WeaponDBs;
    public List<ItemDB> ItemDBs;
    public List<ScenarioHistoryDB> ScenarioHistoryDBs;
    public List<ScenarioGroupHistoryDB> ScenarioGroupHistoryDBs;
    public CheatFlags CheatFlags;
}

