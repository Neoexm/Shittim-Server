namespace BlueArchiveAPI.NetworkModels;
using System.Collections.ObjectModel;

public class SystemVersionRequest : RequestPacket, IRequest<SystemVersionResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.System_Version;
}

public class SessionInfoRequest : RequestPacket, IRequest<SessionInfoResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Session_Info;
}

public class NetworkTimeSyncRequest : RequestPacket, IRequest<NetworkTimeSyncResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.NetworkTime_Sync;
    public long SendTick;
    public long ReceiveTick;
    public long EchoSendTick;
    public long EchoReceiveTick;
}

public class AuditGachaStatisticsRequest : RequestPacket, IRequest<AuditGachaStatisticsResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Audit_GachaStatistics;
    public long MerchandiseUniqueId;
    public long ShopUniqueId;
    public long Count;
}

public class AccountCreateRequest : RequestPacket, IRequest<AccountCreateResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Create;
    public string DevId;
    public long Version;
    public long IMEI;
    public string AccessIP;
    public string MarketId;
    public string UserType;
    public string AdvertisementId;
    public string OSType;
    public string OSVersion;
    public string CountryCode;
}

public class AccountNicknameRequest : RequestPacket, IRequest<AccountNicknameResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Nickname;
    public string Nickname;
}

public class AccountAuthRequest : RequestPacket, IRequest<AccountAuthResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Auth;
    public long Version;
    public string DevId;
    public long IMEI;
    public string AccessIP;
    public string MarketId;
    public string UserType;
    public string AdvertisementId;
    public string OSType;
    public string OSVersion;
    public string DeviceUniqueId;
    public string DeviceModel;
    public int DeviceSystemMemorySize;
    public string CountryCode;
    public string Idfv;
    public bool IsTeenVersion;
}

public class AccountCurrencySyncRequest : RequestPacket, IRequest<AccountCurrencySyncResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CurrencySync;
}

public class AccountSetRepresentCharacterAndCommentRequest : RequestPacket, IRequest<AccountSetRepresentCharacterAndCommentResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_SetRepresentCharacterAndComment;
    public int RepresentCharacterServerId;
    public string Comment;
}

public class AccountGetTutorialRequest : RequestPacket, IRequest<AccountGetTutorialResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_GetTutorial;
}

public class AccountSetTutorialRequest : RequestPacket, IRequest<AccountSetTutorialResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_SetTutorial;
    public List<long> TutorialIds;
}

public class AccountPassCheckRequest : RequestPacket, IRequest<AccountPassCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_PassCheck;
    public string DevId;
}

public class AccountCheckYostarRequest : RequestPacket, IRequest<AccountCheckYostarResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CheckYostar;
    public long UID;
    public string YostarToken;
    public string EnterTicket;
    public bool PassCookieResult;
    public string Cookie;
}

public class AccountCallNameRequest : RequestPacket, IRequest<AccountCallNameResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CallName;
    public string CallName;
    public string CallNameKatakana;
}

public class AccountBirthDayRequest : RequestPacket, IRequest<AccountBirthDayResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_BirthDay;
    public DateTime BirthDay;
}

public class AccountAuth2Request : AccountAuthRequest, IRequest<AccountAuth2Response>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_Auth2;
}

public class AccountLinkRewardRequest : RequestPacket, IRequest<AccountLinkRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_LinkReward;
}

public class AccountCheckNexonRequest : RequestPacket, IRequest<AccountCheckNexonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_CheckNexon;
    public long NpSN;
    public string NpToken;
    public bool PassCheckNexonServer;
    public string EnterTicket;
}

public class AccountDetachNexonRequest : RequestPacket, IRequest<AccountDetachNexonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_DetachNexon;
}

public class AccountReportXignCodeCheaterRequest : RequestPacket, IRequest<AccountReportXignCodeCheaterResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_ReportXignCodeCheater;
    public string ErrorCode;
}

public class AccountDismissRepurchasablePopupRequest : RequestPacket, IRequest<AccountDismissRepurchasablePopupResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_DismissRepurchasablePopup;
    public List<long> ProductIds;
}

public class AccountInvalidateTokenRequest : RequestPacket, IRequest<AccountInvalidateTokenResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_InvalidateToken;
}

public class AccountLoginSyncRequest : RequestPacket, IRequest<AccountLoginSyncResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_LoginSync;
    public List<Protocol> SyncProtocols;
}

public class AccountVerifyAdultCheckRequest : RequestPacket, IRequest<AccountVerifyAdultCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Account_VerifyCheckAdultAgree;
}

public class CharacterListRequest : RequestPacket, IRequest<CharacterListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_List;
}

public class CharacterTranscendenceRequest : RequestPacket, IRequest<CharacterTranscendenceResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_Transcendence;
    public long TargetCharacterServerId;
}

public class CharacterExpGrowthRequest : RequestPacket, IRequest<CharacterExpGrowthResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_ExpGrowth;
    public long TargetCharacterServerId;
    public ConsumeRequestDB ConsumeRequestDB;
}

public class CharacterFavorGrowthRequest : RequestPacket, IRequest<CharacterFavorGrowthResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_FavorGrowth;
    public long TargetCharacterDBId;
    public Dictionary<long, int> ConsumeItemDBIdsAndCounts;
}

public class CharacterUnlockWeaponRequest : RequestPacket, IRequest<CharacterUnlockWeaponResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_UnlockWeapon;
    public long TargetCharacterServerId;
}

public class CharacterWeaponExpGrowthRequest : RequestPacket, IRequest<CharacterWeaponExpGrowthResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_WeaponExpGrowth;
    public long TargetCharacterServerId;
    public Dictionary<long, long> ConsumeUniqueIdAndCounts;
}

public class CharacterWeaponTranscendenceRequest : RequestPacket, IRequest<CharacterWeaponTranscendenceResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_WeaponTranscendence;
    public long TargetCharacterServerId;
}

public class CharacterSetFavoritesRequest : RequestPacket, IRequest<CharacterSetFavoritesResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Character_SetFavorites;
    public Dictionary<long, bool> ActivateByServerIds;
}

public class EquipmentBatchGrowthRequest : RequestPacket, IRequest<EquipmentBatchGrowthResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Equipment_BatchGrowth;
    public List<EquipmentBatchGrowthRequestDB> EquipmentBatchGrowthRequestDBs;
}

public class ItemListRequest : RequestPacket, IRequest<ItemListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_List;
}

public class ItemSellRequest : RequestPacket, IRequest<ItemSellResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_Sell;
    public List<long> TargetServerIds;
}

public class ItemConsumeRequest : RequestPacket, IRequest<ItemConsumeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_Consume;
    public long TargetItemServerId;
    public int ConsumeCount;
}

public class ItemLockRequest : RequestPacket, IRequest<ItemLockResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_Lock;
    public long TargetServerId;
    public bool IsLocked;
}

public class ItemBulkConsumeRequest : RequestPacket, IRequest<ItemBulkConsumeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_BulkConsume;
    public long TargetItemServerId;
    public int ConsumeCount;
}

public class ItemSelectTicketRequest : RequestPacket, IRequest<ItemSelectTicketResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Item_SelectTicket;
    public long TicketItemServerId;
    public long SelectItemUniqueId;
    public int ConsumeCount;
}

public class EchelonListRequest : RequestPacket, IRequest<EchelonListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_List;
}

public class EchelonSaveRequest : RequestPacket, IRequest<EchelonSaveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_Save;
    public EchelonDB EchelonDB;
    public List<ClanAssistUseInfo> AssistUseInfos;
}

public class EchelonPresetListRequest : RequestPacket, IRequest<EchelonPresetListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_PresetList;
}

public class EchelonPresetSaveRequest : RequestPacket, IRequest<EchelonPresetSaveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_PresetSave;
    public EchelonPresetDB PresetDB;
}

public class EchelonPresetGroupRenameRequest : RequestPacket, IRequest<EchelonPresetGroupRenameResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Echelon_PresetGroupRename;
    public int PresetGroupIndex;
    public string PresetGroupLabel;
}

public class CampaignListRequest : RequestPacket, IRequest<CampaignListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_List;
}

public class CampaignEnterMainStageRequest : RequestPacket, IRequest<CampaignEnterMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterMainStage;
    public long StageUniqueId;
}

public class CampaignConfirmMainStageRequest : RequestPacket, IRequest<CampaignConfirmMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_ConfirmMainStage;
    public long StageUniqueId;
}

public class CampaignDeployEchelonRequest : RequestPacket, IRequest<CampaignDeployEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_DeployEchelon;
    public long StageUniqueId;
    public List<HexaUnit> DeployedEchelons;
}

public class CampaignWithdrawEchelonRequest : RequestPacket, IRequest<CampaignWithdrawEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_WithdrawEchelon;
    public long StageUniqueId;
    public List<long> WithdrawEchelonEntityId;
}

public class CampaignMapMoveRequest : RequestPacket, IRequest<CampaignMapMoveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_MapMove;
    public long StageUniqueId;
    public long EchelonEntityId;
    public HexLocation DestPosition;
}

public class CampaignEndTurnRequest : RequestPacket, IRequest<CampaignEndTurnResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EndTurn;
    public long StageUniqueId;
}

public class CampaignEnterTacticRequest : RequestPacket, IRequest<CampaignEnterTacticResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterTactic;
    public long StageUniqueId;
    public long EchelonIndex;
    public long EnemyIndex;
}

public class CampaignTacticResultRequest : RequestPacket, IRequest<CampaignTacticResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_TacticResult;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
    public SkillCardHand Hand;
    public TacticSkipSummary SkipSummary;
}

public class CampaignRetreatRequest : RequestPacket, IRequest<CampaignRetreatResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_Retreat;
    public long StageUniqueId;
}

public class CampaignChapterClearRewardRequest : RequestPacket, IRequest<CampaignChapterClearRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_ChapterClearReward;
    public long CampaignChapterUniqueId;
    public StageDifficulty StageDifficulty;
}

public class CampaignHealRequest : RequestPacket, IRequest<CampaignHealResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_Heal;
    public long CampaignStageUniqueId;
    public long EchelonIndex;
    public long CharacterServerId;
}

public class CampaignEnterSubStageRequest : RequestPacket, IRequest<CampaignEnterSubStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterSubStage;
    public long StageUniqueId;
    public long LastEnterStageEchelonNumber;
}

public class CampaignSubStageResultRequest : RequestPacket, IRequest<CampaignSubStageResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_SubStageResult;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
}

public class CampaignPortalRequest : RequestPacket, IRequest<CampaignPortalResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_Portal;
    public long StageUniqueId;
    public long EchelonEntityId;
}

public class CampaignConfirmTutorialStageRequest : RequestPacket, IRequest<CampaignConfirmTutorialStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_ConfirmTutorialStage;
    public long StageUniqueId;
}

public class CampaignPurchasePlayCountHardStageRequest : RequestPacket, IRequest<CampaignPurchasePlayCountHardStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_PurchasePlayCountHardStage;
    public long StageUniqueId;
}

public class CampaignEnterTutorialStageRequest : RequestPacket, IRequest<CampaignEnterTutorialStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_EnterTutorialStage;
    public long StageUniqueId;
}

public class CampaignTutorialStageResultRequest : RequestPacket, IRequest<CampaignTutorialStageResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_TutorialStageResult;
    public BattleSummary Summary;
}

public class CampaignRestartMainStageRequest : RequestPacket, IRequest<CampaignRestartMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Campaign_RestartMainStage;
    public long StageUniqueId;
}

public class MailListRequest : RequestPacket, IRequest<MailListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mail_List;
    public bool IsReadMail;
    public DateTime PivotTime;
    public long PivotIndex;
    public bool IsDescending;
}

public class MailCheckRequest : RequestPacket, IRequest<MailCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mail_Check;
}

public class MailReceiveRequest : RequestPacket, IRequest<MailReceiveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mail_Receive;
    public List<long> MailServerIds;
}

public class MissionListRequest : RequestPacket, IRequest<MissionListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_List;
    public Int64? EventContentId;
}

public class MissionRewardRequest : RequestPacket, IRequest<MissionRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_Reward;
    public long MissionUniqueId;
    public long ProgressServerId;
    public Int64? EventContentId;
}

public class MissionMultipleRewardRequest : RequestPacket, IRequest<MissionMultipleRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_MultipleReward;
    public MissionCategory MissionCategory;
    public Int64? GuideMissionSeasonId;
    public Int64? EventContentId;
}

public class MissionSyncRequest : RequestPacket, IRequest<MissionSyncResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Mission_Sync;
}

public class AttendanceRewardRequest : RequestPacket, IRequest<AttendanceRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Attendance_Reward;
    public Dictionary<long, long> DayByBookUniqueId;
    public long AttendanceBookUniqueId;
    public long Day;
}

public class ShopBuyMerchandiseRequest : RequestPacket, IRequest<ShopBuyMerchandiseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyMerchandise;
    public bool IsRefreshGoods;
    public long ShopUniqueId;
    public long GoodsId;
    public long PurchaseCount;
}

public class ShopBuyGachaRequest : RequestPacket, IRequest<ShopBuyGachaResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyGacha;
    public long GoodsId;
    public long ShopUniqueId;
}

public class ShopListRequest : RequestPacket, IRequest<ShopListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_List;
    public List<ShopCategoryType> CategoryList;
}

public class ShopRefreshRequest : RequestPacket, IRequest<ShopRefreshResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_Refresh;
    public ShopCategoryType ShopCategoryType;
}

public class ShopBuyEligmaRequest : RequestPacket, IRequest<ShopBuyEligmaResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyEligma;
    public long GoodsUniqueId;
    public long ShopUniqueId;
    public long CharacterUniqueId;
    public long PurchaseCount;
}

public class ShopBuyGacha2Request : ShopBuyGachaRequest, IRequest<ShopBuyGacha2Response>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyGacha2;
}

public class ShopGachaRecruitListRequest : RequestPacket, IRequest<ShopGachaRecruitListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_GachaRecruitList;
}

public class ShopBuyRefreshMerchandiseRequest : RequestPacket, IRequest<ShopBuyRefreshMerchandiseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyRefreshMerchandise;
    public List<long> ShopUniqueIds;
}

public class ShopBuyGacha3Request : ShopBuyGacha2Request, IRequest<ShopBuyGacha3Response>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyGacha3;
    public long FreeRecruitId;
    public ParcelCost Cost;
}

public class ShopBuyAPRequest : RequestPacket, IRequest<ShopBuyAPResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Shop_BuyAP;
    public long ShopUniqueId;
    public long PurchaseCount;
}

public class RecipeCraftRequest : RequestPacket, IRequest<RecipeCraftResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Recipe_Craft;
    public long RecipeCraftUniqueId;
    public long RecipeIngredientUniqueId;
}

public class MemoryLobbyListRequest : RequestPacket, IRequest<MemoryLobbyListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_List;
}

public class MemoryLobbySetMainRequest : RequestPacket, IRequest<MemoryLobbySetMainResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_SetMain;
    public long MemoryLobbyId;
}

public class MemoryLobbyUpdateLobbyModeRequest : RequestPacket, IRequest<MemoryLobbyUpdateLobbyModeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_UpdateLobbyMode;
    public bool IsMemoryLobbyMode;
}

public class MemoryLobbyInteractRequest : RequestPacket, IRequest<MemoryLobbyInteractResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MemoryLobby_Interact;
}

public class CumulativeTimeRewardListRequest : RequestPacket, IRequest<CumulativeTimeRewardListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CumulativeTimeReward_List;
}

public class OpenConditionListRequest : RequestPacket, IRequest<OpenConditionListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.OpenCondition_List;
}

public class OpenConditionSetRequest : RequestPacket, IRequest<OpenConditionSetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.OpenCondition_Set;
    public OpenConditionDB ConditionDB;
}

public class OpenConditionEventListRequest : RequestPacket, IRequest<OpenConditionEventListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.OpenCondition_EventList;
    public List<long> ConquestEventIds;
    public Dictionary<long, long> WorldRaidSeasonAndGroupIds;
}

public class ToastListRequest : RequestPacket, IRequest<ToastListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Toast_List;
}

public class RaidListRequest : RequestPacket, IRequest<RaidListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_List;
    public string RaidBossGroup;
    public Difficulty RaidDifficulty;
    public RaidRoomSortOption RaidRoomSortOption;
}

public class RaidCompleteListRequest : RequestPacket, IRequest<RaidCompleteListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_CompleteList;
}

public class RaidDetailRequest : RequestPacket, IRequest<RaidDetailResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Detail;
    public long RaidServerId;
    public long RaidUniqueId;
}

public class RaidSearchRequest : RequestPacket, IRequest<RaidSearchResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Search;
    public string SecretCode;
    public List<string> Tags;
}

public class RaidCreateBattleRequest : RequestPacket, IRequest<RaidCreateBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_CreateBattle;
    public long RaidUniqueId;
    public bool IsPractice;
    public List<int> Tags;
    public bool IsPublic;
    public Difficulty Difficulty;
    public ClanAssistUseInfo AssistUseInfo;
}

public class RaidEnterBattleRequest : RequestPacket, IRequest<RaidEnterBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_EnterBattle;
    public long RaidServerId;
    public long RaidUniqueId;
    public bool IsPractice;
    public ClanAssistUseInfo AssistUseInfo;
}

public class RaidBattleUpdateRequest : RequestPacket, IRequest<RaidBattleUpdateResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_BattleUpdate;
    public long RaidServerId;
    public int RaidBossIndex;
    public long CumulativeDamage;
    public long CumulativeGroggyPoint;
}

public class RaidEndBattleRequest : RequestPacket, IRequest<RaidEndBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_EndBattle;
    public int EchelonId;
    public long RaidServerId;
    public bool IsPractice;
    public BattleSummary Summary;
    public ClanAssistUseInfo AssistUseInfo;
}

public class RaidRewardRequest : RequestPacket, IRequest<RaidRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Reward;
    public long RaidServerId;
    public bool IsPractice;
}

public class RaidRewardAllRequest : RequestPacket, IRequest<RaidRewardAllResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_RewardAll;
}

public class RaidShareRequest : RequestPacket, IRequest<RaidShareResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Share;
    public long RaidServerId;
}

public class RaidSeasonRewardRequest : RequestPacket, IRequest<RaidSeasonRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_SeasonReward;
}

public class RaidLobbyRequest : RequestPacket, IRequest<RaidLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Lobby;
}

public class RaidGiveUpRequest : RequestPacket, IRequest<RaidGiveUpResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_GiveUp;
    public long RaidServerId;
    public bool IsPractice;
}

public class RaidOpponentListRequest : RequestPacket, IRequest<RaidOpponentListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_OpponentList;
    public Int64? Rank;
    public Int64? Score;
    public bool IsUpper;
    public RankingSearchType SearchType;
}

public class RaidRankingRewardRequest : RequestPacket, IRequest<RaidRankingRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_RankingReward;
}

public class RaidLoginRequest : RequestPacket, IRequest<RaidLoginResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Login;
}

public class RaidSweepRequest : RequestPacket, IRequest<RaidSweepResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_Sweep;
    public long UniqueId;
    public long SweepCount;
}

public class RaidGetBestTeamRequest : RequestPacket, IRequest<RaidGetBestTeamResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Raid_GetBestTeam;
    public long SearchAccountId;
}

public class SkipHistoryListRequest : RequestPacket, IRequest<SkipHistoryListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SkipHistory_List;
}

public class SkipHistorySaveRequest : RequestPacket, IRequest<SkipHistorySaveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SkipHistory_Save;
    public SkipHistoryDB SkipHistoryDB;
}

public class ScenarioListRequest : RequestPacket, IRequest<ScenarioListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_List;
}

public class ScenarioClearRequest : RequestPacket, IRequest<ScenarioClearResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Clear;
    public long ScenarioId;
    public BattleSummary BattleSummary;
}

public class ScenarioGroupHistoryUpdateRequest : RequestPacket, IRequest<ScenarioGroupHistoryUpdateResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_GroupHistoryUpdate;
    public long ScenarioGroupUniqueId;
    public long ScenarioType;
}

public class ScenarioSkipRequest : RequestPacket, IRequest<ScenarioSkipResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Skip;
    public long ScriptGroupId;
    public int SkipPointScriptCount;
}

public class ScenarioSelectRequest : RequestPacket, IRequest<ScenarioSelectResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Select;
    public long ScriptGroupId;
    public long ScriptSelectGroup;
}

public class ScenarioAccountStudentChangeRequest : RequestPacket, IRequest<ScenarioAccountStudentChangeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_AccountStudentChange;
    public long AccountStudent;
    public long AccountStudentBefore;
}

public class ScenarioLobbyStudentChangeRequest : RequestPacket, IRequest<ScenarioLobbyStudentChangeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_LobbyStudentChange;
    public List<long> LobbyStudents;
    public List<long> LobbyStudentsBefore;
}

public class ScenarioSpecialLobbyChangeRequest : RequestPacket, IRequest<ScenarioSpecialLobbyChangeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_SpecialLobbyChange;
    public long MemoryLobbyId;
    public long MemoryLobbyIdBefore;
}

public class ScenarioEnterRequest : RequestPacket, IRequest<ScenarioEnterResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Enter;
    public long ScenarioId;
}

public class ScenarioEnterMainStageRequest : RequestPacket, IRequest<ScenarioEnterMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_EnterMainStage;
    public long StageUniqueId;
}

public class ScenarioConfirmMainStageRequest : RequestPacket, IRequest<ScenarioConfirmMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_ConfirmMainStage;
    public long StageUniqueId;
}

public class ScenarioDeployEchelonRequest : RequestPacket, IRequest<ScenarioDeployEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_DeployEchelon;
    public long StageUniqueId;
    public List<HexaUnit> DeployedEchelons;
}

public class ScenarioWithdrawEchelonRequest : RequestPacket, IRequest<ScenarioWithdrawEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_WithdrawEchelon;
    public long StageUniqueId;
    public List<long> WithdrawEchelonEntityId;
}

public class ScenarioMapMoveRequest : RequestPacket, IRequest<ScenarioMapMoveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_MapMove;
    public long StageUniqueId;
    public long EchelonEntityId;
    public HexLocation DestPosition;
}

public class ScenarioEndTurnRequest : RequestPacket, IRequest<ScenarioEndTurnResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_EndTurn;
    public long StageUniqueId;
}

public class ScenarioEnterTacticRequest : RequestPacket, IRequest<ScenarioEnterTacticResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_EnterTactic;
    public long StageUniqueId;
    public long EchelonIndex;
    public long EnemyIndex;
}

public class ScenarioTacticResultRequest : RequestPacket, IRequest<ScenarioTacticResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_TacticResult;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
    public SkillCardHand Hand;
    public TacticSkipSummary SkipSummary;
}

public class ScenarioRetreatRequest : RequestPacket, IRequest<ScenarioRetreatResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Retreat;
    public long StageUniqueId;
}

public class ScenarioPortalRequest : RequestPacket, IRequest<ScenarioPortalResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_Portal;
    public long StageUniqueId;
    public long EchelonEntityId;
}

public class ScenarioRestartMainStageRequest : RequestPacket, IRequest<ScenarioRestartMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_RestartMainStage;
    public long StageUniqueId;
}

public class ScenarioSkipMainStageRequest : RequestPacket, IRequest<ScenarioSkipMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Scenario_SkipMainStage;
    public long StageUniqueId;
}

public class CafeGetInfoRequest : RequestPacket, IRequest<CafeGetInfoResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_Get;
    public long AccountServerId;
}

public class CafeAckRequest : RequestPacket, IRequest<CafeAckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_Ack;
}

public class CafeListPresetRequest : RequestPacket, IRequest<CafeListPresetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ListPreset;
}

public class CafeRenamePresetRequest : RequestPacket, IRequest<CafeRenamePresetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_RenamePreset;
    public int SlotId;
    public string PresetName;
}

public class CafeClearPresetRequest : RequestPacket, IRequest<CafeClearPresetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ClearPreset;
    public int SlotId;
}

public class CafeUpdatePresetFurnitureRequest : RequestPacket, IRequest<CafeUpdatePresetFurnitureResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_UpdatePresetFurniture;
    public int SlotId;
}

public class CafeApplyPresetRequest : RequestPacket, IRequest<CafeApplyPresetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ApplyPreset;
    public int SlotId;
}

public class CafeRankUpRequest : RequestPacket, IRequest<CafeRankUpResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_RankUp;
    public long AccountServerId;
    public ConsumeRequestDB ConsumeRequestDB;
}

public class CafeReceiveCurrencyRequest : RequestPacket, IRequest<CafeReceiveCurrencyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_ReceiveCurrency;
    public long AccountServerId;
}

public class CafeGiveGiftRequest : RequestPacket, IRequest<CafeGiveGiftResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_GiveGift;
    public long CharacterUniqueId;
    public ConsumeRequestDB ConsumeRequestDB;
}

public class CafeSummonCharacterRequest : RequestPacket, IRequest<CafeSummonCharacterResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_SummonCharacter;
    public long CharacterServerId;
}

public class CafeTrophyHistoryRequest : RequestPacket, IRequest<CafeTrophyHistoryResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Cafe_TrophyHistory;
}

public class CraftSelectNodeRequest : RequestPacket, IRequest<CraftSelectNodeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_SelectNode;
    public long SlotId;
    public long LeafNodeIndex;
}

public class CraftUpdateNodeLevelRequest : RequestPacket, IRequest<CraftUpdateNodeLevelResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_UpdateNodeLevel;
    public ConsumeRequestDB ConsumeRequestDB;
    public long ConsumeGoldAmount;
    public long SlotId;
    public CraftNodeTier CraftNodeType;
}

public class CraftBeginProcessRequest : RequestPacket, IRequest<CraftBeginProcessResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_BeginProcess;
    public long SlotId;
}

public class CraftCompleteProcessRequest : RequestPacket, IRequest<CraftCompleteProcessResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_CompleteProcess;
    public long SlotId;
}

public class CraftRewardRequest : RequestPacket, IRequest<CraftRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_Reward;
    public long SlotId;
}

public class CraftShiftingBeginProcessRequest : RequestPacket, IRequest<CraftShiftingBeginProcessResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_ShiftingBeginProcess;
    public long SlotId;
    public long RecipeId;
    public ConsumeRequestDB ConsumeRequestDB;
}

public class CraftShiftingCompleteProcessRequest : RequestPacket, IRequest<CraftShiftingCompleteProcessResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_ShiftingCompleteProcess;
    public long SlotId;
}

public class CraftShiftingRewardRequest : RequestPacket, IRequest<CraftShiftingRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Craft_ShiftingReward;
    public long SlotId;
}

public class ArenaEnterLobbyRequest : RequestPacket, IRequest<ArenaEnterLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterLobby;
}

public class ArenaLoginRequest : RequestPacket, IRequest<ArenaLoginResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_Login;
}

public class ArenaSettingChangeRequest : RequestPacket, IRequest<ArenaSettingChangeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_SettingChange;
    public long MapId;
}

public class ArenaOpponentListRequest : RequestPacket, IRequest<ArenaOpponentListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_OpponentList;
}

public class ArenaEnterBattleRequest : RequestPacket, IRequest<ArenaEnterBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterBattle;
    public long OpponentAccountServerId;
    public long OpponentIndex;
}

public class ArenaEnterBattlePart1Request : RequestPacket, IRequest<ArenaEnterBattlePart1Response>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterBattlePart1;
    public long OpponentAccountServerId;
    public long OpponentRank;
    public int OpponentIndex;
}

public class ArenaEnterBattlePart2Request : RequestPacket, IRequest<ArenaEnterBattlePart2Response>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_EnterBattlePart2;
    public ArenaBattleDB ArenaBattleDB;
}

public class ArenaBattleResultRequest : RequestPacket, IRequest<ArenaBattleResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_BattleResult;
    public ArenaBattleDB ArenaBattleDB;
}

public class ArenaCumulativeTimeRewardRequest : RequestPacket, IRequest<ArenaCumulativeTimeRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_CumulativeTimeReward;
}

public class ArenaDailyRewardRequest : RequestPacket, IRequest<ArenaDailyRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_DailyReward;
}

public class ArenaRankListRequest : RequestPacket, IRequest<ArenaRankListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_RankList;
    public int StartIndex;
    public int Count;
}

public class ArenaHistoryRequest : RequestPacket, IRequest<ArenaHistoryResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_History;
    public DateTime SearchStartDate;
    public int Count;
}

public class ArenaCheckSeasonCloseRewardRequest : RequestPacket, IRequest<ArenaCheckSeasonCloseRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_CheckSeasonCloseReward;
}

public class ArenaSyncEchelonSettingTimeRequest : RequestPacket, IRequest<ArenaSyncEchelonSettingTimeResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Arena_SyncEchelonSettingTime;
}

public class WeekDungeonListRequest : RequestPacket, IRequest<WeekDungeonListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_List;
}

public class WeekDungeonEnterBattleRequest : RequestPacket, IRequest<WeekDungeonEnterBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_EnterBattle;
    public long StageUniqueId;
    public long EchelonIndex;
}

public class WeekDungeonBattleResultRequest : RequestPacket, IRequest<WeekDungeonBattleResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_BattleResult;
    public long StageUniqueId;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
}

public class WeekDungeonRetreatRequest : RequestPacket, IRequest<WeekDungeonRetreatResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WeekDungeon_Retreat;
    public long StageUniqueId;
}

public class AcademyGetInfoRequest : RequestPacket, IRequest<AcademyGetInfoResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Academy_GetInfo;
}

public class AcademyAttendScheduleRequest : RequestPacket, IRequest<AcademyAttendScheduleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Academy_AttendSchedule;
    public long ZoneId;
}

public class EventRewardIncreaseRequest : RequestPacket, IRequest<EventRewardIncreaseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Event_RewardIncrease;
}

public class ContentSaveGetRequest : RequestPacket, IRequest<ContentSaveGetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ContentSave_Get;
}

public class ContentSaveDiscardRequest : RequestPacket, IRequest<ContentSaveDiscardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ContentSave_Discard;
    public ContentType ContentType;
    public long StageUniqueId;
}

public class ClanLobbyRequest : RequestPacket, IRequest<ClanLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Lobby;
}

public class ClanLoginRequest : RequestPacket, IRequest<ClanLoginResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Login;
}

public class ClanSearchRequest : RequestPacket, IRequest<ClanSearchResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Search;
    public string SearchString;
    public ClanJoinOption ClanJoinOption;
    public string ClanUniqueCode;
}

public class ClanCreateRequest : RequestPacket, IRequest<ClanCreateResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Create;
    public string ClanNickName;
    public ClanJoinOption ClanJoinOption;
}

public class ClanMemberRequest : RequestPacket, IRequest<ClanMemberResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Member;
    public long ClanDBId;
    public long MemberAccountId;
}

public class ClanApplicantRequest : RequestPacket, IRequest<ClanApplicantResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Applicant;
    public long OffSet;
}

public class ClanJoinRequest : RequestPacket, IRequest<ClanJoinResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Join;
    public long ClanDBId;
}

public class ClanQuitRequest : RequestPacket, IRequest<ClanQuitResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Quit;
}

public class ClanPermitRequest : RequestPacket, IRequest<ClanPermitResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Permit;
    public long ApplicantAccountId;
    public bool IsPerMit;
}

public class ClanKickRequest : RequestPacket, IRequest<ClanKickResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Kick;
    public long MemberAccountId;
}

public class ClanSettingRequest : RequestPacket, IRequest<ClanSettingResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Setting;
    public string ChangedClanName;
    public string ChangedNotice;
    public ClanJoinOption ClanJoinOption;
}

public class ClanConferRequest : RequestPacket, IRequest<ClanConferResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Confer;
    public long MemberAccountId;
    public ClanSocialGrade ConferingGrade;
}

public class ClanDismissRequest : RequestPacket, IRequest<ClanDismissResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Dismiss;
}

public class ClanAutoJoinRequest : RequestPacket, IRequest<ClanAutoJoinResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_AutoJoin;
}

public class ClanMemberListRequest : RequestPacket, IRequest<ClanMemberListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_MemberList;
    public long ClanDBId;
}

public class ClanCancelApplyRequest : RequestPacket, IRequest<ClanCancelApplyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_CancelApply;
}

public class ClanMyAssistListRequest : RequestPacket, IRequest<ClanMyAssistListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_MyAssistList;
}

public class ClanSetAssistRequest : RequestPacket, IRequest<ClanSetAssistResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_SetAssist;
    public EchelonType EchelonType;
    public int SlotNumber;
    public long CharacterDBId;
}

public class ClanChatLogRequest : RequestPacket, IRequest<ClanChatLogResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_ChatLog;
    public string Channel;
    public DateTime FromDate;
}

public class ClanCheckRequest : RequestPacket, IRequest<ClanCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_Check;
}

public class ClanAllAssistListRequest : RequestPacket, IRequest<ClanAllAssistListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Clan_AllAssistList;
    public EchelonType EchelonType;
}

public class BillingTransactionStartByYostarRequest : RequestPacket, IRequest<BillingTransactionStartByYostarResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_TransactionStartByYostar;
    public long ShopCashId;
    public bool VirtualPayment;
}

public class BillingTransactionEndByYostarRequest : RequestPacket, IRequest<BillingTransactionEndByYostarResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_TransactionEndByYostar;
    public long PurchaseOrderId;
    public BillingTransactionEndType EndType;
}

public class BillingPurchaseListByYostarRequest : RequestPacket, IRequest<BillingPurchaseListByYostarResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_PurchaseListByYostar;
}

public class BillingPurchaseCashShopVerifyByNexonRequest : RequestPacket, IRequest<BillingPurchaseCashShopVerifyByNexonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_PurchaseCashShopVerifyByNexon;
    public long NpSN;
    public string StampToken;
    public long ShopCashId;
    public bool VirtualPayment;
    public string CurrencyCode;
    public long CurrencyValue;
}

public class BillingPurchaseListByNexonRequest : RequestPacket, IRequest<BillingPurchaseListByNexonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Billing_PurchaseListByNexon;
}

public class EventContentAdventureListRequest : RequestPacket, IRequest<EventContentAdventureListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_AdventureList;
    public long EventContentId;
}

public class EventContentEnterMainStageRequest : RequestPacket, IRequest<EventContentEnterMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterMainStage;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentConfirmMainStageRequest : RequestPacket, IRequest<EventContentConfirmMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ConfirmMainStage;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentEnterTacticRequest : RequestPacket, IRequest<EventContentEnterTacticResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterTactic;
    public long EventContentId;
    public long StageUniqueId;
    public long EchelonIndex;
    public long EnemyIndex;
}

public class EventContentTacticResultRequest : RequestPacket, IRequest<EventContentTacticResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_TacticResult;
    public long EventContentId;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
    public SkillCardHand Hand;
    public TacticSkipSummary SkipSummary;
}

public class EventContentEnterSubStageRequest : RequestPacket, IRequest<EventContentEnterSubStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterSubStage;
    public long EventContentId;
    public long StageUniqueId;
    public long LastEnterStageEchelonNumber;
}

public class EventContentSubStageResultRequest : RequestPacket, IRequest<EventContentSubStageResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_SubStageResult;
    public long EventContentId;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
}

public class EventContentDeployEchelonRequest : RequestPacket, IRequest<EventContentDeployEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DeployEchelon;
    public long EventContentId;
    public long StageUniqueId;
    public List<HexaUnit> DeployedEchelons;
}

public class EventContentWithdrawEchelonRequest : RequestPacket, IRequest<EventContentWithdrawEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_WithdrawEchelon;
    public long EventContentId;
    public long StageUniqueId;
    public List<long> WithdrawEchelonEntityId;
}

public class EventContentMapMoveRequest : RequestPacket, IRequest<EventContentMapMoveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_MapMove;
    public long EventContentId;
    public long StageUniqueId;
    public long EchelonEntityId;
    public HexLocation DestPosition;
}

public class EventContentEndTurnRequest : RequestPacket, IRequest<EventContentEndTurnResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EndTurn;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentRetreatRequest : RequestPacket, IRequest<EventContentRetreatResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_Retreat;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentPortalRequest : RequestPacket, IRequest<EventContentPortalResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_Portal;
    public long EventContentId;
    public long StageUniqueId;
    public long EchelonEntityId;
}

public class EventContentPurchasePlayCountHardStageRequest : RequestPacket, IRequest<EventContentPurchasePlayCountHardStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_PurchasePlayCountHardStage;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentShopListRequest : RequestPacket, IRequest<EventContentShopListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopList;
    public long EventContentId;
    public List<ShopCategoryType> CategoryList;
}

public class EventContentShopRefreshRequest : RequestPacket, IRequest<EventContentShopRefreshResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopRefresh;
    public long EventContentId;
    public ShopCategoryType ShopCategoryType;
}

public class EventContentReceiveStageTotalRewardRequest : RequestPacket, IRequest<EventContentReceiveStageTotalRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ReceiveStageTotalReward;
    public long EventContentId;
}

public class EventContentEnterMainGroundStageRequest : RequestPacket, IRequest<EventContentEnterMainGroundStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterMainGroundStage;
    public long EventContentId;
    public long StageUniqueId;
    public long LastEnterStageEchelonNumber;
}

public class EventContentMainGroundStageResultRequest : RequestPacket, IRequest<EventContentMainGroundStageResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_MainGroundStageResult;
    public long EventContentId;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
}

public class EventContentShopBuyMerchandiseRequest : RequestPacket, IRequest<EventContentShopBuyMerchandiseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopBuyMerchandise;
    public long EventContentId;
    public bool IsRefreshMerchandise;
    public long ShopUniqueId;
    public long GoodsUniqueId;
    public long PurchaseCount;
}

public class EventContentShopBuyRefreshMerchandiseRequest : RequestPacket, IRequest<EventContentShopBuyRefreshMerchandiseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ShopBuyRefreshMerchandise;
    public long EventContentId;
    public List<long> ShopUniqueIds;
}

public class EventContentSelectBuffRequest : RequestPacket, IRequest<EventContentSelectBuffResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_SelectBuff;
    public long SelectedBuffId;
}

public class EventContentBoxGachaShopListRequest : RequestPacket, IRequest<EventContentBoxGachaShopListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_BoxGachaShopList;
    public long EventContentId;
}

public class EventContentBoxGachaShopPurchaseRequest : RequestPacket, IRequest<EventContentBoxGachaShopPurchaseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_BoxGachaShopPurchase;
    public long EventContentId;
    public long PurchaseCount;
    public bool PurchaseAll;
}

public class EventContentBoxGachaShopRefreshRequest : RequestPacket, IRequest<EventContentBoxGachaShopRefreshResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_BoxGachaShopRefresh;
    public long EventContentId;
}

public class EventContentCollectionListRequest : RequestPacket, IRequest<EventContentCollectionListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CollectionList;
    public long EventContentId;
}

public class EventContentCollectionForMissionRequest : RequestPacket, IRequest<EventContentCollectionForMissionResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CollectionForMission;
    public long EventContentId;
}

public class EventContentScenarioGroupHistoryUpdateRequest : RequestPacket, IRequest<EventContentScenarioGroupHistoryUpdateResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_ScenarioGroupHistoryUpdate;
    public long ScenarioGroupUniqueId;
    public long ScenarioType;
    public long EventContentId;
}

public class EventContentCardShopListRequest : RequestPacket, IRequest<EventContentCardShopListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CardShopList;
    public long EventContentId;
}

public class EventContentCardShopShuffleRequest : RequestPacket, IRequest<EventContentCardShopShuffleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CardShopShuffle;
    public long EventContentId;
}

public class EventContentCardShopPurchaseRequest : RequestPacket, IRequest<EventContentCardShopPurchaseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_CardShopPurchase;
    public long EventContentId;
    public int SlotNumber;
}

public class EventContentRestartMainStageRequest : RequestPacket, IRequest<EventContentRestartMainStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_RestartMainStage;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentLocationGetInfoRequest : RequestPacket, IRequest<EventContentLocationGetInfoResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_LocationGetInfo;
    public long EventContentId;
}

public class EventContentLocationAttendScheduleRequest : RequestPacket, IRequest<EventContentLocationAttendScheduleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_LocationAttendSchedule;
    public long EventContentId;
    public long ZoneId;
    public long Count;
}

public class EventContentFortuneGachaPurchaseRequest : RequestPacket, IRequest<EventContentFortuneGachaPurchaseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_FortuneGachaPurchase;
    public long EventContentId;
}

public class EventContentSubEventLobbyRequest : RequestPacket, IRequest<EventContentSubEventLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_SubEventLobby;
    public long EventContentId;
}

public class EventContentEnterStoryStageRequest : RequestPacket, IRequest<EventContentEnterStoryStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_EnterStoryStage;
    public long StageUniqueId;
    public long EventContentId;
}

public class EventContentStoryStageResultRequest : RequestPacket, IRequest<EventContentStoryStageResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_StoryStageResult;
    public long EventContentId;
    public long StageUniqueId;
}

public class EventContentDiceRaceLobbyRequest : RequestPacket, IRequest<EventContentDiceRaceLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DiceRaceLobby;
    public long EventContentId;
}

public class EventContentDiceRaceRollRequest : RequestPacket, IRequest<EventContentDiceRaceRollResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DiceRaceRoll;
    public long EventContentId;
}

public class EventContentDiceRaceLapRewardRequest : RequestPacket, IRequest<EventContentDiceRaceLapRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_DiceRaceLapReward;
    public long EventContentId;
}

public class EventContentPermanentListRequest : RequestPacket, IRequest<EventContentPermanentListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.EventContent_PermanentList;
}

public class TTSGetFileRequest : RequestPacket, IRequest<TTSGetFileResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TTS_GetFile;
}

public class TTSGetKanaRequest : RequestPacket, IRequest<TTSGetKanaResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TTS_GetKana;
    public string CallName;
}

public class ContentLogUIOpenStatisticsRequest : RequestPacket, IRequest<ContentLogUIOpenStatisticsResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ContentLog_UIOpenStatistics;
    public Dictionary<string, int> OpenCountPerPrefab;
}

public class MomoTalkOutLineRequest : RequestPacket, IRequest<MomoTalkOutLineResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_OutLine;
}

public class MomoTalkMessageListRequest : RequestPacket, IRequest<MomoTalkMessageListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_MessageList;
    public long CharacterDBId;
}

public class MomoTalkReadRequest : RequestPacket, IRequest<MomoTalkReadResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_Read;
    public long CharacterDBId;
    public long LastReadMessageGroupId;
    public Int64? ChosenMessageId;
}

public class MomoTalkFavorScheduleRequest : RequestPacket, IRequest<MomoTalkFavorScheduleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MomoTalk_FavorSchedule;
    public long ScheduleId;
}

public class ClearDeckListRequest : RequestPacket, IRequest<ClearDeckListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ClearDeck_List;
    public long StageId;
}

public class MiniGameStageListRequest : RequestPacket, IRequest<MiniGameStageListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_StageList;
    public long EventContentId;
}

public class MiniGameEnterStageRequest : RequestPacket, IRequest<MiniGameEnterStageResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_EnterStage;
    public long EventContentId;
    public long UniqueId;
}

public class MiniGameResultRequest : RequestPacket, IRequest<MiniGameResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_Result;
    public long EventContentId;
    public long UniqueId;
    public MinigameRhythmSummary Summary;
}

public class MiniGameMissionListRequest : RequestPacket, IRequest<MiniGameMissionListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_MissionList;
    public long EventContentId;
}

public class MiniGameMissionRewardRequest : RequestPacket, IRequest<MiniGameMissionRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_MissionReward;
    public long MissionUniqueId;
    public long ProgressServerId;
    public long EventContentId;
}

public class MiniGameMissionMultipleRewardRequest : RequestPacket, IRequest<MiniGameMissionMultipleRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.MiniGame_MissionMultipleReward;
    public MissionCategory MissionCategory;
    public long EventContentId;
}

public class NotificationLobbyCheckRequest : RequestPacket, IRequest<NotificationLobbyCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Notification_LobbyCheck;
}

public class NotificationEventContentReddotRequest : RequestPacket, IRequest<NotificationEventContentReddotResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Notification_EventContentReddotCheck;
}

public class ProofTokenRequestQuestionRequest : RequestPacket, IRequest<ProofTokenRequestQuestionResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ProofToken_RequestQuestion;
}

public class ProofTokenSubmitRequest : RequestPacket, IRequest<ProofTokenSubmitResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ProofToken_Submit;
    public long Answer;
}

public class SchoolDungeonListRequest : RequestPacket, IRequest<SchoolDungeonListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_List;
}

public class SchoolDungeonEnterBattleRequest : RequestPacket, IRequest<SchoolDungeonEnterBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_EnterBattle;
    public long StageUniqueId;
}

public class SchoolDungeonBattleResultRequest : RequestPacket, IRequest<SchoolDungeonBattleResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_BattleResult;
    public long StageUniqueId;
    public bool PassCheckCharacter;
    public BattleSummary Summary;
}

public class SchoolDungeonRetreatRequest : RequestPacket, IRequest<SchoolDungeonRetreatResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.SchoolDungeon_Retreat;
    public long StageUniqueId;
}

public class TimeAttackDungeonLobbyRequest : RequestPacket, IRequest<TimeAttackDungeonLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_Lobby;
}

public class TimeAttackDungeonCreateBattleRequest : RequestPacket, IRequest<TimeAttackDungeonCreateBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_CreateBattle;
    public bool IsPractice;
}

public class TimeAttackDungeonEnterBattleRequest : RequestPacket, IRequest<TimeAttackDungeonEnterBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_EnterBattle;
    public long RoomId;
    public ClanAssistUseInfo AssistUseInfo;
}

public class TimeAttackDungeonEndBattleRequest : RequestPacket, IRequest<TimeAttackDungeonEndBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_EndBattle;
    public int EchelonId;
    public long RoomId;
    public BattleSummary Summary;
    public ClanAssistUseInfo AssistUseInfo;
}

public class TimeAttackDungeonSweepRequest : RequestPacket, IRequest<TimeAttackDungeonSweepResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_Sweep;
    public long SweepCount;
}

public class TimeAttackDungeonGiveUpRequest : RequestPacket, IRequest<TimeAttackDungeonGiveUpResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_GiveUp;
    public long RoomId;
}

public class TimeAttackDungeonLoginRequest : RequestPacket, IRequest<TimeAttackDungeonLoginResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.TimeAttackDungeon_Login;
}

public class WorldRaidLobbyRequest : RequestPacket, IRequest<WorldRaidLobbyResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_Lobby;
    public long SeasonId;
}

public class WorldRaidBossListRequest : RequestPacket, IRequest<WorldRaidBossListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_BossList;
    public long SeasonId;
    public bool RequestOnlyWorldBossData;
}

public class WorldRaidEnterBattleRequest : RequestPacket, IRequest<WorldRaidEnterBattleResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_EnterBattle;
    public long SeasonId;
    public long GroupId;
    public long UniqueId;
    public long EchelonId;
    public bool IsPractice;
    public bool IsTicket;
    public List<ClanAssistUseInfo> AssistUseInfos;
}

public class WorldRaidBattleResultRequest : RequestPacket, IRequest<WorldRaidBattleResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_BattleResult;
    public long SeasonId;
    public long GroupId;
    public long UniqueId;
    public long EchelonId;
    public bool IsPractice;
    public bool IsTicket;
    public BattleSummary Summary;
    public List<ClanAssistUseInfo> AssistUseInfos;
}

public class WorldRaidReceiveRewardRequest : RequestPacket, IRequest<WorldRaidReceiveRewardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.WorldRaid_ReceiveReward;
    public long SeasonId;
}

public class ResetableContentGetRequest : RequestPacket, IRequest<ResetableContentGetResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.ResetableContent_Get;
}

public class ConquestGetInfoRequest : RequestPacket, IRequest<ConquestGetInfoResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_GetInfo;
    public long EventContentId;
}

public class ConquestConquerRequest : RequestPacket, IRequest<ConquestConquerResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_Conquer;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
}

public class ConquestConquerWithBattleStartRequest : RequestPacket, IRequest<ConquestConquerWithBattleStartResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_ConquerWithBattleStart;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public Int64? EchelonNumber;
    public ClanAssistUseInfo ClanAssistUseInfo;
}

public class ConquestConquerWithBattleResultRequest : RequestPacket, IRequest<ConquestConquerWithBattleResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_ConquerWithBattleResult;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public BattleSummary BattleSummary;
}

public class ConquestManageBaseRequest : RequestPacket, IRequest<ConquestManageBaseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_ManageBase;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
    public int ManageCount;
}

public class ConquestUpgradeBaseRequest : RequestPacket, IRequest<ConquestUpgradeBaseResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_UpgradeBase;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
}

public class ConquestTakeEventObjectRequest : RequestPacket, IRequest<ConquestTakeEventObjectResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_TakeEventObject;
    public long EventContentId;
    public long ConquestObjectDBId;
}

public class ConquestEventObjectBattleStartRequest : RequestPacket, IRequest<ConquestEventObjectBattleStartResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_EventObjectBattleStart;
    public long EventContentId;
    public long ConquestObjectDBId;
    public long EchelonNumber;
    public ClanAssistUseInfo ClanAssistUseInfo;
}

public class ConquestEventObjectBattleResultRequest : RequestPacket, IRequest<ConquestEventObjectBattleResultResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_EventObjectBattleResult;
    public long EventContentId;
    public long ConquestObjectDBId;
    public BattleSummary BattleSummary;
}

public class ConquestNormalizeEchelonRequest : RequestPacket, IRequest<ConquestNormalizeEchelonResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_NormalizeEchelon;
    public long EventContentId;
    public StageDifficulty Difficulty;
    public long TileUniqueId;
}

public class ConquestCheckRequest : RequestPacket, IRequest<ConquestCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Conquest_Check;
    public long EventContentId;
}

public class FriendListRequest : RequestPacket, IRequest<FriendListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_List;
}

public class FriendRemoveRequest : RequestPacket, IRequest<FriendRemoveResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_Remove;
    public long TargetAccountId;
}

public class FriendGetFriendDetailedInfoRequest : RequestPacket, IRequest<FriendGetFriendDetailedInfoResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_GetFriendDetailedInfo;
    public long FriendAccountId;
}

public class FriendGetIdCardRequest : RequestPacket, IRequest<FriendGetIdCardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_GetIdCard;
}

public class FriendSetIdCardRequest : RequestPacket, IRequest<FriendSetIdCardResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_SetIdCard;
    public string Comment;
    public long RepresentCharacterUniqueId;
    public bool SearchPermission;
    public bool AutoAcceptFriendRequest;
    public bool ShowAccountLevel;
    public bool ShowFriendCode;
    public bool ShowRaidRanking;
    public bool ShowArenaRanking;
    public long BackgroundId;
}

public class FriendSearchRequest : RequestPacket, IRequest<FriendSearchResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_Search;
    public string FriendCode;
    public FriendSearchLevelOption LevelOption;
}

public class FriendSendFriendRequestRequest : RequestPacket, IRequest<FriendSendFriendRequestResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_SendFriendRequest;
    public long TargetAccountId;
}

public class FriendAcceptFriendRequestRequest : RequestPacket, IRequest<FriendAcceptFriendRequestResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_AcceptFriendRequest;
    public long TargetAccountId;
}

public class FriendDeclineFriendRequestRequest : RequestPacket, IRequest<FriendDeclineFriendRequestResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_DeclineFriendRequest;
    public long TargetAccountId;
}

public class FriendCancelFriendRequestRequest : RequestPacket, IRequest<FriendCancelFriendRequestResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_CancelFriendRequest;
    public long TargetAccountId;
}

public class FriendCheckRequest : RequestPacket, IRequest<FriendCheckResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Friend_Check;
}

public class CharacterGearListRequest : RequestPacket, IRequest<CharacterGearListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CharacterGear_List;
}

public class CharacterGearUnlockRequest : RequestPacket, IRequest<CharacterGearUnlockResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CharacterGear_Unlock;
    public long CharacterServerId;
    public int SlotIndex;
}

public class CharacterGearTierUpRequest : RequestPacket, IRequest<CharacterGearTierUpResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.CharacterGear_TierUp;
    public long GearServerId;
}

public class QueuingGetTicketRequest : RequestPacket, IRequest<QueuingGetTicketResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Queuing_GetTicketGL;
    public long NpSN;
    public string NpToken;
    public string Npacode;
    public string OSType;
    public string AccessIP;
    public bool MakeStandby;
    public bool PassCheck;
    public bool PassCheckNexon;
    public string WaitingTicket;
    public string ClientVersion;
}

public class ManagementBannerListRequest : RequestPacket, IRequest<ManagementBannerListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Management_BannerList;
}

public class ManagementContentsLockListRequest : RequestPacket, IRequest<ManagementContentsLockListResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Management_ContentsLockList;
}

public class CommonCheatRequest : RequestPacket, IRequest<CommonCheatResponse>
{
    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.Common_Cheat;
    public string Cheat;
    public List<CheatCharacterCustomPreset> CharacterCustomPreset;
}

