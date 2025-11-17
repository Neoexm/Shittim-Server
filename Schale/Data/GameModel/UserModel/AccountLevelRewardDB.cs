using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class AccountLevelRewardDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long RewardId { get; set; }
    }

    public static class AccountLevelRewardDBServerExtensions
    {
        public static IQueryable<AccountLevelRewardDBServer> GetAccountLevelRewards(this SchaleDataContext context, long accountId)
        {
            return context.AccountLevelRewards.Where(x => x.AccountServerId == accountId);
        }
    }
}


