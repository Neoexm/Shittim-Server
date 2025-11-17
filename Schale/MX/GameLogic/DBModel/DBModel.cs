using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.Conquest.ConquestTileMapEvent;
using Schale.MX.Core.Math;
using Schale.MX.Core.Services;
using Schale.MX.Data;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.BattlesEntities;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.Data;
using Schale.MX.NetworkProtocol;
using Schale.MX.TableBoard;

namespace Schale.MX.GameLogic.DBModel
{
    public struct CharacterWeapon
    {
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
    }

    public struct Equipment
    {
        public long UniqueId { get; set; }
        public int Tier { get; set; }
        public int Level { get; set; }
    }

    public struct CharacterGear
    {
        public long UniqueId { get; set; }
        public int Tier { get; set; }
        public int Level { get; set; }
    }

    public class AcademyDB
    {
        public DateTime LastUpdate { get; set; }
        public Dictionary<long, List<VisitingCharacterDB>>? ZoneVisitCharacterDBs { get; set; }
        public Dictionary<long, List<long>>? ZoneScheduleGroupRecords { get; set; }
    }

    public class AcademyLocationDB
    {
        public long LocationId { get; set; }
        public long Rank { get; set; }
        public long Exp { get; set; }
    }

    public class AcademyMessageOutLineDB
    {
        public long CharacterUniqueId { get; set; }
        public long NewMessageCount { get; set; }
        public long LastMessageUniqueId { get; set; }
        public long LastMessageServerId { get; set; }
    }

    public class AcademyMessageDB
    {
        public long MessageServerId { get; set; }
        public long MessageGroupId { get; set; }
        public long MessageUniqueId { get; set; }
        public long SelectedMessageUniqueId { get; set; }
        public long CharacterServerId { get; set; }
        public long CharacterUniqueId { get; set; }
        public bool IsRead { get; set; }
    }

    public class AcademyScheduleDB
    {
        public long ScheduleUniqueId { get; set; }
        public long ScheduleGroupId { get; set; }
        public long ZoneUniqueId { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public int CompleteCount { get; set; }
    }

    public class AccountAchievementDB
    {
        public long AchievementUniqueId { get; set; }
        public long AchievementValue { get; set; }
    }

    public class AccountAttachmentDB
    {
        public long AccountId { get; set; }
        public long EmblemUniqueId { get; set; }
    }

    public class AccountBanByNexonDB
    {
        public long AccountId { get; set; }
        public long Npsn { get; set; }
        public long AccountBanId { get; set; }
        public int BanType { get; set; }
        public int BanDay { get; set; }
        public DateTime BanStartDate { get; set; }
        public DateTime BanEndDate { get; set; }
        public string? BanComment { get; set; }
        public int DeleteFlag { get; set; }
    }

    public class AccountCheckAdultAgree
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public bool CheckAdultAgree { get; set; }
    }

    public class AccountCurrencyDB
    {
        public long AccountLevel { get; set; }
        public long AcademyLocationRankSum { get; set; }
        public Dictionary<CurrencyTypes, long>? CurrencyDict { get; set; }
        public Dictionary<CurrencyTypes, DateTime>? UpdateTimeDict { get; set; }
    }

    public class AccountDB
    {
        public long ServerId { get; set; }
        public string? Nickname { get; set; }
        public string? CallName { get; set; }
        public string? CallNameKatakana { get; set; }
        public string? CallNameKorean { get; set; }
        public string? DevId { get; set; }
        public AccountState State { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public string? Comment { get; set; }
        public int LobbyMode { get; set; }
        public long RepresentCharacterServerId { get; set; }
        public long MemoryLobbyUniqueId { get; set; }
        public DateTime LastConnectTime { get; set; }
        public DateTime BirthDay { get; set; }
        public DateTime CallNameUpdateTime { get; set; }
        public long PublisherAccountId { get; set; }
        public Nullable<int> RetentionDays { get; set; }
        public Nullable<int> VIPLevel { get; set; }
        public DateTime CreateDate { get; set; }
        public Nullable<int> UnReadMailCount { get; set; }
        public Nullable<DateTime> LinkRewardDate { get; set; }
    }

    public class AccountLevelRewardDB
    {
        public long RewardId { get; set; }
    }

    public class AccountRestrictionsDB
    {
        public bool NicknameRestriction { get; set; }
        public bool CommentRestriction { get; set; }
        public bool CallnameRestriction { get; set; }
    }

    public class ArenaHistoryDB
    {
        public ArenaBattleDB? ArenaBattleDB { get; set; }
        public DateTime BattleEndTime { get; set; }
        public BattleSummary? BattleSummary { get; set; }
        public ArenaUserDB? AttackingUserDB { get; set; }
        public ArenaUserDB? DefendingUserDB { get; set; }
        public long WinnerAccountServerId { get; set; }
    }

    public class ArenaPlayerInfoDB
    {
        public long CurrentSeasonId { get; set; }
        public long PlayerGroupId { get; set; }
        public long CurrentRank { get; set; }
        public long SeasonRecord { get; set; }
        public long AllTimeRecord { get; set; }
        public long CumulativeTimeReward { get; set; }
        public DateTime TimeRewardLastUpdateTime { get; set; }
        public DateTime BattleEnterActiveTime { get; set; }
        public DateTime DailyRewardActiveTime { get; set; }
    }

    public class ArenaBattleDB
    {
        public long ArenaBattleServerId { get; set; }
        public long Season { get; set; }
        public long Group { get; set; }
        public DateTime BattleStartTime { get; set; }
        public DateTime BattleEndTime { get; set; }
        public long Seed { get; set; }
        public ArenaUserDB? AttackingUserDB { get; set; }
        public ArenaUserDB? DefendingUserDB { get; set; }
        public BattleSummary? BattleSummary { get; set; }
    }

    public class ArenaUserDB
    {
        public long AccountServerId { get; set; }
        public long RepresentCharacterUniqueId { get; set; }
        public long RepresentCharacterCostumeId { get; set; }
        public string? NickName { get; set; }
        public long Rank { get; set; }
        public long Level { get; set; }
        public long Exp { get; set; }
        public ArenaTeamSettingDB? TeamSettingDB { get; set; }
        public AccountAttachmentDB? AccountAttachmentDB { get; set; }
        public string? UserName { get; set; }
    }

    public class ArenaTeamSettingDB
    {
        public EchelonType EchelonType { get; set; }
        public long LeaderCharacterId { get; set; }
        public long TSSInteractionCharacterId { get; set; }
        public long TSSInteractionCharacterServerId { get; set; }
        public IList<ArenaCharacterDB>? MainCharacters { get; set; }
        public IList<ArenaCharacterDB>? SupportCharacters { get; set; }
        public ArenaCharacterDB? TSSCharacterDB { get; set; }
        public int SquadCount { get; set; }
        public long MapId { get; set; }
    }

    public class ArenaCharacterDB
    {
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public int PublicSkillLevel { get; set; }
        public int ExSkillLevel { get; set; }
        public int PassiveSkillLevel { get; set; }
        public int ExtraPassiveSkillLevel { get; set; }
        public int LeaderSkillLevel { get; set; }
        public List<EquipmentDB>? EquipmentDBs { get; set; }
        public Dictionary<long, long>? FavorRankInfo { get; set; }
        public Dictionary<int, int>? PotentialStats { get; set; }
        public int CombatStyleIndex { get; set; }
        public WeaponDB? WeaponDB { get; set; }
        public GearDB? GearDB { get; set; }
        public CostumeDB? CostumeDB { get; set; }
    }

    public class ArenaDamageReportDB
    {
        public long ArenaBattleServerId { get; set; }
        public long WinnerAccountServerId { get; set; }
        public ArenaUserDB? AttackerUserDB { get; set; }
        public ArenaUserDB? DefenderUserDB { get; set; }
        public DateTime BattleEndTime { get; set; }
        public Dictionary<long, long>? AttackerDamageReport { get; set; }
        public Dictionary<long, long>? DefenderDamageReport { get; set; }
    }

    public enum AssistRelation : int
    {
        None = 0,
        Clan = 1,
        Friend = 2,
        Cheat = 4,
        Stranger = 8,
    }

    public class AssistCharacterDB : CharacterDB
    {
        public EchelonType EchelonType { get; set; }
        public int SlotNumber { get; set; }
        public long AccountId { get; set; }
        public AssistRelation AssistRelation { get; set; }
        public long AssistCharacterServerId { get; set; }
        public string? NickName { get; set; }
        public List<EquipmentDB>? EquipmentDBs { get; set; }
        public WeaponDB? WeaponDB { get; set; }
        public GearDB? GearDB { get; set; }
        public long CostumeId { get; set; }
        public CostumeDB? CostumeDB { get; set; }
        public bool IsMulligan { get; set; }
        public bool IsTSAInteraction { get; set; }
        public int CombatStyleIndex { get; set; }
        public bool HasWeapon { get; set; }
        public bool HasGear { get; set; }
    }

    public class AttendanceHistoryDB
    {
        public long ServerId { get; set; }
        public long AttendanceBookUniqueId { get; set; }
        public Dictionary<long, DateTime>? AttendedDay { get; set; }
        public bool Expired { get; set; }
        public long LastAttendedDay { get; set; }
        public DateTime LastAttendedDate { get; set; }
        public Dictionary<long, Nullable<DateTime>>? AttendedDayNullable { get; set; }
    }

    public class BanDB
    {
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public DateTime BanStartDate { get; set; }
        public DateTime BanEndDate { get; set; }
        public DateTime RegisterDate { get; set; }
        public byte CancelFlag { get; set; }
        public DateTime CancelDate { get; set; }
        public string? Reason { get; set; }
    }

    public class BannerDB
    {
        public long BannerOrder { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public string? WebViewTitle { get; set; }
        public string? WebViewUrl { get; set; }
        public int LinkedLobbyBannerId { get; set; }
        public EventContentType BannerType { get; set; }
        public BannerDisplayType BannerDisplayType { get; set; }
    }

    public class BattlePassInfoDB
    {
        public long BattlePassId { get; set; }
        public int PassLevel { get; set; }
        public long PassExp { get; set; }
        public long PurchaseGroupId { get; set; }
        public int ReceiveRewardLevel { get; set; }
        public int ReceivePurchaseRewardLevel { get; set; }
        public long WeeklyPassExp { get; set; }
        public DateTime LastWeeklyPassExpLimitRefreshDate { get; set; }
    }

    public class BattlePassProductPurchaseDB
    {
        public long ProductId { get; set; }
        public long BattlePassId { get; set; }
        public long PurchaseBattlePassGroupId { get; set; }
    }

    public class BeforehandGachaSnapshotDB
    {
        public long ShopUniqueId { get; set; }
        public long GoodsId { get; set; }
        public long LastIndex { get; set; }
        public List<long>? LastResults { get; set; }
        public Nullable<long> SavedIndex { get; set; }
        public List<long>? SavedResults { get; set; }
        public Nullable<long> PickedIndex { get; set; }
    }

    public enum ShopCashBlockType : long
    {
        All = -1,
        AppStore = -2,
        GooglePlay = -3,
        None = -9999,
    }

    public class BlockedProductDB
    {
        public long CashProductId { get; set; }
        public ShopCashBlockType MarketBlockType { get; set; }
        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class CafeDB
    {
        public long CafeDBId { get; set; }
        public long CafeId { get; set; }
        public long AccountId { get; set; }
        public int CafeRank { get; set; }
        public DateTime LastUpdate { get; set; }
        public Nullable<DateTime> LastSummonDate { get; set; }
        public bool IsNew { get; set; }
        public Dictionary<long, CafeCharacterDB>? CafeVisitCharacterDBs { get; set; }
        public List<FurnitureDB>? FurnitureDBs { get; set; }
        public DateTime ProductionAppliedTime { get; set; }
        public CafeProductionDB? ProductionDB { get; set; }
        public Dictionary<CurrencyTypes, long>? CurrencyDict_Obsolete { get; set; }
        public Dictionary<CurrencyTypes, DateTime>? UpdateTimeDict_Obsolete { get; set; }
        public class CafeCharacterDB : VisitingCharacterDB
        {
            public bool IsSummon { get; set; }
            public DateTime LastInteractTime { get; set; }
        }

    }

    public class CafeProductionDB
    {
        public long CafeDBId { get; set; }
        public long ComfortValue { get; set; }
        public DateTime AppliedDate { get; set; }
        public List<CafeProductionParcelInfo>? ProductionParcelInfos { get; set; }
        public class CafeProductionParcelInfo
        {
            public ParcelKeyPair? Key { get; set; }
            public long Amount { get; set; }
        }

    }

    public class CafePresetDB
    {
        public long ServerId { get; set; }
        public int SlotId { get; set; }
        public string? PresetName { get; set; }
        public bool IsEmpty { get; set; }
    }

    public class CampaignChapterClearRewardHistoryDB
    {
        public long ChapterUniqueId { get; set; }
        public StageDifficulty RewardType { get; set; }
        public DateTime ReceiveDate { get; set; }
    }

    public class EventContentMainStageSaveDB : CampaignMainStageSaveDB
    {
        public new static ContentType ContentType { get => ContentType.EventContentMainStage; }

        public Dictionary<long, long>? SelectedBuffDict { get; set; }
        public bool IsBuffSelectPopupOpen { get; set; }
        public long CurrentAppearedBuffGroupId { get; set; }
    }

    public class CampaignMainStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.CampaignMainStage; }

        public CampaignState CampaignState { get; set; }
        public int CurrentTurn { get; set; }
        public int EnemyClearCount { get; set; }
        public int LastEnemyEntityId { get; set; }
        public int TacticRankSCount { get; set; }
        public Dictionary<long, HexaUnit>? EnemyInfos { get; set; }
        public Dictionary<long, HexaUnit>? EchelonInfos { get; set; }
        public Dictionary<long, List<long>>? WithdrawInfos { get; set; }
        public Dictionary<long, Strategy>? StrategyObjects { get; set; }
        public Dictionary<long, List<ParcelInfo>>? StrategyObjectRewards { get; set; }
        public List<long>? StrategyObjectHistory { get; set; }
        public Dictionary<long, List<long>>? ActivatedHexaEventsAndConditions { get; set; }
        public Dictionary<long, List<long>>? HexaEventDelayedExecutions { get; set; }
        public Dictionary<int, HexaTileState>? TileMapStates { get; set; }
        public List<HexaDisplayInfo>? DisplayInfos { get; set; }
        public List<HexaUnit>? DeployedEchelonInfos { get; set; }
    }

    public class CampaignStageHistoryDB
    {
        public long StoryUniqueId { get; set; }
        public long ChapterUniqueId { get; set; }
        public long StageUniqueId { get; set; }
        public long TacticClearCountWithRankSRecord { get; set; }
        public long ClearTurnRecord { get; set; }
        public long BestStarRecord { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public DateTime LastPlay { get; set; }
        public long TodayPlayCount { get; set; }
        public long TodayPurchasePlayCountHardStage { get; set; }
        public Nullable<DateTime> FirstClearRewardReceive { get; set; }
        public Nullable<DateTime> StarRewardReceive { get; set; }
        public bool IsClearedEver { get; set; }
        public long TodayPlayCountForUI { get; set; }
    }

    public class CampaignSubStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.CampaignSubStage; }

    }

    public class CampaignExtraStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.CampaignExtraStage; }

    }

    public class EventContentSubStageSaveDB : CampaignSubStageSaveDB
    {
        public new static ContentType ContentType { get => ContentType.EventContentSubStage; }

    }

    public class EventContentMainGroundStageSaveDB : CampaignSubStageSaveDB
    {
        public new static ContentType ContentType { get => ContentType.EventContentMainGroundStage; }

    }

    public class EventContentStoryStageSaveDB : CampaignSubStageSaveDB
    {
        public new static ContentType ContentType { get => ContentType.EventContentStoryStage; }

    }

    public class CampaignTutorialStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.CampaignTutorialStage; }

    }

    public class CardShopElementDB
    {
        public long EventContentId { get; set; }
        public int SlotNumber { get; set; }
        public long CardShopElementId { get; set; }
        public bool SoldOut { get; set; }
    }

    public class CardShopPurchaseHistoryDB
    {
        public long EventContentId { get; set; }
        public Rarity Rarity { get; set; }
        public long PurchaseCount { get; set; }
    }

    public class CharacterDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.Character; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int FavorRank { get; set; }
        public long FavorExp { get; set; }
        public int PublicSkillLevel { get; set; }
        public int ExSkillLevel { get; set; }
        public int PassiveSkillLevel { get; set; }
        public int ExtraPassiveSkillLevel { get; set; }
        public int LeaderSkillLevel { get; set; }
        public bool IsFavorite { get; set; }
        public List<long>? EquipmentServerIds { get; set; }
        public Dictionary<int, int>? PotentialStats { get; set; }
        public Dictionary<int, long>? EquipmentSlotAndDBIds { get; set; }
    }

    public class ClanAssistRentHistoryDB
    {
        public long AssistCharacterAccountId { get; set; }
        public long AssistCharacterDBId { get; set; }
        public DateTime RentDate { get; set; }
        public long AssistCharacterId { get; set; }
    }

    public class ClanAssistSlotDB
    {
        public EchelonType EchelonType { get; set; }
        public long SlotNumber { get; set; }
        public long CharacterDBId { get; set; }
        public DateTime DeployDate { get; set; }
        public long TotalRentCount { get; set; }
        public int CombatStyleIndex { get; set; }
    }

    public class ClanAssistRewardInfo
    {
        public long CharacterDBId { get; set; }
        public DateTime DeployDate { get; set; }
        public long RentCount { get; set; }
        public List<ParcelInfo>? CumultativeRewardParcels { get; set; }
        public List<ParcelInfo>? RentRewardParcels { get; set; }
    }

    public class ClanAssistUseInfo
    {
        public long CharacterAccountId { get; set; }
        public long CharacterDBId { get; set; }
        public EchelonType EchelonType { get; set; }
        public int SlotNumber { get; set; }
        public AssistRelation AssistRelation { get; set; }
        public int EchelonSlotType { get; set; }
        public int EchelonSlotIndex { get; set; }
        public int CombatStyleIndex { get; set; }
        public long DecodedShardId { get; set; }
        public long DecodedCharacterDBId { get; set; }
        public bool IsMulligan { get; set; }
        public bool IsTSAInteraction { get; set; }
    }

    public class ClanDB
    {
        public long ClanDBId { get; set; }
        public string? ClanName { get; set; }
        public string? ClanChannelName { get; set; }
        public string? ClanPresidentNickName { get; set; }
        public long ClanPresidentRepresentCharacterUniqueId { get; set; }
        public long ClanPresidentRepresentCharacterCostumeId { get; set; }
        public string? ClanNotice { get; set; }
        public long ClanMemberCount { get; set; }
        public ClanJoinOption ClanJoinOption { get; set; }
    }

    public class ClanMemberDB
    {
        public long AccountId { get; set; }
        public long AccountLevel { get; set; }
        public string? AccountNickName { get; set; }
        public long ClanDBId { get; set; }
        public long RepresentCharacterUniqueId { get; set; }
        public long RepresentCharacterCostumeId { get; set; }
        public long AttendanceCount { get; set; }
        public long CafeComfortValue { get; set; }
        public ClanSocialGrade ClanSocialGrade { get; set; }
        public DateTime JoinDate { get; set; }
        public DateTime SocialGradeUpdateTime { get; set; }
        public DateTime LastLoginDate { get; set; }
        public DateTime GameLoginDate { get; set; }
        public DateTime AppliedDate { get; set; }
        public AccountAttachmentDB? AttachmentDB { get; set; }
    }

    public class ClanMemberDescriptionDB
    {
        public long Exp { get; set; }
        public string? Comment { get; set; }
        public int CollectedCharactersCount { get; set; }
        public long ArenaSeasonBestRanking { get; set; }
        public long ArenaSeasonCurrentRanking { get; set; }
    }

    public struct ClearDeckKey
    {
        public ContentType ContentType { get; set; }
        public long[] Arguments { get; set; }
    }

    public class ClearDeckDB
    {
        public List<ClearDeckCharacterDB>? ClearDeckCharacterDBs { get; set; }
        public List<long>? MulliganUniqueIds { get; set; }
        public long LeaderUniqueId { get; set; }
        public long TSSInteractionUniqueId { get; set; }
        public EchelonType EchelonType { get; set; }
        public long EchelonExtensionType { get; set; }
    }

    public class ClearDeckCharacterDB
    {
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public int SlotNumber { get; set; }
        public bool HasWeapon { get; set; }
        public SquadType SquadType { get; set; }
        public int WeaponStarGrade { get; set; }
        public int CombatStyleIndex { get; set; }
    }

    public class ClearDeckCharacter
    {
        public long CharacterId { get; set; }
        public int Level { get; set; }
        public int StarGrade { get; set; }
    }

    public class ClearDeckEchelonDB
    {
        public long CharacterIdLeader { get; set; }
        public List<ClearDeckCharacter>? MainCharacters { get; set; }
        public List<ClearDeckCharacter>? SupporterCharacters { get; set; }
        public ClearDeckCharacter? TSSCharacter { get; set; }
        public int MainCharacterCount { get; set; }
        public int SupporterCharacterCount { get; set; }
        public int TotalCharacterCount { get; set; }
        public int MainCharacterLevelSum { get; set; }
        public int SupporterCharacterLevelSum { get; set; }
        public int TotalCharacterLevelSum { get; set; }
    }

    public class ConquestEchelonDB
    {
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public long TileUniqueId { get; set; }
        public EchelonDB? EchelonDB { get; set; }
        public long AssistCharacterUniqueId { get; set; }
        public ClanAssistUseInfo? AssistUseInfo { get; set; }
    }

    public class ConquestErosionDB : ConquestEventObjectDB
    {
        public new ConquestEventObjectType ObjectType { get; set; }
        public long ErosionId { get; set; }
        public long ConditionSnapshot { get; set; }
        public DateTime CreateDate { get; set; }
    }

    public abstract class ConquestEventObjectDB
    {
        public long ConquestObjectDBId { get; set; }
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public long TileUniqueId { get; set; }
        public long ObjectId { get; set; }
        public ConquestEventObjectType ObjectType { get; set; }
        public bool IsAlive { get; set; }
    }

    public class ConquestInfoDB
    {
        public long EventContentId { get; set; }
        public int EventGauge { get; set; }
        public int EventSpawnCount { get; set; }
        public int EchelonChangeCount { get; set; }
        public int TodayConquestRentCount { get; set; }
        public int TodayOperationRentCount { get; set; }
        public long CumulatedConditionValue { get; set; }
        public long ReceivedCalculateRewardConditionAmount { get; set; }
        public long CalculateRewardConditionValue { get; set; }
        public Nullable<long> AlertMassErosionId { get; set; }
    }

    public class ConquestStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.Conquest; }

        public Nullable<long> ConquestEventObjectDBId { get; set; }
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public long TileUniqueId { get; set; }
        public long TilePresetId { get; set; }
        public ConquestTileType ConquestTileType { get; set; }
        public bool UseManageEchelon { get; set; }
        public AssistCharacterDB? AssistCharacterDB { get; set; }
        public int EchelonSlotType { get; set; }
        public int EchelonSlotIndex { get; set; }
    }

    public class ConquestSummary
    {
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public Dictionary<int, ConquestStepSummary>? ConquestStepSummaryDict { get; set; }
    }

    public class ConquestStepSummary
    {
        public long ConqueredTileCount { get; set; }
        public long AllTileCount { get; set; }
        public long ErosionRemainingCount { get; set; }
        public bool HasPhaseComplete { get; set; }
        public bool IsErosionPhaseStart { get; set; }
        public bool IsStepOpen { get; set; }
    }

    public class ConquestMainStorySummary
    {
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public Dictionary<int, ConquestMainStoryStepSummary>? ConquestStepSummaryDict { get; set; }
    }

    public class ConquestMainStoryStepSummary
    {
        public long ConqueredTileCount { get; set; }
        public long AllTileCount { get; set; }
        public bool IsStepOpen { get; set; }
    }

    public class ConquestTileDB
    {
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public long TileUniqueId { get; set; }
        public TileState TileState { get; set; }
        public long Level { get; set; }
        public bool[]? StarFlags { get; set; }
        public DateTime CreateTime { get; set; }
        public bool IsThreeStarClear { get; set; }
        public bool IsAnyStarClear { get; set; }
        public long BestStarRecord { get; set; }
    }

    public class ConquestTreasureBoxDB : ConquestEventObjectDB
    {
        public new ConquestEventObjectType ObjectType { get; set; }
    }

    public class ConquestUnexpectedEnemyDB : ConquestEventObjectDB
    {
        public long UnitId { get; set; }
        public new ConquestEventObjectType ObjectType { get; set; }
    }

    public abstract class ConsumableItemBaseDB : ParcelBase
    {
        public ParcelKeyPair? Key { get; set; }
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public long StackCount { get; set; }
        public bool CanConsume { get; set; }
    }

    public interface IConsumableItemBaseExcel
    {
        public ParcelType Type { get; set; }
        public long UniqueId { get; set; }
        public long ShiftingCraftQuality { get; set; }
        public long StackableMax { get; set; }
        public Rarity Rarity { get; set; }
        public IReadOnlyList<Tag>? Tags { get; set; }
        public IReadOnlyDictionary<CraftNodeTier, long>? CraftQualityDict { get; set; }
    }

    public class ItemExcelData : IConsumableItemBaseExcel
    {
        public ParcelType Type { get; set; }
        public long UniqueId { get; set; }
        public long ShiftingCraftQuality { get; set; }
        public long StackableMax { get; set; }
        public Rarity Rarity { get; set; }
        public IReadOnlyList<Tag>? Tags { get; set; }
        public IReadOnlyDictionary<CraftNodeTier, long>? CraftQualityDict { get; set; }
    }

    public class FurnitureExcelData : IConsumableItemBaseExcel
    {
        public ParcelType Type { get; set; }
        public long UniqueId { get; set; }
        public long ShiftingCraftQuality { get; set; }
        public long StackableMax { get; set; }
        public Rarity Rarity { get; set; }
        public IReadOnlyList<Tag>? Tags { get; set; }
        public IReadOnlyDictionary<CraftNodeTier, long>? CraftQualityDict { get; set; }
    }

    public class EquipmentExcelData : IConsumableItemBaseExcel
    {
        public ParcelType Type { get; set; }
        public long UniqueId { get; set; }
        public long ShiftingCraftQuality { get; set; }
        public long StackableMax { get; set; }
        public Rarity Rarity { get; set; }
        public IReadOnlyList<Tag>? Tags { get; set; }
        public IReadOnlyDictionary<CraftNodeTier, long>? CraftQualityDict { get; set; }
    }

    public class ConsumeRequestDB
    {
        public Dictionary<long, long>? ConsumeItemServerIdAndCounts { get; set; }
        public Dictionary<long, long>? ConsumeEquipmentServerIdAndCounts { get; set; }
        public Dictionary<long, long>? ConsumeFurnitureServerIdAndCounts { get; set; }
        public bool IsItemsValid { get; set; }
        public bool IsEquipmentsValid { get; set; }
        public bool IsFurnituresValid { get; set; }
        public bool IsValid { get; set; }
    }

    public class ConsumeResultDB
    {
        public List<long>? RemovedItemServerIds { get; set; }
        public List<long>? RemovedEquipmentServerIds { get; set; }
        public List<long>? RemovedFurnitureServerIds { get; set; }
        public Dictionary<long, long>? UsedItemServerIdAndRemainingCounts { get; set; }
        public Dictionary<long, long>? UsedEquipmentServerIdAndRemainingCounts { get; set; }
        public Dictionary<long, long>? UsedFurnitureServerIdAndRemainingCounts { get; set; }
    }

    public class EquipmentBatchGrowthRequestDB
    {
        public long TargetServerId { get; set; }
        public List<ConsumeRequestDB>? ConsumeRequestDBs { get; set; }
        public long AfterTier { get; set; }
        public long AfterLevel { get; set; }
        public long AfterExp { get; set; }
        public List<SelectTicketReplaceInfo>? ReplaceInfos { get; set; }
    }

    public class SkillLevelBatchGrowthRequestDB
    {
        public SkillSlot SkillSlot { get; set; }
        public int Level { get; set; }
        public List<SelectTicketReplaceInfo>? ReplaceInfos { get; set; }
    }

    public class SelectTicketReplaceInfo
    {
        public ParcelType MaterialType { get; set; }
        public long MaterialId { get; set; }
        public long TicketItemId { get; set; }
        public int Amount { get; set; }
    }

    public class GearTierUpRequestDB
    {
        public long TargetServerId { get; set; }
        public long AfterTier { get; set; }
        public List<SelectTicketReplaceInfo>? ReplaceInfos { get; set; }
    }

    public class PotentialGrowthRequestDB
    {
        public PotentialStatBonusRateType Type { get; set; }
        public int Level { get; set; }
    }

    public abstract class ContentSaveDB
    {
        public ContentType ContentType { get; set; }
        public long AccountServerId { get; set; }
        public DateTime CreateTime { get; set; }
        public long StageUniqueId { get; set; }
        public long LastEnterStageEchelonNumber { get; set; }
        public List<ParcelInfo>? StageEntranceFee { get; set; }
        public Dictionary<long, long>? EnemyKillCountByUniqueId { get; set; }
        public long TacticClearTimeMscSum { get; set; }
        public long AccountLevelWhenCreateDB { get; set; }
        public string? BIEchelon { get; set; }
        public string? BIEchelon1 { get; set; }
        public string? BIEchelon2 { get; set; }
        public string? BIEchelon3 { get; set; }
        public string? BIEchelon4 { get; set; }
    }

    public abstract class ContentsValueChangeDB
    {
        public ContentsChangeType ContentsChangeType { get; set; }
    }

    public class CostumeDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.Costume; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public long UniqueId { get; set; }
        public long BoundCharacterServerId { get; set; }
    }

    public enum CraftProcessCompleteType : int
    {
        None = 0,
        ByTimeElapse = 1,
        ByPlayer = 2,
    }

    public enum CraftState : int
    {
        None = 0,
        BaseNode = 1,
        NodeSelecting = 2,
        Crafting = 3,
        Complete = 4,
    }

    public class CraftInfoDB
    {
        public long SlotSequence { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CraftSlotOpenDate { get; set; }
        public List<CraftNodeDB>? Nodes { get; set; }
        public IEnumerable<long>? ResultIds { get; set; }
        public IEnumerable<ParcelInfo>? RewardParcelInfos { get; set; }
    }

    public class CraftNodeDB
    {
        public CraftNodeTier NodeTier { get; set; }
        public long SlotSequence { get; set; }
        public long NodeId { get; set; }
        public long NodeQuality { get; set; }
        public long NodeLevel { get; set; }
        public int NodeRandomSeed { get; set; }
        public int NodeRandomSequence { get; set; }
        public List<long>? LeafNodeIds { get; set; }
        public long ResultId { get; set; }
        public CraftNodeResult? CraftNodeResult { get; set; }
        public ParcelInfo? RewardParcelInfo { get; set; }
    }

    public class ShiftingCraftInfoDB
    {
        public long SlotSequence { get; set; }
        public long CraftRecipeId { get; set; }
        public long CraftAmount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class CraftNodeResult
    {
        public CraftNodeTier NodeTier { get; set; }
        public ParcelInfo? ParcelInfo { get; set; }
    }

    public class CraftPresetSlotDB
    {
        public List<CraftPresetNodeDB>? PresetNodeDBs { get; set; }
    }

    public class CraftPresetNodeDB
    {
        public CraftNodeTier NodeTier { get; set; }
        public bool IsActivated { get; set; }
        public long PriortyNodeId { get; set; }
        public ConsumeRequestDB? ConsumeRequestDB { get; set; }
    }

    public class DetailedAccountInfoDB
    {
        public long AccountId { get; set; }
        public string? Nickname { get; set; }
        public long Level { get; set; }
        public string? ClanName { get; set; }
        public string? Comment { get; set; }
        public long FriendCount { get; set; }
        public string? FriendCode { get; set; }
        public long RepresentCharacterUniqueId { get; set; }
        public long CharacterCount { get; set; }
        public Nullable<long> LastNormalCampaignClearStageId { get; set; }
        public Nullable<long> LastHardCampaignClearStageId { get; set; }
        public Nullable<long> ArenaRanking { get; set; }
        public Nullable<long> RaidRanking { get; set; }
        public Nullable<int> RaidTier { get; set; }
        public Nullable<long> EliminateRaidRanking { get; set; }
        public Nullable<int> EliminateRaidTier { get; set; }
        public AssistCharacterDB[]? AssistCharacterDBs { get; set; }
    }

    public class EchelonDB
    {
        public long AccountServerId { get; set; }
        public EchelonType EchelonType { get; set; }
        public long EchelonNumber { get; set; }
        public EchelonExtensionType ExtensionType { get; set; }
        public long LeaderServerId { get; set; }
        public int MainSlotCount { get; set; }
        public int SupportSlotCount { get; set; }
        public List<long>? MainSlotServerIds { get; set; }
        public List<long>? SupportSlotServerIds { get; set; }
        public long TSSInteractionServerId { get; set; }
        public EchelonStatusFlag UsingFlag { get; set; }
        public bool IsUsing { get; set; }
        public List<long>? AllCharacterServerIds { get; set; }
        public List<long>? AllCharacterWithoutTSSServerIds { get; set; }
        public List<long>? AllCharacterWithEmptyServerIds { get; set; }
        public List<long>? BattleCharacterServerIds { get; set; }
        public List<long>? SkillCardMulliganCharacterIds { get; set; }
        public int[]? CombatStyleIndex { get; set; }
        public enum EchelonStatusFlag : int
        {
            None = 0,
            BeforeDeploy = 1,
            OnDuty = 2,
        }

    }

    public class EchelonPresetGroupDB
    {
        public int GroupIndex { get; set; }
        public EchelonExtensionType ExtensionType { get; set; }
        public string? GroupLabel { get; set; }
        public Dictionary<int, EchelonPresetDB>? PresetDBs { get; set; }
        public EchelonPresetDB? Item { get; set; }
    }

    public class EchelonPresetDB
    {
        public int GroupIndex { get; set; }
        public int Index { get; set; }
        public string? Label { get; set; }
        public long LeaderUniqueId { get; set; }
        public long TSSInteractionUniqueId { get; set; }
        public long[]? StrikerUniqueIds { get; set; }
        public long[]? SpecialUniqueIds { get; set; }
        public int[]? CombatStyleIndex { get; set; }
        public List<long>? MulliganUniqueIds { get; set; }
        public EchelonExtensionType ExtensionType { get; set; }
        public int StrikerSlotCount { get; set; }
        public int SpecialSlotCount { get; set; }
    }

    public class EmblemDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.Emblem; }

        public long UniqueId { get; set; }
        public DateTime ReceiveDate { get; set; }
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public class EquipmentDB : ConsumableItemBaseDB
    {
        public override ParcelType Type { get => ParcelType.Equipment; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int Tier { get; set; }
        public long BoundCharacterServerId { get; set; }
        public new bool CanConsume { get; set; }
    }

    public class EventContentBonusRewardDB
    {
        public long EventContentId { get; set; }
        public long EventStageUniqueId { get; set; }
        public long BonusPercentage { get; set; }
        public ParcelInfo? BonusParcelInfo { get; set; }
    }

    public class EventContentBoxGachaDB
    {
        public long EventContentId { get; set; }
        public long Seed { get; set; }
        public long Round { get; set; }
        public int PurchaseCount { get; set; }
    }

    public class EventContentBoxGachaData
    {
        public long EventContentId { get; set; }
        public Dictionary<long, EventContentBoxGachaVariation>? Variations { get; set; }
    }

    public class EventContentBoxGachaVariation
    {
        public long EventContentId { get; set; }
        public long VariationId { get; set; }
        public Dictionary<long, EventContentBoxGachaRoundElement>? GachaRoundElements { get; set; }
    }

    public class EventContentBoxGachaRoundElement
    {
        public long EventContentId { get; set; }
        public long VariationId { get; set; }
        public long Round { get; set; }
        public List<EventContentBoxGachaElement>? Elements { get; set; }
    }

    public class EventContentBoxGachaElement
    {
        public long EventContentId { get; set; }
        public long VariationId { get; set; }
        public long Round { get; set; }
        public long GroupId { get; set; }
        public long UniqueId { get; set; }
        public bool IsPrize { get; set; }
        public List<ParcelInfo>? Rewards { get; set; }
    }

    public class EventContentChangeDB
    {
        public long EventContentId { get; set; }
        public long UseAmount { get; set; }
        public long ChangeCount { get; set; }
        public long AccumulateChangeCount { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public bool ChangeFlag { get; set; }
    }

    public class EventContentCollectionDB
    {
        public long EventContentId { get; set; }
        public long GroupId { get; set; }
        public long UniqueId { get; set; }
        public DateTime ReceiveDate { get; set; }
    }

    public class EventContentDiceRaceDB
    {
        public long EventContentId { get; set; }
        public long Node { get; set; }
        public long LapCount { get; set; }
        public long DiceRollCount { get; set; }
        public long ReceiveRewardLapCount { get; set; }
    }

    public class EventContentDiceResult
    {
        public int Index { get; set; }
        public int MoveAmount { get; set; }
        public List<ParcelInfo>? Rewards { get; set; }
    }

    public class EventContentFortuneGachaStackCountDB
    {
        public long EventContentId { get; set; }
        public int GachaStackCount { get; set; }
    }

    public class EventContentLocationDB
    {
        public long LocationId { get; set; }
        public long Rank { get; set; }
        public long Exp { get; set; }
        public long ScheduleCount { get; set; }
        public Dictionary<long, List<VisitingCharacterDB>>? ZoneVisitCharacterDBs { get; set; }
    }

    public class EventContentPermanentDB
    {
        public long EventContentId { get; set; }
        public bool IsStageAllClear { get; set; }
        public bool IsReceivedCharacterReward { get; set; }
    }

    public class EventContentTreasureSaveBoard
    {
        public long VariationId { get; set; }
        public int Round { get; set; }
        public List<EventContentTreasureObject>? TreasureObjects { get; set; }
    }

    public class EventContentTreasureObject
    {
        public long ServerId { get; set; }
        public long RewardId { get; set; }
        public int Rotation { get; set; }
        public bool IsHiddenImage { get; set; }
        public List<EventContentTreasureCell>? Cells { get; set; }
    }

    public class EventContentTreasureCell
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class EventContentTreasureHistoryDB
    {
        public long EventContentId { get; set; }
        public int Round { get; set; }
        public EventContentTreasureBoardHistory? Board { get; set; }
        public bool IsComplete { get; set; }
        public List<EventContentTreasureObject>? HintTreasures { get; set; }
        public int MetaRound { get; set; }
        public bool CanComplete { get; set; }
        public bool CanFlip { get; set; }
        public EventContentTreasureInfo? TreasureInfo { get; set; }
        public EventContentTreasureRoundInfo? TreasureRoundInfo { get; set; }
    }

    public class EventContentTreasureBoardHistory
    {
        public List<long>? TreasureIds { get; set; }
        public List<EventContentTreasureCell>? NormalCells { get; set; }
        public List<EventContentTreasureObject>? Treasures { get; set; }
    }

    public class EventInfoDB
    {
        public long EventId { get; set; }
        public uint ImageNameHash { get; set; }
    }

    public class EventRewardIncreaseDB
    {
        public EventTargetType EventTargetType { get; set; }
        public BasisPoint Multiplier { get; set; }
        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class FieldStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.FieldContentStage; }

    }

    public class FriendDB
    {
        public long AccountId { get; set; }
        public int Level { get; set; }
        public string? Nickname { get; set; }
        public DateTime LastConnectTime { get; set; }
        public long RepresentCharacterUniqueId { get; set; }
        public long RepresentCharacterCostumeId { get; set; }
        public long ComfortValue { get; set; }
        public long FriendCount { get; set; }
        public AccountAttachmentDB? AttachmentDB { get; set; }
    }

    public class FriendIdCardDB
    {
        public int Level { get; set; }
        public string? FriendCode { get; set; }
        public string? Comment { get; set; }
        public DateTime LastConnectTime { get; set; }
        public long RepresentCharacterUniqueId { get; set; }
        public long RepresentCharacterCostumeId { get; set; }
        public bool SearchPermission { get; set; }
        public bool AutoAcceptFriendRequest { get; set; }
        public long CardBackgroundId { get; set; }
        public bool ShowAccountLevel { get; set; }
        public bool ShowFriendCode { get; set; }
        public bool ShowRaidRanking { get; set; }
        public bool ShowArenaRanking { get; set; }
        public bool ShowEliminateRaidRanking { get; set; }
        public Nullable<long> ArenaRanking { get; set; }
        public Nullable<long> RaidRanking { get; set; }
        public Nullable<int> RaidTier { get; set; }
        public Nullable<long> EliminateRaidRanking { get; set; }
        public Nullable<int> EliminateRaidTier { get; set; }
        public long EmblemId { get; set; }
    }

    public class FurnitureDB : ConsumableItemBaseDB
    {
        public override ParcelType Type { get => ParcelType.Furniture; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public FurnitureLocation Location { get; set; }
        public long CafeDBId { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float Rotation { get; set; }
        public long ItemDeploySequence { get; set; }
        public new bool CanConsume { get; set; }
    }

    public class GachaLogDB
    {
        public long CharacterId { get; set; }
    }

    public class GearDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.CharacterGear; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int Tier { get; set; }
        public long SlotIndex { get; set; }
        public long BoundCharacterServerId { get; set; }
        public EquipmentDB? ToEquipmentDB { get; set; }
    }

    public class GuideMissionSeasonDB
    {
        public long SeasonId { get; set; }
        public long LoginCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime LoginDate { get; set; }
        public bool IsComplete { get; set; }
        public bool IsFinalMissionComplete { get; set; }
        public Nullable<DateTime> CollectionItemReceiveDate { get; set; }
    }

    public class IdCardBackgroundDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.IdCardBackground; }

        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public enum IssueAlertTypeCode : int
    {
        All = 1,
        File_Target = 2,
        AllButFile_Exception = 3,
    }

    public class IssueAlertInfoDB
    {
        public int IssueAlertId { get; set; }
        public IssueAlertTypeCode IssueAlertType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public byte DisplayOrder { get; set; }
        public int PublishId { get; set; }
        public string? Url { get; set; }
        public string? Subject { get; set; }
    }

    public class ItemDB : ConsumableItemBaseDB
    {
        public override ParcelType Type { get => ParcelType.Item; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public new bool CanConsume { get; set; }
    }

    public class MailDB
    {
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public MailType Type { get; set; }
        public long UniqueId { get; set; }
        public string? Sender { get; set; }
        public Dictionary<Language, string>? LocalizedSender { get; set; }
        public string? Comment { get; set; }
        public Dictionary<Language, string>? LocalizedComment { get; set; }
        public DateTime SendDate { get; set; }
        public Nullable<DateTime> ReceiptDate { get; set; }
        public Nullable<DateTime> ExpireDate { get; set; }
        public List<ParcelInfo>? ParcelInfos { get; set; }
        public List<ParcelInfo>? RemainParcelInfos { get; set; }
    }

    public class MiniGameCCGSummary
    {
        public int RandomSeed { get; set; }
        public bool IsPlayerWin { get; set; }
        public List<MiniGameCCGCharacterDB>? Strikers { get; set; }
        public int TotalUsedCost { get; set; }
        public int TotalDamageAmount { get; set; }
        public int TotalKillCount { get; set; }
        public Dictionary<long, int>? TotalSkillCount { get; set; }
        public List<string>? InputLogs { get; set; }
    }

    public enum MiniGameCCGCampOption : int
    {
        Invalid = 0,
        Heal = 1,
        Revive = 2,
        RemoveCard = 3,
        Skip = 4,
    }

    public class MiniGameCCGCardDB
    {
        public int CardDBId { get; set; }
        public long CardId { get; set; }
    }

    public class MiniGameCCGCharacterDB
    {
        public int SlotIndex { get; set; }
        public long CharacterId { get; set; }
        public int Health { get; set; }
    }

    public enum MiniGameCCGStageRewardType : int
    {
        Invalid = 0,
        All = 1,
        Select = 2,
    }

    public class MiniGameCCGStageRewardDB
    {
        public MiniGameCCGStageRewardType Type { get; set; }
        public int RewardIndex { get; set; }
        public long[]? RewardIds { get; set; }
    }

    public class MiniGameCCGStagePlayDB
    {
        public long StageId { get; set; }
        public long EnemyGroupId { get; set; }
        public bool IsClear { get; set; }
        public List<MiniGameCCGStageRewardDB>? RewardDBs { get; set; }
        public int RerollPoint { get; set; }
    }

    public struct MiniGameCCGPlayHistory
    {
        public long LevelId { get; set; }
        public long NodeId { get; set; }
        public MiniGameCCGStagePlayDB StagePlayDB { get; set; }
    }

    public class MiniGameCCGSaveDB
    {
        public int CardDBIdProvider { get; set; }
        public long EventContentId { get; set; }
        public long CCGId { get; set; }
        public bool GameOver { get; set; }
        public bool Clear { get; set; }
        public List<MiniGameCCGCharacterDB>? Strikers { get; set; }
        public List<MiniGameCCGCharacterDB>? Specials { get; set; }
        public List<long>? OverflowedStrikerIds { get; set; }
        public List<long>? OverflowedSpecialIds { get; set; }
        public List<MiniGameCCGCardDB>? Deck { get; set; }
        public long LevelId { get; set; }
        public long CurrentNodeId { get; set; }
        public int RewardPoint { get; set; }
        public int CurrentStageIndex { get; set; }
        public int LastStageIndex { get; set; }
        public MiniGameCCGStagePlayDB? CurrentStageDB { get; set; }
        public List<MiniGameCCGPlayHistory>? ClearedHistoryDBs { get; set; }
        public List<long>? Perks { get; set; }
        public int TotalUsedCost { get; set; }
        public int TotalDamageAmount { get; set; }
        public int TotalKillCount { get; set; }
        public Dictionary<long, int>? TotalSkillCount { get; set; }
    }

    public class MiniGameDefenseStageHistoryDB
    {
        public long StageId { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public bool FirstClearRewardReceive { get; set; }
        public bool StarRewardReceive { get; set; }
        public bool IsThreeStar { get; set; }
        public bool IsFirstClear { get; set; }
    }

    public class MiniGameDreamMakerInfoDB
    {
        public long EventContentId { get; set; }
        public long Round { get; set; }
        public long Multiplier { get; set; }
        public long DayOfNumber { get; set; }
        public long ActionCount { get; set; }
        public long CurrentRoundEndingId { get; set; }
        public bool EndingRewardReceived { get; set; }
        public bool CanStartNewGame { get; set; }
        public bool CanReceiveEndingReward { get; set; }
    }

    public class MiniGameDreamMakerParameterDB
    {
        public DreamMakerParameterType ParameterType { get; set; }
        public long BaseAmount { get; set; }
        public long CurrentAmount { get; set; }
    }

    public class MiniGameDreamMakerEndingDB
    {
        public long EventContentId { get; set; }
        public long EndingId { get; set; }
        public long ClearCount { get; set; }
        public DateTime ClearDate { get; set; }
    }

    public class MiniGameHistoryDB
    {
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long HighScore { get; set; }
        public long AccumulatedScore { get; set; }
        public DateTime ClearDate { get; set; }
        public bool IsFullCombo { get; set; }
    }

    public class MiniGameResult
    {
        public EventContentType ContentType { get; set; }
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long TotalScore { get; set; }
        public long ComboCount { get; set; }
        public long FeverCount { get; set; }
        public bool AllCombo { get; set; }
        public long HPBonusScore { get; set; }
        public long NoteCount { get; set; }
        public long CriticalCount { get; set; }
    }

    public class MiniGameRoadPuzzleHistoryDB
    {
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long Round { get; set; }
        public DateTime ClearDate { get; set; }
    }

    public class MiniGameShootingHistoryDB
    {
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long ArriveSection { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public bool IsClearToday { get; set; }
    }

    public class MissionHistoryDB
    {
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long MissionUniqueId { get; set; }
        public DateTime CompleteTime { get; set; }
        public bool Expired { get; set; }
    }

    public class MissionProgressDB
    {
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long MissionUniqueId { get; set; }
        public bool Complete { get; set; }
        public DateTime StartTime { get; set; }
        public Dictionary<long, long>? ProgressParameters { get; set; }
    }

    public class MomoTalkOutLineDB
    {
        public long CharacterDBId { get; set; }
        public long CharacterId { get; set; }
        public long LatestMessageGroupId { get; set; }
        public Nullable<long> ChosenMessageId { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }

    public class MomoTalkChoiceDB
    {
        public long CharacterDBId { get; set; }
        public long MessageGroupId { get; set; }
        public long ChosenMessageId { get; set; }
        public DateTime ChosenDate { get; set; }
    }

    public class MonthlyProductPurchaseDB
    {
        public long ProductId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public Nullable<DateTime> LastDailyRewardDate { get; set; }
        public Nullable<DateTime> RewardEndDate { get; set; }
        public ProductTagType ProductTagType { get; set; }
    }

    public class MultiFloorRaidDB
    {
        public long SeasonId { get; set; }
        public int ClearedDifficulty { get; set; }
        public DateTime LastClearDate { get; set; }
        public int RewardDifficulty { get; set; }
        public DateTime LastRewardDate { get; set; }
        public int ClearBattleFrame { get; set; }
        public bool AllCleared { get; set; }
        public bool HasReceivableRewards { get; set; }
        public List<ParcelInfo>? TotalReceivableRewards { get; set; }
        public List<ParcelInfo>? TotalReceivedRewards { get; set; }
    }

    public class MultiSweepPresetDB
    {
        public long PresetId { get; set; }
        public string? PresetName { get; set; }
        public IEnumerable<long>? StageIds { get; set; }
        public IEnumerable<ParcelKeyPair>? ParcelIds { get; set; }
    }

    public class OpenConditionDB
    {
        public OpenConditionContent ContentType { get; set; }
        public bool HideWhenLocked { get; set; }
        public long AccountLevel { get; set; }
        public long ScenarioModeId { get; set; }
        public long CampaignStageUniqueId { get; set; }
        public MultipleConditionCheckType MultipleConditionCheckType { get; set; }
        public WeekDay OpenDayOfWeek { get; set; }
        public long OpenHour { get; set; }
        public WeekDay CloseDayOfWeek { get; set; }
        public long CloseHour { get; set; }
        public long CafeIdForCafeRank { get; set; }
        public long CafeRank { get; set; }
        public long OpenedCafeId { get; set; }
    }

    public class PickupCharacterSelectionDB
    {
        public long AccountId { get; set; }
        public long ShopRecruitId { get; set; }
        public long PickupIndex { get; set; }
        public long CharacterId { get; set; }
    }

    public class PickupFirstGetHistoryDB
    {
        public long ShopRecruitId { get; set; }
        public DateTime LogDate { get; set; }
    }

    public class ProductPurchaseCountDB
    {
        public long EventContentId { get; set; }
        public long ShopExcelId { get; set; }
        public int PurchaseCount { get; set; }
        public DateTime LastPurchaseDate { get; set; }
        public PurchaseCountResetType PurchaseCountResetType { get; set; }
        public DateTime ResetDate { get; set; }
    }

    public class ProtocolLockDB
    {
        public int ProtocolId { get; set; }
        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreateDate { get; set; }
    }

    public class PurchaseCountDB
    {
        public long ShopCashId { get; set; }
        public int PurchaseCount { get; set; }
        public DateTime ResetDate { get; set; }
        public Nullable<DateTime> PurchaseDate { get; set; }
        public Nullable<DateTime> ManualResetDate { get; set; }
    }

    public class PurchaseOrderByNexonDB
    {
        public long ProductId { get; set; }
        public int state { get; set; }
        public long PurchaseOrderId { get; set; }
    }

    public class PurchaseOrderDB
    {
        public long ShopCashId { get; set; }
        public PurchaseStatusCode StatusCode { get; set; }
        public long PurchaseOrderId { get; set; }
    }

    public class RaidBattleDB
    {
        public ContentType ContentType { get; set; }
        public long RaidUniqueId { get; set; }
        public int RaidBossIndex { get; set; }
        public long CurrentBossHP { get; set; }
        public long CurrentBossGroggy { get; set; }
        public long CurrentBossAIPhase { get; set; }
        public string? BIEchelon { get; set; }
        public bool IsClear { get; set; }
        public RaidMemberCollection? RaidMembers { get; set; }
        public List<long>? SubPartsHPs { get; set; }
    }

    public class RaidBossDB
    {
        public ContentType ContentType { get; set; }
        public int BossIndex { get; set; }
        public long BossCurrentHP { get; set; }
        public long BossGroggyPoint { get; set; }
    }

    public class RaidDB
    {
        public RaidMemberDescription? Owner { get; set; }
        public ContentType ContentType { get; set; }
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public long SeasonId { get; set; }
        public DateTime Begin { get; set; }
        public DateTime End { get; set; }
        public long OwnerAccountServerId { get; set; }
        public string? OwnerNickname { get; set; }
        public long PlayerCount { get; set; }
        public string? BossGroup { get; set; }
        public Difficulty BossDifficulty { get; set; }
        public int LastBossIndex { get; set; }
        public List<int>? Tags { get; set; }
        public string? SecretCode { get; set; }
        public RaidStatus RaidState { get; set; }
        public bool IsPractice { get; set; }
        public List<RaidBossDB>? RaidBossDBs { get; set; }
        public Dictionary<long, List<long>>? ParticipateCharacterServerIds { get; set; }
        public bool IsEnterRoom { get; set; }
        public long SessionHitPoint { get; set; }
        public long AccountLevelWhenCreateDB { get; set; }
        public bool ClanAssistUsed { get; set; }
    }

    public abstract class RaidLobbyInfoDB
    {
        public long SeasonId { get; set; }
        public int Tier { get; set; }
        public long Ranking { get; set; }
        public long BestRankingPoint { get; set; }
        public long TotalRankingPoint { get; set; }
        public long ReceivedRankingRewardId { get; set; }
        public bool CanReceiveRankingReward { get; set; }
        public RaidDB? PlayingRaidDB { get; set; }
        public List<long>? ReceiveRewardIds { get; set; }
        public List<long>? ReceiveLimitedRewardIds { get; set; }
        public List<long>? ParticipateCharacterServerIds { get; set; }
        public Dictionary<string, Difficulty>? PlayableHighestDifficulty { get; set; }
        public Dictionary<long, long>? SweepPointByRaidUniqueId { get; set; }
        public DateTime SeasonStartDate { get; set; }
        public DateTime SeasonEndDate { get; set; }
        public DateTime SettlementEndDate { get; set; }
        public long NextSeasonId { get; set; }
        public DateTime NextSeasonStartDate { get; set; }
        public DateTime NextSeasonEndDate { get; set; }
        public DateTime NextSettlementEndDate { get; set; }
        public ClanAssistUseInfo? ClanAssistUseInfo { get; set; }
        public Dictionary<int, bool>? RemainFailCompensation { get; set; }
    }

    public class SingleRaidLobbyInfoDB : RaidLobbyInfoDB
    {
        public List<Difficulty>? ClearDifficulty { get; set; }
    }

    public class EliminateRaidLobbyInfoDB : RaidLobbyInfoDB
    {
        public List<string>? OpenedBossGroups { get; set; }
        public Dictionary<string, long>? BestRankingPointPerBossGroup { get; set; }
    }

    public class RaidGiveUpDB
    {
        public long Ranking { get; set; }
        public long RankingPoint { get; set; }
        public long BestRankingPoint { get; set; }
    }

    public abstract class RaidUserDB
    {
        public long AccountId { get; set; }
        public long RepresentCharacterUniqueId { get; set; }
        public long RepresentCharacterCostumeId { get; set; }
        public long Level { get; set; }
        public string? Nickname { get; set; }
        public int Tier { get; set; }
        public long Rank { get; set; }
        public long BestRankingPoint { get; set; }
        public double BestRankingPointDetail { get; set; }
        public AccountAttachmentDB? AccountAttachmentDB { get; set; }
    }

    public class SingleRaidUserDB : RaidUserDB
    {
        public RaidTeamSettingDB? RaidTeamSettingDB { get; set; }
    }

    public class EliminateRaidUserDB : RaidUserDB
    {
        public Dictionary<string, long>? BossGroupToRankingPoint { get; set; }
    }

    public class RaidTeamSettingDB
    {
        public long AccountId { get; set; }
        public long TryNumber { get; set; }
        public EchelonType EchelonType { get; set; }
        public EchelonExtensionType EchelonExtensionType { get; set; }
        public IList<RaidCharacterDB>? MainCharacterDBs { get; set; }
        public IList<RaidCharacterDB>? SupportCharacterDBs { get; set; }
        public IList<long>? SkillCardMulliganCharacterIds { get; set; }
        public long TSSInteractionUniqueId { get; set; }
        public long LeaderCharacterUniqueId { get; set; }
    }

    public class EliminateRaidAdditionalRankingInfo
    {
        public Dictionary<string, long>? BossGroupToScore { get; set; }
    }

    public class RaidCharacterDB
    {
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public int SlotIndex { get; set; }
        public long AccountId { get; set; }
        public bool IsAssist { get; set; }
        public bool HasWeapon { get; set; }
        public int WeaponStarGrade { get; set; }
        public long CostumeId { get; set; }
        public int CombatStyleIndex { get; set; }
    }

    public class RaidRankingInfo
    {
        public long SeasonId { get; set; }
        public long AccountId { get; set; }
        public long Ranking { get; set; }
        public long Score { get; set; }
        public double ScoreDetail { get; set; }
    }

    public class RaidRankBracket
    {
        public Difficulty Difficulty { get; set; }
        public Nullable<int> BossGroupIndex { get; set; }
        public long RankCount { get; set; }
    }

    public class RaidDetailDB
    {
        public long RaidUniqueId { get; set; }
        public DateTime EndDate { get; set; }
        public List<RaidPlayerInfoDB>? DamageTable { get; set; }
    }

    public class RaidLimitedRewardHistoryDB
    {
        public ContentType ContentType { get; set; }
        public long SeasonId { get; set; }
        public long RewardId { get; set; }
        public DateTime ReceiveDate { get; set; }
    }

    public class RaidParticipateCharactersDB
    {
        public long RaidServerId { get; set; }
        public long AccountServerId { get; set; }
        public List<long>? ParticipateCharacterServerIds { get; set; }
    }

    public class RaidPlayerInfoDB
    {
        public long RaidServerId { get; set; }
        public long AccountId { get; set; }
        public DateTime JoinDate { get; set; }
        public long DamageAmount { get; set; }
        public int RaidEndRewardFlag { get; set; }
        public int RaidPlayCount { get; set; }
        public string? Nickname { get; set; }
        public long CharacterId { get; set; }
        public long CostumeId { get; set; }
        public Nullable<long> AccountLevel { get; set; }
    }

    public class RaidSeasonHistoryDB
    {
        public long SeasonServerId { get; set; }
        public DateTime ReceiveDateTime { get; set; }
        public long SeasonRewardGauage { get; set; }
    }

    public class RaidSeasonManageDB
    {
        public long SeasonId { get; set; }
        public DateTime SeasonStartDate { get; set; }
        public DateTime SeasonEndDate { get; set; }
        public DateTime SeasonSettlementEndDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }

    public class RaidSeasonPointRewardHistoryDB
    {
        public ContentType ContentType { get; set; }
        public long SeasonId { get; set; }
        public long LastReceivedSeasonRewardId { get; set; }
        public DateTime SeasonRewardReceiveDate { get; set; }
    }

    public class RaidSeasonRankingHistoryDB
    {
        public ContentType ContentType { get; set; }
        public long AccountId { get; set; }
        public long SeasonId { get; set; }
        public long Ranking { get; set; }
        public long BestRankingPoint { get; set; }
        public int Tier { get; set; }
        public DateTime ReceivedDate { get; set; }
    }

    public struct ResetableContentId
    {
        public ResetContentType Type { get; set; }
        public long Mapped { get; set; }
    }

    public class ResetableContentValueDB
    {
        public ResetableContentId ResetableContentId { get; set; }
        public long ContentValue { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    public class ScenarioCollectionDB
    {
        public long GroupId { get; set; }
        public long UniqueId { get; set; }
        public DateTime ReceiveDate { get; set; }
    }

    public class ScenarioHistoryDB
    {
        public long ScenarioUniqueId { get; set; }
        public DateTime ClearDateTime { get; set; }
    }

    public class ScenarioGroupHistoryDB
    {
        public long AccountServerId { get; set; }
        public long ScenarioGroupUqniueId { get; set; }
        public long ScenarioType { get; set; }
        public Nullable<long> EventContentId { get; set; }
        public DateTime ClearDateTime { get; set; }
        public bool IsReturn { get; set; }
    }

    public class SchoolDungeonStageHistoryDB
    {
        public long StageUniqueId { get; set; }
        public long BestStarRecord { get; set; }
        public bool[]? StarFlags { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public bool IsClearedEver { get; set; }
    }

    public class SchoolDungeonStageSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.SchoolDungeon; }

    }

    public class SelectGachaSnapshotDB
    {
        public long ShopUniqueId { get; set; }
        public long LastIndex { get; set; }
        public List<long>? LastResults { get; set; }
        public Nullable<long> SavedIndex { get; set; }
        public List<long>? SavedResults { get; set; }
        public Nullable<long> PickedIndex { get; set; }
    }

    public class SessionDB
    {
        public SessionKey? SessionKey { get; set; }
        public DateTime LastConnect { get; set; }
        public int ConnectionTime { get; set; }
    }

    public class ShopCashProductSelectionDB
    {
        public long SlotGroupId { get; set; }
        public long SlotIndex { get; set; }
        public long SelectionId { get; set; }
    }

    public class ShopEligmaHistoryDB
    {
        public long CharacterUniqueId { get; set; }
        public long PurchaseCount { get; set; }
    }

    public class ShopFreeRecruitHistoryDB
    {
        public long UniqueId { get; set; }
        public int RecruitCount { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }

    public enum ShopProductType : int
    {
        None = 0,
        General = 1,
        Refresh = 2,
    }

    public class ShopInfoDB
    {
        public long EventContentId { get; set; }
        public ShopCategoryType Category { get; set; }
        public Nullable<long> ManualRefreshCount { get; set; }
        public bool IsRefresh { get; set; }
        public ShopGroupType ShopGroupType { get; set; }
        public Nullable<DateTime> NextAutoRefreshDate { get; set; }
        public Nullable<DateTime> LastAutoRefreshDate { get; set; }
        public List<ShopProductDB>? ShopProductList { get; set; }
    }

    public class ShopProductDB
    {
        public long EventContentId { get; set; }
        public long ShopExcelId { get; set; }
        public ShopCategoryType Category { get; set; }
        public long DisplayOrder { get; set; }
        public long PurchaseCount { get; set; }
        public bool SoldOut { get; set; }
        public long PurchaseCountLimit { get; set; }
        public long Price { get; set; }
        public ShopProductType ProductType { get; set; }
    }

    public class ShopRecruitDB
    {
        public long Id { get; set; }
        public DateTime SalesStartDate { get; set; }
        public DateTime SalesEndDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }

    public class SkipHistoryDB
    {
        public int Prologue { get; set; }
        public Dictionary<int, int>? Tutorial { get; set; }
    }

    public class MemoryLobbyDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.MemoryLobby; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public long MemoryLobbyUniqueId { get; set; }
    }

    public class StickerBookDB
    {
        public long AccountId { get; set; }
        public IEnumerable<StickerDB>? UnusedStickerDBs { get; set; }
        public IEnumerable<StickerDB>? UsedStickerDBs { get; set; }
    }

    public class StickerDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.Sticker; }

        public long StickerUniqueId { get; set; }
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public class StoryStrategyStageSaveDB : CampaignMainStageSaveDB
    {
        public new static ContentType ContentType { get => ContentType.StoryStrategyStage; }

    }

    public class StrategyObjectHistoryDB
    {
        public long StrategyObjectId { get; set; }
    }

    public class TimeAttackDungeonCharacterDB
    {
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public long CostumeId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public bool HasWeapon { get; set; }
        public WeaponDB? WeaponDB { get; set; }
        public bool IsAssist { get; set; }
        public int CombatStyleIndex { get; set; }
    }

    public class TimeAttackDungeonBattleHistoryDB
    {
        public TimeAttackDungeonType DungeonType { get; set; }
        public long GeasId { get; set; }
        public long DefaultPoint { get; set; }
        public long ClearTimePoint { get; set; }
        public long EndFrame { get; set; }
        public long TotalPoint { get; set; }
        public List<TimeAttackDungeonCharacterDB>? MainCharacterDBs { get; set; }
        public List<TimeAttackDungeonCharacterDB>? SupportCharacterDBs { get; set; }
    }

    public class TimeAttackDungeonRewardHistoryDB
    {
        public DateTime Date { get; set; }
        public TimeAttackDungeonRoomDB? RoomDB { get; set; }
        public bool IsSweep { get; set; }
    }

    public class TimeAttackDungeonRoomDB
    {
        public long AccountId { get; set; }
        public long SeasonId { get; set; }
        public long RoomId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime RewardDate { get; set; }
        public bool IsPractice { get; set; }
        public List<DateTime>? SweepHistoryDates { get; set; }
        public List<TimeAttackDungeonBattleHistoryDB>? BattleHistoryDBs { get; set; }
        public int PlayCount { get; set; }
        public long TotalPointSum { get; set; }
        public bool IsRewardReceived { get; set; }
        public bool IsOpened { get; set; }
        public bool CanUseAssist { get; set; }
        public bool IsPlayCountOver { get; set; }
    }

    public class ToastDB
    {
        public long UniqueId { get; set; }
        public string? Text { get; set; }
        public Dictionary<Language, string>? LocalizeText { get; set; }
        public string? ToastId { get; set; }
        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
        public int LifeTime { get; set; }
        public int Delay { get; set; }
    }

    public class VisitingCharacterDB
    {
        public long UniqueId { get; set; }
        public long ServerId { get; set; }
    }

    public class WeaponDB : ParcelBase
    {
        public override ParcelType Type { get => ParcelType.CharacterWeapon; }

        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
        public long UniqueId { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int StarGrade { get; set; }
        public long BoundCharacterServerId { get; set; }
    }

    public class WeekDungeonSaveDB : ContentSaveDB
    {
        public new static ContentType ContentType { get => ContentType.WeekDungeon; }

        public WeekDungeonType WeekDungeonType { get; set; }
        public int Seed { get; set; }
        public int Sequence { get; set; }
    }

    public class WeekDungeonStageHistoryDB
    {
        public long AccountServerId { get; set; }
        public long StageUniqueId { get; set; }
        public Dictionary<StarGoalType, long>? StarGoalRecord { get; set; }
        public bool IsCleardEver { get; set; }
    }

    public class WorldRaidSnapshot
    {
        public List<WorldRaidLocalBossDB>? WorldRaidLocalBossDBs { get; set; }
        public List<WorldRaidClearHistoryDB>? WorldRaidClearHistoryDBs { get; set; }
    }

    public class WorldRaidLocalBossDB
    {
        public long SeasonId { get; set; }
        public long GroupId { get; set; }
        public long UniqueId { get; set; }
        public bool IsScenario { get; set; }
        public bool IsCleardEver { get; set; }
        public long TacticMscSum { get; set; }
        public RaidBattleDB? RaidBattleDB { get; set; }
        public bool IsContinue { get; set; }
    }

    public class WorldRaidWorldBossDB
    {
        public long GroupId { get; set; }
        public long HP { get; set; }
        public long Participants { get; set; }
    }

    public class WorldRaidClearHistoryDB
    {
        public long SeasonId { get; set; }
        public long GroupId { get; set; }
        public DateTime RewardReceiveDate { get; set; }
    }

    public class WorldRaidBossListInfoDB
    {
        public long GroupId { get; set; }
        public WorldRaidWorldBossDB? WorldBossDB { get; set; }
        public List<WorldRaidLocalBossDB>? LocalBossDBs { get; set; }
    }

    public class WorldRaidBossGroup : ContentsValueChangeDB
    {
        public new ContentsChangeType ContentsChangeType { get; set; }
        public long GroupId { get; set; }
        public DateTime BossSpawnTime { get; set; }
        public DateTime EliminateTime { get; set; }
    }

    public class WorldRaidBossDamageRatio : ContentsValueChangeDB
    {
        public new ContentsChangeType ContentsChangeType { get; set; }
        public BasisPoint DamageRatio { get; set; }
    }

}




