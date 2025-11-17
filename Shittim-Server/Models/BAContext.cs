using Microsoft.EntityFrameworkCore;
using Schale.Data.GameModel;

namespace BlueArchiveAPI.Models
{
    public class BAContext : DbContext
    {
        public BAContext(DbContextOptions<BAContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<CharacterDBServer> Characters { get; set; }
        public DbSet<WeaponDBServer> Weapons { get; set; }
        public DbSet<AccountCurrencyDBServer> AccountCurrencies { get; set; }
        public DbSet<ItemDBServer> Items { get; set; }
        public DbSet<EquipmentDBServer> Equipments { get; set; }
        public DbSet<CostumeDBServer> Costumes { get; set; }
        public DbSet<EchelonDBServer> Echelons { get; set; }
        public DbSet<MailDBServer> Mails { get; set; }
        public DbSet<MissionProgressDBServer> MissionProgresses { get; set; }
        public DbSet<ScenarioHistoryDBServer> ScenarioHistories { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.AccountServerId);
            });
            
            modelBuilder.Entity<CharacterDBServer>(entity =>
            {
                entity.HasKey(c => c.ServerId);
                entity.HasIndex(c => c.AccountServerId);
                entity.Ignore(c => c.PotentialStats);
                entity.Ignore(c => c.EquipmentSlotAndDBIds);
            });
            
            modelBuilder.Entity<WeaponDBServer>(entity =>
            {
                entity.HasKey(w => w.ServerId);
                entity.HasIndex(w => w.AccountServerId);
            });
            
            modelBuilder.Entity<AccountCurrencyDBServer>(entity =>
            {
                entity.HasKey(a => a.ServerId);
                entity.HasIndex(a => a.AccountServerId);
                entity.Ignore(a => a.CurrencyDict);
                entity.Ignore(a => a.UpdateTimeDict);
            });
            
            modelBuilder.Entity<ItemDBServer>(entity =>
            {
                entity.HasKey(i => i.ServerId);
                entity.HasIndex(i => i.AccountServerId);
            });
            
            modelBuilder.Entity<EquipmentDBServer>(entity =>
            {
                entity.HasKey(e => e.ServerId);
                entity.HasIndex(e => e.AccountServerId);
            });
            
            modelBuilder.Entity<CostumeDBServer>(entity =>
            {
                entity.HasKey(c => c.ServerId);
                entity.HasIndex(c => c.AccountServerId);
            });
            
            modelBuilder.Entity<EchelonDBServer>(entity =>
            {
                entity.HasKey(e => new { e.AccountServerId, e.EchelonNumber, e.EchelonType });
            });
            
            modelBuilder.Entity<MailDBServer>(entity =>
            {
                entity.HasKey(m => m.ServerId);
                entity.HasIndex(m => m.AccountServerId);
            });
            
            modelBuilder.Entity<MissionProgressDBServer>(entity =>
            {
                entity.HasKey(m => new { m.AccountServerId, m.MissionUniqueId });
            });
            
            modelBuilder.Entity<ScenarioHistoryDBServer>(entity =>
            {
                entity.HasKey(s => new { s.AccountServerId, s.ScenarioUniqueId });
            });
        }
    }
}