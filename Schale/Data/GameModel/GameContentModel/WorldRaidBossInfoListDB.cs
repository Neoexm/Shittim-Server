using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class WorldRaidBossListInfoDBServer
    {
        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long GroupId { get; set; }
        public WorldRaidWorldBossDBServer? WorldBossDB { get; set; }

        [NotMapped]
        public List<WorldRaidLocalBossDBServer> LocalBossDBs { get; set; } = [];

        public WorldRaidBossListInfoDBServer() { }

        public WorldRaidBossListInfoDBServer(long groupId, WorldRaidWorldBossDBServer worldBossDB)
        {
            GroupId = groupId;
            WorldBossDB = worldBossDB;
        }
    }

    public static class WorldRaidBossListInfoDBServerExtensions
    {
        public static WorldRaidBossListInfoDBServer? GetWorldRaidBossListByGroupId(this IQueryable<WorldRaidBossListInfoDBServer> worldRaidBossListInfoDBs, long groupId)
        {
            return worldRaidBossListInfoDBs.FirstOrDefault(x => x.GroupId == groupId);
        }
    }
}


