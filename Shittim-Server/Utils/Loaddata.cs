using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;

namespace Shittim.Utils
{
	public class ImportAccountAuthRequest
	{
		public long Protocol { get; set; }
		public long Version { get; set; }
		public string DevId { get; set; }
		public long IMEI { get; set; }
		public string AccessIP { get; set; }
		public string MarketId { get; set; }
		public string UserType { get; set; }
		public string AdvertisementId { get; set; }
		public string OSType { get; set; }
		public string OSVersion { get; set; }
		public string DeviceUniqueId { get; set; }
		public string DeviceModel { get; set; }
		public int DeviceSystemMemorySize { get; set; }

		public string CountryCode { get; set; }
		public string Idfv { get; set; }
		public bool IsTeenVersion { get; set; }
		public string DeviceLocaleCode { get; set; }
		public string GameOptionLanguage { get; set; }
	}

	public class ImportAccountAuthResponse
	{
		public long Protocol { get; set; }
		public long CurrentVersion { get; set; }
		public long MinimumVersion { get; set; }
		public bool IsDevelopment { get; set; }
		public bool BattleValidation { get; set; }
		public bool UpdateRequired { get; set; }
		public string TTSCdnUri { get; set; }
		public ImportAccountDB AccountDB { get; set; }
		public IEnumerable<AttendanceBookReward> AttendanceBookRewards { get; set; }
		public IEnumerable<AttendanceHistoryDB> AttendanceHistoryDBs { get; set; }
		public IEnumerable<OpenConditionDB> OpenConditions { get; set; }
		public IEnumerable<PurchaseCountDB> RepurchasableMonthlyProductCountDBs { get; set; }
		public IEnumerable<ParcelInfo> MonthlyProductParcel { get; set; }
		public IEnumerable<ParcelInfo> MonthlyProductMail { get; set; }
		public IEnumerable<ParcelInfo> BiweeklyProductParcel { get; set; }
		public IEnumerable<ParcelInfo> BiweeklyProductMail { get; set; }
		public IEnumerable<ParcelInfo> WeeklyProductParcel { get; set; }
		public IEnumerable<ParcelInfo> WeeklyProductMail { get; set; }
		public string EncryptedUID { get; set; }
		public AccountRestrictionsDB AccountRestrictionsDB { get; set; }
		public IEnumerable<IssueAlertInfoDB> IssueAlertInfos { get; set; }

		public IEnumerable<AccountBanByNexonDB> accountBanByNexonDBs { get; set; }
	}

	public class ImportAccountLoginSyncRequest
	{
		public long Protocol { get; set; }
		public List<long> SyncProtocols { get; set; }
		public string SkillCutInOption { get; set; }
	}

	public class ImportAccountLoginSyncResponse
	{
		public long Protocol { get; set; }
		public ResponsePacket Responses { get; set; }
		public CafeGetInfoResponse CafeGetInfoResponse { get; set; }
		public AccountCurrencySyncResponse AccountCurrencySyncResponse { get; set; }
		public CharacterListResponse CharacterListResponse { get; set; }
		public EquipmentItemListResponse EquipmentItemListResponse { get; set; }
		public CharacterGearListResponse CharacterGearListResponse { get; set; }
		public ImportItemListResponse ItemListResponse { get; set; }
		public EchelonListResponse EchelonListResponse { get; set; }
		public MemoryLobbyListResponse MemoryLobbyListResponse { get; set; }
		public CampaignListResponse CampaignListResponse { get; set; }
		public ArenaLoginResponse ArenaLoginResponse { get; set; }
		public RaidLoginResponse RaidLoginResponse { get; set; }
		public EliminateRaidLoginResponse EliminateRaidLoginResponse { get; set; }
		public CraftInfoListResponse CraftInfoListResponse { get; set; }
		public ClanLoginResponse ClanLoginResponse { get; set; }
		public MomoTalkOutLineResponse MomotalkOutlineResponse { get; set; }
		public ScenarioListResponse ScenarioListResponse { get; set; }
		public ShopGachaRecruitListResponse ShopGachaRecruitListResponse { get; set; }
		public TimeAttackDungeonLoginResponse TimeAttackDungeonLoginResponse { get; set; }
		public BillingPurchaseListByYostarResponse BillingPurchaseListByYostarResponse { get; set; }
		public EventContentPermanentListResponse EventContentPermanentListResponse { get; set; }
		public AttachmentGetResponse AttachmentGetResponse { get; set; }
		public AttachmentEmblemListResponse AttachmentEmblemListResponse { get; set; }
		public ContentSweepMultiSweepPresetListResponse ContentSweepMultiSweepPresetListResponse { get; set; }
		public StickerLoginResponse StickerListResponse { get; set; }
		public MultiFloorRaidSyncResponse MultiFloorRaidSyncResponse { get; set; }
		public long FriendCount { get; set; }
		public string FriendCode { get; set; }
	}

	public class ImportItemListRequest
	{
		public long Protocol { get; set; }
	}

	public class ImportItemListResponse
	{
		public long Protocol { get; set; }
		public List<ItemDBServer>? ItemDBs { get; set; }
		public List<ItemDBServer>? ExpiryItemDBs { get; set; }
	}

	public class ImportAccountDB
	{
		public long ServerId { get; set; }
		public string? Nickname { get; set; }
		public string? CallName { get; set; }
		public string? DevId { get; set; }
		public AccountState State { get; set; }
		public int Level { get; set; }
		public long Exp { get; set; }
		public string? Comment { get; set; }
		public int LobbyMode { get; set; }
		public long RepresentCharacterServerId { get; set; }
		public long MemoryLobbyUniqueId { get; set; }
		public DateTime LastConnectTime { get; set; }
		public DateTime? BirthDay { get; set; }
		public DateTime CallNameUpdateTime { get; set; }
		public long PublisherAccountId { get; set; }
		public int? RetentionDays { get; set; }
		public int? VIPLevel { get; set; }
		public DateTime CreateDate { get; set; }
		public int? UnReadMailCount { get; set; }
		public DateTime? LinkRewardDate { get; set; }

		public string? CallNameKatakana { get; set; }
		public string? CallNameKorean { get; set; }
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
		public string BanComment { get; set; }
		public int DeleteFlag { get; set; }
	}

	public class AccountData
	{
	    [JsonPropertyName("payload")]
	    public JsonElement Payload { get; set; }

	    [JsonPropertyName("type")]
	    public string Type { get; set; }
	}
}
