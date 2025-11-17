using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class CampaignChapterClearRewardHistoryDBServer
    {
        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long ChapterUniqueId { get; set; }
        public StageDifficulty RewardType { get; set; }
        public DateTime ReceiveDate { get; set; }
    }

    public static class CampaignChapterClearRewardHistoryDBServerExtensions
    {
        public static IQueryable<CampaignChapterClearRewardHistoryDBServer> GetAccountCampaignChapterClearRewardHistories(this SchaleDataContext context, long accountId)
        {
            return context.CampaignChapterClearRewardHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}


