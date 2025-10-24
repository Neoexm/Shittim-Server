using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Plana.Database.GameModel
{
    public class WorldRaidClearHistoryDBServer
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
        public DateTime RewardReceiveDate { get; set; }
    }

    public static class WorldRaidClearHistoryDBServerExtensions
    {
        public static IQueryable<WorldRaidClearHistoryDBServer> GetAccountWorldRaidClearHistories(this SCHALEContext context, long accountId)
        {
            return context.WorldRaidClearHistories.Where(x => x.AccountServerId == accountId);
        }

        public static IQueryable<WorldRaidClearHistoryDBServer> GetWorldRaidClearHistoriesBySeasonId(this IQueryable<WorldRaidClearHistoryDBServer> worldRaidClearHistoryDBs, long seasonId)
        {
            return worldRaidClearHistoryDBs.Where(x => x.SeasonId == seasonId);
        }
    }
}