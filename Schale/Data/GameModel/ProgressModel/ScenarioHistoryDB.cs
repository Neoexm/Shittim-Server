using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class ScenarioHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long ScenarioUniqueId { get; set; }
        public DateTime ClearDateTime { get; set; }
    }

    public static class ScenarioHistoryDBServerExtension
    {
        public static IQueryable<ScenarioHistoryDBServer> GetAccountScenarioHistories(this SchaleDataContext context, long accountId)
        {
            return context.ScenarioHistories.Where(x => x.AccountServerId == accountId);
        }

        public static List<ScenarioHistoryDBServer> AddScenarios(this SchaleDataContext context, long accountId, params ScenarioHistoryDBServer[] scenarios)
        {
            if (scenarios == null || scenarios.Length == 0)
                return [];

            foreach (var scenario in scenarios)
            {
                scenario.AccountServerId = accountId;
                context.ScenarioHistories.Add(scenario);
            }

            return [.. scenarios];
        }
    }
}


