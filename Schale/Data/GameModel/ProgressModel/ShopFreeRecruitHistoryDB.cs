using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class ShopFreeRecruitHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long AccountId { get => AccountServerId; set => AccountServerId = value; }
        public long UniqueId { get; set; }
        public int RecruitCount { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }

    public static class ShopFreeRecruitHistoryDBServerExtensions
    {
        public static IQueryable<ShopFreeRecruitHistoryDBServer> GetAccountRecruitHistory(this SchaleDataContext context, long accountId)
        {
            return context.ShopFreeRecruitHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}
