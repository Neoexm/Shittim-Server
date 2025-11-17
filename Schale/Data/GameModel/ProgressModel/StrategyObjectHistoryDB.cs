using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class StrategyObjectHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long StrategyObjectId { get; set; }
    }

    public static class StrategyObjectHistoryDBServerExtensions
    {
        public static IQueryable<StrategyObjectHistoryDBServer> GetAccountStrategyObjectHistories(this SchaleDataContext context, long accountId)
        {
            return context.StrategyObjectHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}


