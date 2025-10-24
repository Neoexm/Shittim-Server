using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.Data;

namespace Plana.Database.GameModel
{
    public class RaidDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public RaidMemberDescription? Owner { get; set; }
        public ContentType ContentType { get; set; }
        public long UniqueId { get; set; }
        public long SeasonId { get; set; }
        public DateTime Begin { get; set; }
        public DateTime End { get; set; }
        public long PlayerCount { get; set; }
        public string? BossGroup { get; set; }
        public Difficulty BossDifficulty { get; set; }
        public int LastBossIndex { get; set; }
        public List<int> Tags { get; set; } = new List<int>();
        public string SecretCode { get; set; } = string.Empty;
        public RaidStatus RaidState { get; set; }
        public bool IsPractice { get; set; }
        public List<RaidBossDBServer> RaidBossDBs { get; set; } = new();
        public Dictionary<long, List<long>> ParticipateCharacterServerIds { get; set; } = new Dictionary<long, List<long>>();
        public bool IsEnterRoom { get; set; }
        public long SessionHitPoint { get; set; }
        public long AccountLevelWhenCreateDB { get; set; }
        public bool ClanAssistUsed { get; set; }
    }

    public static class RaidDBServerExtensions
    {
        public static IQueryable<RaidDBServer> GetAccountRaids(this SCHALEContext context, long accountId)
        {
            return context.Raids.Where(x => x.AccountServerId == accountId);
        }
    }
}