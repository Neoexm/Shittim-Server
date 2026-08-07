using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class SkipHistoryDBServer
    {
        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public int Prologue { get; set; }
        public Dictionary<int, int>? Tutorial { get; set; }
    }

    public static class SkipHistoryDBServerExtensions
    {
        public static IQueryable<SkipHistoryDBServer> GetAccountSkipHistories(this SchaleDataContext context, long accountId)
        {
            return context.SkipHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}
