using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    public class WorldRaidLocalBossDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long SeasonId { get; set; }
        public long GroupId { get; set; }
        public long UniqueId { get; set; }
        public bool IsScenario { get; set; }
        public bool IsCleardEver { get; set; }
        public long TacticMscSum { get; set; }
        public RaidBattleDBServer? RaidBattleDB { get; set; }
        public bool IsContinue { get; set; }
    }

    public static class WorldRaidLocalBossDBServerExtensions
    {
        public static IQueryable<WorldRaidLocalBossDBServer> GetAccountWorldRaidLocalBosses(this SchaleDataContext context, long accountId)
        {
            return context.WorldRaidLocalBosses.Where(x => x.AccountServerId == accountId);
        }

        public static IQueryable<WorldRaidLocalBossDBServer> GetWorldRaidLocalBossesBySeasonId(this IQueryable<WorldRaidLocalBossDBServer> worldRaidLocalBossDBs, long seasonId)
        {
            return worldRaidLocalBossDBs.Where(x => x.SeasonId == seasonId);
        }

        public static IQueryable<WorldRaidLocalBossDBServer> GetWorldRaidLocalBossesByGroupId(this IQueryable<WorldRaidLocalBossDBServer> worldRaidLocalBossDBs, long groupId)
        {
            return worldRaidLocalBossDBs.Where(x => x.GroupId == groupId);
        }
    }
}


