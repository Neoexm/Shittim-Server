using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class ScenarioGroupHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long ScenarioGroupUqniueId { get; set; }
        public long ScenarioType { get; set; }
        public long? EventContentId { get; set; }
        public DateTime ClearDateTime { get; set; }
        public bool IsReturn { get; set; }
    }

    public static class ScenarioGroupHistoryDBServerExtensions
    {
        public static IQueryable<ScenarioGroupHistoryDBServer> GetAccountScenarioGroupHistories(this SchaleDataContext context, long accountId)
        {
            return context.ScenarioGroupHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}


