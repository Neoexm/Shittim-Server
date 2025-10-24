using Microsoft.EntityFrameworkCore;

namespace BlueArchiveAPI.Models
{
    public class BAContext : DbContext
    {
        public BAContext(DbContextOptions<BAContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Character> Characters { get; set; }
        public DbSet<Weapon> Weapons { get; set; }
        public DbSet<AccountCurrency> AccountCurrencies { get; set; }
        public DbSet<AccountTutorial> AccountTutorials { get; set; }
        public DbSet<AccountLevelReward> AccountLevelRewards { get; set; }
        public DbSet<CampaignStageHistory> CampaignStageHistories { get; set; }
        public DbSet<CampaignChapterReward> CampaignChapterRewards { get; set; }
        public DbSet<StrategyObjectHistory> StrategyObjectHistories { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<Gear> Gears { get; set; }
        public DbSet<Echelon> Echelons { get; set; }
        public DbSet<EchelonPreset> EchelonPresets { get; set; }
        public DbSet<EchelonPresetGroup> EchelonPresetGroups { get; set; }
        public DbSet<Cafe> Cafes { get; set; }
        public DbSet<Furniture> Furnitures { get; set; }
        public DbSet<Mail> Mails { get; set; }
        public DbSet<AcademyLocation> AcademyLocations { get; set; }
        public DbSet<Academy> Academies { get; set; }
        public DbSet<AccountAttachment> AccountAttachments { get; set; }
        public DbSet<StickerBook> StickerBooks { get; set; }
        public DbSet<EventContentPermanent> EventContentPermanents { get; set; }
        public DbSet<MissionProgress> MissionProgresses { get; set; }
        public DbSet<ScenarioHistory> ScenarioHistories { get; set; }
        public DbSet<ScenarioGroupHistory> ScenarioGroupHistories { get; set; }
        public DbSet<MomoTalkOutline> MomoTalkOutlines { get; set; }
        public DbSet<Costume> Costumes { get; set; }
        public DbSet<MemoryLobby> MemoryLobbies { get; set; }
        public DbSet<Emblem> Emblems { get; set; }
        public DbSet<MultiFloorRaid> MultiFloorRaids { get; set; }
        public DbSet<TimeAttackDungeonRoom> TimeAttackDungeonRooms { get; set; }
        public DbSet<CurrencyTransaction> CurrencyTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<User>()
                .OwnsOne(u => u.GameSettings, gs =>
                {
                    gs.Property<int>("Id");
                });
            
            modelBuilder.Entity<User>()
                .OwnsOne(u => u.ContentInfo, ci =>
                {
                    ci.Property<int>("Id");
                    ci.OwnsOne(c => c.ArenaDataInfo, ad =>
                    {
                        ad.Property(a => a.SeasonId).IsRequired();
                    });
                    ci.OwnsOne(c => c.MultiFloorRaidDataInfo, mf =>
                    {
                        mf.Property(m => m.SeasonId).IsRequired();
                    });
                });
            
            modelBuilder.Entity<ScenarioHistory>()
                .HasKey(s => new { s.AccountServerId, s.ScenarioId });
            
            modelBuilder.Entity<ScenarioGroupHistory>()
                .HasKey(s => new { s.AccountServerId, s.ScenarioGroupId });
            
            // Configure keys for new entities added for Atrahasis LoginSync port
            modelBuilder.Entity<Costume>()
                .HasKey(c => c.ServerId);
            
            modelBuilder.Entity<MemoryLobby>()
                .HasKey(m => m.ServerId);
            
            modelBuilder.Entity<Emblem>()
                .HasKey(e => e.ServerId);
            
            modelBuilder.Entity<MultiFloorRaid>()
                .HasKey(m => m.ServerId);
            
            modelBuilder.Entity<TimeAttackDungeonRoom>()
                .HasKey(t => t.ServerId);
        }
    }
}
