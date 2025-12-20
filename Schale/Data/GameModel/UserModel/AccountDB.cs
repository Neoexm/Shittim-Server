using System.ComponentModel.DataAnnotations;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class AccountDBServer
    {
        internal virtual ICollection<AccountCurrencyDBServer> Currencies { get; }
        internal virtual ICollection<ItemDBServer> Items { get; }
        internal virtual ICollection<CharacterDBServer> Characters { get; }
        internal virtual ICollection<EquipmentDBServer> Equipments { get; }
        internal virtual ICollection<WeaponDBServer> Weapons { get; }
        internal virtual ICollection<GearDBServer> Gears { get; }
        internal virtual ICollection<EchelonDBServer> Echelons { get; }
        internal virtual ICollection<EchelonPresetDBServer> EchelonPresets { get; }
        internal virtual ICollection<EchelonPresetGroupDBServer> EchelonPresetGroups { get; }
        internal virtual ICollection<CafeDBServer> Cafes { get; }
        internal virtual ICollection<FurnitureDBServer> Furnitures { get; }
        internal virtual ICollection<MemoryLobbyDBServer> MemoryLobbies { get; }
        internal virtual ICollection<MailDBServer> Mails { get; }
        internal virtual ICollection<EmblemDBServer> Emblems { get; }
        internal virtual ICollection<IdCardBackgroundDBServer> IdCardBackgrounds { get; }
        internal virtual ICollection<CostumeDBServer> Costumes { get; }
        internal virtual ICollection<StickerDBServer> Stickers { get; }
        internal virtual ICollection<AccountAttachmentDBServer> AccountAttachments { get; }
        internal virtual ICollection<AccountLevelRewardDBServer> AccountLevelRewards { get; }

        internal virtual ICollection<MissionProgressDBServer> MissionProgresses { get; }
        internal virtual ICollection<AttendanceHistoryDBServer> AttendanceHistories { get; }
        internal virtual ICollection<AcademyDBServer> Academies { get; }
        internal virtual ICollection<AcademyLocationDBServer> AcademyLocations { get; }
        internal virtual ICollection<CampaignMainStageSaveDBServer> CampaignMainStageSaves { get; }
        internal virtual ICollection<CampaignChapterClearRewardHistoryDBServer> CampaignChapterClearRewardHistories { get; }
        internal virtual ICollection<StrategyObjectHistoryDBServer> StrategyObjectHistories { get; }
        internal virtual ICollection<ScenarioHistoryDBServer> ScenarioHistories { get; }
        internal virtual ICollection<ScenarioGroupHistoryDBServer> ScenarioGroupHistories { get; }
        internal virtual ICollection<CampaignStageHistoryDBServer> CampaignStageHistories { get; }
        internal virtual ICollection<WeekDungeonStageHistoryDBServer> WeekDungeonStageHistories { get; }
        internal virtual ICollection<SchoolDungeonStageHistoryDBServer> SchoolDungeonStageHistories { get; }
        internal virtual ICollection<MomoTalkOutLineDBServer> MomoTalkOutLines { get; }
        internal virtual ICollection<MomoTalkChoiceDBServer> MomoTalkChoices { get; }
        internal virtual ICollection<EventContentPermanentDBServer> EventContentPermanents { get; }
        internal virtual ICollection<StickerBookDBServer> StickerBooks { get; }
        internal virtual ICollection<ShopFreeRecruitHistoryDBServer> ShopFreeRecruitHistories { get; }
        internal virtual ICollection<CraftInfoDBServer> CraftInfos { get; }

        internal virtual ICollection<SingleRaidLobbyInfoDBServer> SingleRaidLobbyInfos { get; }
        internal virtual ICollection<EliminateRaidLobbyInfoDBServer> EliminateRaidLobbyInfos { get; }
        internal virtual ICollection<RaidDBServer> Raids { get; }
        internal virtual ICollection<RaidBattleDBServer> RaidBattles { get; }
        internal virtual ICollection<TimeAttackDungeonRoomDBServer> TimeAttackDungeonRooms { get; }
        internal virtual ICollection<TimeAttackDungeonBattleHistoryDBServer> TimeAttackDungeonBattleHistories { get; }
        internal virtual ICollection<MultiFloorRaidDBServer> MultiFloorRaids { get; }
        internal virtual ICollection<WorldRaidLocalBossDBServer> WorldRaidLocalBosses { get; }
        internal virtual ICollection<WorldRaidClearHistoryDBServer> WorldRaidClearHistories { get; }

        public virtual AccountGameSettingDB GameSettings { get; set; } = new();
        public virtual ContentInfoDB ContentInfo { get; set; } = new();
        public virtual ICollection<BattleSummaryDB> BattleSummaries { get; set; }
        public virtual ICollection<RaidSummaryDB> RaidSummaries { get; set; }

        public AccountDBServer()
        {
            Currencies = new List<AccountCurrencyDBServer>();
            Items = new List<ItemDBServer>();
            Characters = new List<CharacterDBServer>();
            Equipments = new List<EquipmentDBServer>();
            Weapons = new List<WeaponDBServer>();
            Gears = new List<GearDBServer>();
            Echelons = new List<EchelonDBServer>();
            EchelonPresets = new List<EchelonPresetDBServer>();
            EchelonPresetGroups = new List<EchelonPresetGroupDBServer>();
            Cafes = new List<CafeDBServer>();
            Furnitures = new List<FurnitureDBServer>();
            MemoryLobbies = new List<MemoryLobbyDBServer>();
            Mails = new List<MailDBServer>();
            Emblems = new List<EmblemDBServer>();
            IdCardBackgrounds = new List<IdCardBackgroundDBServer>();
            Costumes = new List<CostumeDBServer>();
            Stickers = new List<StickerDBServer>();
            AccountAttachments = new List<AccountAttachmentDBServer>();
            AccountLevelRewards = new List<AccountLevelRewardDBServer>();

            MissionProgresses = new List<MissionProgressDBServer>();
            AttendanceHistories = new List<AttendanceHistoryDBServer>();
            Academies = new List<AcademyDBServer>();
            AcademyLocations = new List<AcademyLocationDBServer>();
            CampaignMainStageSaves = new List<CampaignMainStageSaveDBServer>();
            CampaignChapterClearRewardHistories = new List<CampaignChapterClearRewardHistoryDBServer>();
            StrategyObjectHistories = new List<StrategyObjectHistoryDBServer>();
            ScenarioHistories = new List<ScenarioHistoryDBServer>();
            ScenarioGroupHistories = new List<ScenarioGroupHistoryDBServer>();
            CampaignStageHistories = new List<CampaignStageHistoryDBServer>();
            WeekDungeonStageHistories = new List<WeekDungeonStageHistoryDBServer>();
            SchoolDungeonStageHistories = new List<SchoolDungeonStageHistoryDBServer>();
            MomoTalkOutLines = new List<MomoTalkOutLineDBServer>();
            MomoTalkChoices = new List<MomoTalkChoiceDBServer>();
            EventContentPermanents = new List<EventContentPermanentDBServer>();
            EventContentPermanents = new List<EventContentPermanentDBServer>();
            StickerBooks = new List<StickerBookDBServer>();
            ShopFreeRecruitHistories = new List<ShopFreeRecruitHistoryDBServer>();
            CraftInfos = new List<CraftInfoDBServer>();

            SingleRaidLobbyInfos = new List<SingleRaidLobbyInfoDBServer>();
            EliminateRaidLobbyInfos = new List<EliminateRaidLobbyInfoDBServer>();
            Raids = new List<RaidDBServer>();
            RaidBattles = new List<RaidBattleDBServer>();
            TimeAttackDungeonRooms = new List<TimeAttackDungeonRoomDBServer>();
            TimeAttackDungeonBattleHistories = new List<TimeAttackDungeonBattleHistoryDBServer>();
            MultiFloorRaids = new List<MultiFloorRaidDBServer>();
            WorldRaidLocalBosses = new List<WorldRaidLocalBossDBServer>();
            WorldRaidClearHistories = new List<WorldRaidClearHistoryDBServer>();

            BattleSummaries = new List<BattleSummaryDB>();
            RaidSummaries = new List<RaidSummaryDB>();
        }

        public AccountDBServer(long publisherId) : this()
        {
            PublisherAccountId = publisherId;
            State = AccountState.Normal;
            Level = 1;
            LastConnectTime = DateTime.Now;
            CreateDate = DateTime.Now;
            GameSettings = new();
            ContentInfo = new()
            {
                RaidDataInfo = new() { SeasonId = 1, BestRankingPoint = 0, TotalRankingPoint = 0 },
                TimeAttackDungeonDataInfo = new() { SeasonId = 1, SeasonBestRecord = 0 },
                EliminateRaidDataInfo = new() { SeasonId = 1, BestRankingPoint = 0, TotalRankingPoint = 0 },
                ArenaDataInfo = new() { SeasonId = 1 },
                MultiFloorRaidDataInfo = new() { SeasonId = 1 },
            };
            BattleSummaries = new List<BattleSummaryDB>();
            RaidSummaries = new List<RaidSummaryDB>();
        }

        [Key]
        public long ServerId { get; set; }

        public string Nickname { get; set; } = string.Empty;
        public string? CallName { get; set; }
        public string? DevId { get; set; }
        public long? PublisherAccountId { get; set; }
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
        public int? RetentionDays { get; set; }
        public int? VIPLevel { get; set; }
        public DateTime CreateDate { get; set; }
        public int? UnReadMailCount { get; set; }
        public DateTime? LinkRewardDate { get; set; }
    }

    public static class AccountDBServerExtensions
    {
        public static AccountDBServer GetAccount(this SchaleDataContext context, long accountId)
        {
            return context.Accounts.First(x => x.ServerId == accountId);
        }

        public static AccountDBServer? GetSingleAccount(this SchaleDataContext context, long accountId)
        {
            return context.Accounts.SingleOrDefault(x => x.ServerId == accountId);
        }

        public static AccountDBServer GetAccountByPublisherId(this SchaleDataContext context, long publisherId)
        {
            return context.Accounts.First(x => x.PublisherAccountId == publisherId);
        }

        public static AccountDBServer? GetSingleAccountByPublisherId(this SchaleDataContext context, long publisherId)
        {
            return context.Accounts.SingleOrDefault(x => x.PublisherAccountId == publisherId);
        }
    }
}


