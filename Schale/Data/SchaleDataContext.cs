using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Text.Json.Serialization;
using Schale.Data.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Schale.Data.GameModel;
using System.Collections.Concurrent;

namespace Schale.Data
{
    public class SchaleDataContext : DbContext
    {
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<AccountTutorial> AccountTutorials { get; set; }
        public DbSet<AccountDBServer> Accounts { get; set; }

        public DbSet<AccountCurrencyDBServer> Currencies { get; set; }
        public DbSet<ItemDBServer> Items { get; set; }
        public DbSet<CharacterDBServer> Characters { get; set; }
        public DbSet<EquipmentDBServer> Equipments { get; set; }
        public DbSet<WeaponDBServer> Weapons { get; set; }
        public DbSet<GearDBServer> Gears { get; set; }
        public DbSet<EchelonDBServer> Echelons { get; set; }
        public DbSet<EchelonPresetDBServer> EchelonPresets { get; set; }
        public DbSet<EchelonPresetGroupDBServer> EchelonPresetGroups { get; set; }
        public DbSet<CafeDBServer> Cafes { get; set; }
        public DbSet<FurnitureDBServer> Furnitures { get; set; }
        public DbSet<MemoryLobbyDBServer> MemoryLobbies { get; set; }
        public DbSet<MailDBServer> Mails { get; set; }
        public DbSet<EmblemDBServer> Emblems { get; set; }
        public DbSet<IdCardBackgroundDBServer> IdCardBackgrounds { get; set; }
        public DbSet<CostumeDBServer> Costumes { get; set; }
        public DbSet<StickerDBServer> Stickers { get; set; }
        public DbSet<AccountAttachmentDBServer> AccountAttachments { get; set; }
        public DbSet<AccountLevelRewardDBServer> AccountLevelRewards { get; set; }

        public DbSet<MissionProgressDBServer> MissionProgresses { get; set; }
        public DbSet<AttendanceHistoryDBServer> AttendanceHistories { get; set; }
        public DbSet<AcademyDBServer> Academies { get; set; }
        public DbSet<AcademyLocationDBServer> AcademyLocations { get; set; }
        public DbSet<CampaignMainStageSaveDBServer> CampaignMainStageSaves { get; set; }
        public DbSet<CampaignChapterClearRewardHistoryDBServer> CampaignChapterClearRewardHistories { get; set; }
        public DbSet<StrategyObjectHistoryDBServer> StrategyObjectHistories { get; set; }
        public DbSet<ScenarioHistoryDBServer> ScenarioHistories { get; set; }
        public DbSet<ScenarioGroupHistoryDBServer> ScenarioGroupHistories { get; set; }
        public DbSet<CampaignStageHistoryDBServer> CampaignStageHistories { get; set; }
        public DbSet<WeekDungeonStageHistoryDBServer> WeekDungeonStageHistories { get; set; }
        public DbSet<SchoolDungeonStageHistoryDBServer> SchoolDungeonStageHistories { get; set; }
        public DbSet<MomoTalkOutLineDBServer> MomoTalkOutLines { get; set; }
        public DbSet<MomoTalkChoiceDBServer> MomoTalkChoices { get; set; }
        public DbSet<EventContentPermanentDBServer> EventContentPermanents { get; set; }
        public DbSet<StickerBookDBServer> StickerBooks { get; set; }
        public DbSet<ShopFreeRecruitHistoryDBServer> ShopFreeRecruitHistories { get; set; }
        public DbSet<CraftInfoDBServer> CraftInfos { get; set; }

        public DbSet<SingleRaidLobbyInfoDBServer> SingleRaidLobbyInfos { get; set; }
        public DbSet<EliminateRaidLobbyInfoDBServer> EliminateRaidLobbyInfos { get; set; }
        public DbSet<RaidDBServer> Raids { get; set; }
        public DbSet<RaidBattleDBServer> RaidBattles { get; set; }
        public DbSet<TimeAttackDungeonRoomDBServer> TimeAttackDungeonRooms { get; set; }
        public DbSet<TimeAttackDungeonBattleHistoryDBServer> TimeAttackDungeonBattleHistories { get; set; }
        public DbSet<MultiFloorRaidDBServer> MultiFloorRaids { get; set; }
        public DbSet<WorldRaidLocalBossDBServer> WorldRaidLocalBosses { get; set; }
        public DbSet<WorldRaidBossListInfoDBServer> WorldRaidBossListInfos { get; set; }
        public DbSet<WorldRaidClearHistoryDBServer> WorldRaidClearHistories { get; set; }

        public DbSet<BattleSummaryDB> BattleSummaries { get; set; }
        public DbSet<RaidSummaryDB> RaidSummaries { get; set; }

        public SchaleDataContext() { }

        public SchaleDataContext(DbContextOptions options) : base(options) { }

        public static SchaleDataContext Create(string connectionString) =>
            new(new DbContextOptionsBuilder<SchaleDataContext>()
                .UseSqlServer(connectionString)
                .Options);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureUserAccountModels(modelBuilder);
            ConfigureAccountRelationships(modelBuilder);
            ConfigureUserDataModels(modelBuilder);
            ConfigureProgressModels(modelBuilder);
            ConfigureContentModels(modelBuilder);
            ConfigureBattleModels(modelBuilder);
        }

        private void ConfigureUserAccountModels(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountTutorial>().Property(x => x.AccountServerId).ValueGeneratedNever();
            modelBuilder.Entity<UserAccount>().Property(x => x.Uid).ValueGeneratedOnAdd();

            modelBuilder.Entity<AccountDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<AccountDBServer>().Property(x => x.ContentInfo).HasJsonConversion();
            modelBuilder.Entity<AccountDBServer>().Property(x => x.GameSettings).HasJsonConversion();
        }

        private void ConfigureAccountRelationships(ModelBuilder modelBuilder)
        {
            var accountEntity = modelBuilder.Entity<AccountDBServer>();

            accountEntity.HasMany(x => x.Currencies).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Items).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Characters).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Equipments).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Weapons).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Gears).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Echelons).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.EchelonPresets).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.EchelonPresetGroups).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Cafes).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Furnitures).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.MemoryLobbies).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Mails).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Emblems).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.IdCardBackgrounds).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Costumes).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Stickers).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.AccountAttachments).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.AccountLevelRewards).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            
            accountEntity.HasMany(x => x.MissionProgresses).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.AttendanceHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Academies).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.AcademyLocations).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.CampaignMainStageSaves).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.CampaignChapterClearRewardHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.StrategyObjectHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.ScenarioHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.ScenarioGroupHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.CampaignStageHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.WeekDungeonStageHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.SchoolDungeonStageHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.MomoTalkOutLines).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.MomoTalkChoices).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.EventContentPermanents).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.StickerBooks).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.ShopFreeRecruitHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.CraftInfos).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            
            accountEntity.HasMany(x => x.SingleRaidLobbyInfos).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.EliminateRaidLobbyInfos).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.Raids).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.RaidBattles).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.TimeAttackDungeonRooms).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.TimeAttackDungeonBattleHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.MultiFloorRaids).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.WorldRaidLocalBosses).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.WorldRaidClearHistories).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();

            accountEntity.HasMany(x => x.BattleSummaries).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
            accountEntity.HasMany(x => x.RaidSummaries).WithOne(x => x.Account).HasForeignKey(x => x.AccountServerId).IsRequired();
        }

        private void ConfigureUserDataModels(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountCurrencyDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<AccountCurrencyDBServer>().Property(x => x.CurrencyDict).HasJsonConversion();
            modelBuilder.Entity<AccountCurrencyDBServer>().Property(x => x.UpdateTimeDict).HasJsonConversion();
            
            modelBuilder.Entity<ItemDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<CharacterDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<CharacterDBServer>().Property(x => x.EquipmentSlotAndDBIds).HasJsonConversion();
            modelBuilder.Entity<CharacterDBServer>().Property(x => x.PotentialStats).HasJsonConversion();

            modelBuilder.Entity<EquipmentDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<WeaponDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<GearDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();

            modelBuilder.Entity<EchelonDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<EchelonPresetDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<EchelonPresetGroupDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<EchelonPresetGroupDBServer>().Property(x => x.PresetDBs).HasJsonConversion();
            modelBuilder.Entity<EchelonPresetGroupDBServer>().Property(x => x.Item).HasJsonConversion();

            modelBuilder.Entity<CafeDBServer>().Property(x => x.CafeDBId).ValueGeneratedOnAdd();
            modelBuilder.Entity<CafeDBServer>().Property(x => x.CafeVisitCharacterDBs).HasJsonConversion();
            modelBuilder.Entity<CafeDBServer>().Property(x => x.FurnitureDBs).HasJsonConversion();
            modelBuilder.Entity<CafeDBServer>().Property(x => x.ProductionDB).HasJsonConversion();

            modelBuilder.Entity<FurnitureDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<MemoryLobbyDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();

            modelBuilder.Entity<MailDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<MailDBServer>().Property(x => x.ParcelInfos).HasJsonConversion();
            modelBuilder.Entity<MailDBServer>().Property(x => x.LocalizedComment).HasJsonConversion();
            modelBuilder.Entity<MailDBServer>().Property(x => x.LocalizedSender).HasJsonConversion();
            modelBuilder.Entity<MailDBServer>().Property(x => x.RemainParcelInfos).HasJsonConversion();

            modelBuilder.Entity<EmblemDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<IdCardBackgroundDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<CostumeDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<StickerDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<AccountAttachmentDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<AccountLevelRewardDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
        }

        private void ConfigureProgressModels(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissionProgressDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<MissionProgressDBServer>().Property(x => x.ProgressParameters).HasJsonConversion();

            modelBuilder.Entity<AttendanceHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<AttendanceHistoryDBServer>().Property(x => x.AttendedDay).HasJsonConversion();
            modelBuilder.Entity<AttendanceHistoryDBServer>().Property(x => x.AttendedDayNullable).HasJsonConversion();

            modelBuilder.Entity<AcademyDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<AcademyDBServer>().Property(x => x.ZoneVisitCharacterDBs).HasJsonConversion();
            modelBuilder.Entity<AcademyDBServer>().Property(x => x.ZoneScheduleGroupRecords).HasJsonConversion();

            modelBuilder.Entity<AcademyLocationDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.EnemyInfos).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.EchelonInfos).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.WithdrawInfos).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.StrategyObjects).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.StrategyObjectRewards).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.StrategyObjectHistory).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.ActivatedHexaEventsAndConditions).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.HexaEventDelayedExecutions).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.TileMapStates).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.DisplayInfos).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.DeployedEchelonInfos).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.StageEntranceFee).HasJsonConversion();
            modelBuilder.Entity<CampaignMainStageSaveDBServer>().Property(x => x.EnemyKillCountByUniqueId).HasJsonConversion();

            modelBuilder.Entity<ScenarioGroupHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<CampaignStageHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<WeekDungeonStageHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<WeekDungeonStageHistoryDBServer>().Property(x => x.StarGoalRecord).HasJsonConversion();
            
            modelBuilder.Entity<SchoolDungeonStageHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<MomoTalkOutLineDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<MomoTalkOutLineDBServer>().Property(x => x.ScheduleIds).HasJsonConversion();
            
            modelBuilder.Entity<MomoTalkChoiceDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<EventContentPermanentDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<StickerBookDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<StickerBookDBServer>().Property(x => x.UnusedStickerDBs).HasJsonConversion();
            modelBuilder.Entity<StickerBookDBServer>().Property(x => x.UsedStickerDBs).HasJsonConversion();
            
            modelBuilder.Entity<ShopFreeRecruitHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            
            modelBuilder.Entity<CraftInfoDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<CraftInfoDBServer>().Property(x => x.Nodes).HasJsonConversion();
            modelBuilder.Entity<CraftInfoDBServer>().Property(x => x.ResultIds).HasJsonConversion();
            modelBuilder.Entity<CraftInfoDBServer>().Property(x => x.RewardParcelInfos).HasJsonConversion();
        }

        private void ConfigureContentModels(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.ClearDifficulty).HasJsonConversion();
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.PlayingRaidDB).HasJsonConversion();
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.PlayableHighestDifficulty).HasJsonConversion();
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.SweepPointByRaidUniqueId).HasJsonConversion();
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.ClanAssistUseInfo).HasJsonConversion();
            modelBuilder.Entity<SingleRaidLobbyInfoDBServer>().Property(x => x.RemainFailCompensation).HasJsonConversion();

            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.OpenedBossGroups).HasJsonConversion();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.BestRankingPointPerBossGroup).HasJsonConversion();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.PlayingRaidDB).HasJsonConversion();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.PlayableHighestDifficulty).HasJsonConversion();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.SweepPointByRaidUniqueId).HasJsonConversion();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.ClanAssistUseInfo).HasJsonConversion();
            modelBuilder.Entity<EliminateRaidLobbyInfoDBServer>().Property(x => x.RemainFailCompensation).HasJsonConversion();

            modelBuilder.Entity<RaidDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<RaidDBServer>().Property(x => x.Owner).HasJsonConversion();
            modelBuilder.Entity<RaidDBServer>().Property(x => x.RaidBossDBs).HasJsonConversion();
            modelBuilder.Entity<RaidDBServer>().Property(x => x.ParticipateCharacterServerIds).HasJsonConversion();

            modelBuilder.Entity<RaidBattleDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<RaidBattleDBServer>().Property(x => x.RaidMembers).HasJsonConversion();

            modelBuilder.Entity<TimeAttackDungeonRoomDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<TimeAttackDungeonRoomDBServer>().Property(x => x.SweepHistoryDates).HasJsonConversion();
            modelBuilder.Entity<TimeAttackDungeonRoomDBServer>().Property(x => x.BattleHistoryDBs).HasJsonConversion();
            
            modelBuilder.Entity<TimeAttackDungeonBattleHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<TimeAttackDungeonBattleHistoryDBServer>().Property(x => x.MainCharacterDBs).HasJsonConversion();
            modelBuilder.Entity<TimeAttackDungeonBattleHistoryDBServer>().Property(x => x.SupportCharacterDBs).HasJsonConversion();
            
            modelBuilder.Entity<MultiFloorRaidDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<MultiFloorRaidDBServer>().Property(x => x.TotalReceivableRewards).HasJsonConversion();
            modelBuilder.Entity<MultiFloorRaidDBServer>().Property(x => x.TotalReceivedRewards).HasJsonConversion();

            modelBuilder.Entity<WorldRaidLocalBossDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<WorldRaidLocalBossDBServer>().Property(x => x.RaidBattleDB).HasJsonConversion();

            modelBuilder.Entity<WorldRaidBossListInfoDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<WorldRaidBossListInfoDBServer>().OwnsOne(x => x.WorldBossDB);
            modelBuilder.Entity<WorldRaidBossListInfoDBServer>().Property(x => x.LocalBossDBs).HasJsonConversion();

            modelBuilder.Entity<WorldRaidClearHistoryDBServer>().Property(x => x.ServerId).ValueGeneratedOnAdd();
        }

        private void ConfigureBattleModels(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BattleSummaryDB>().Property(x => x.BossDatas).HasJsonConversion();
            modelBuilder.Entity<BattleSummaryDB>().Property(x => x.Characters).HasJsonConversion();
            modelBuilder.Entity<BattleSummaryDB>().Property(x => x.RaidMembers).HasJsonConversion();
            
            modelBuilder.Entity<RaidSummaryDB>().Property(x => x.ServerId).ValueGeneratedOnAdd();
            modelBuilder.Entity<RaidSummaryDB>().Property(x => x.BattleSummaryIds).HasJsonConversion();
            modelBuilder.Entity<RaidSummaryDB>().Property(x => x.BattleSnapshotDatas).HasJsonConversion();
        }
    }

    public class SchaleSqliteContext : SchaleDataContext
    {
        public SchaleSqliteContext() { }

        public SchaleSqliteContext(DbContextOptions options) : base(options) {}

        public new static SchaleSqliteContext Create(string connectionString)
        {
            return new SchaleSqliteContext(new DbContextOptionsBuilder<SchaleSqliteContext>()
                .UseSqlite(connectionString).Options);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=schale.sqlite3");
        }
    }

    public static class PropertyBuilderExtensions
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        private static readonly ConcurrentDictionary<Type, ValueConverter> _converters = new();
        private static readonly ConcurrentDictionary<Type, ValueComparer> _comparers = new();

        public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> propertyBuilder)
            where T : class?, new()
        {
            var converter = (ValueConverter<T, string>)_converters.GetOrAdd(
                typeof(T),
                _ => new ValueConverter<T, string>(
                    v => JsonSerializer.Serialize(v, _options),
                    v => JsonSerializer.Deserialize<T>(v, _options) ?? new T()
                )
            );

            var comparer = (ValueComparer<T>)_comparers.GetOrAdd(
                typeof(T),
                _ => new ValueComparer<T>(
                    (c1, c2) => JsonSerializer.Serialize(c1, _options) == JsonSerializer.Serialize(c2, _options),
                    c => c == null ? 0 : JsonSerializer.Serialize(c, _options).GetHashCode(),
                    c => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(c, _options), _options)!
                )
            );

            propertyBuilder.HasConversion(converter);
            propertyBuilder.Metadata.SetValueConverter(converter);
            propertyBuilder.Metadata.SetValueComparer(comparer);

            return propertyBuilder;
        }
    }
}


