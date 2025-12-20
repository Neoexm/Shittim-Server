using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class MissionProgressDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long MissionUniqueId { get; set; }
        public bool Complete { get; set; }
        public DateTime StartTime { get; set; }
        public Dictionary<long, long>? ProgressParameters { get; set; }
    }

    public static class MissionProgressDBServerExtensions
    {
        public static IQueryable<MissionProgressDBServer> GetAccountMissionProgresses(this SchaleDataContext context, long accountId)
        {
            return context.MissionProgresses.Where(x => x.AccountServerId == accountId);
        }
    }
}


