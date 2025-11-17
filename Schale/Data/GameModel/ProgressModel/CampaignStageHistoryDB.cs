using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class CampaignStageHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long StoryUniqueId { get; set; }
        public long ChapterUniqueId { get; set; }
        public long StageUniqueId { get; set; }
        public long TacticClearCountWithRankSRecord { get; set; }
        public long ClearTurnRecord { get; set; }
        public long BestStarRecord { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public DateTime LastPlay { get; set; }
        public long TodayPlayCount { get; set; }
        public long TodayPurchasePlayCountHardStage { get; set; }
        public DateTime? FirstClearRewardReceive { get; set; }
        public DateTime? StarRewardReceive { get; set; }
        public bool IsClearedEver { get; set; }
        public long TodayPlayCountForUI { get; set; }

        public CampaignStageHistoryDBServer() { }

        public CampaignStageHistoryDBServer(long accountServerId, long stageId, long chapterId, DateTime dateTime)
        {
            AccountServerId = accountServerId;
            ChapterUniqueId = chapterId;
            StageUniqueId = stageId;
            TacticClearCountWithRankSRecord = 0;
            ClearTurnRecord = 1;
            Star1Flag = false;
            Star2Flag = false;
            Star3Flag = false;
            LastPlay = dateTime;
            TodayPlayCount = 1;
            FirstClearRewardReceive = dateTime;
            StarRewardReceive = dateTime;
        }
    }

    public static class CampaignStageHistoryDBServerExtensions
    {
        public static IQueryable<CampaignStageHistoryDBServer> GetAccountCampaignStageHistories(this SchaleDataContext context, long accountId)
        {
            return context.CampaignStageHistories.Where(x => x.AccountServerId == accountId);
        }

        public static IQueryable<CampaignStageHistoryDBServer> GetEventContentCampaignStageHistories(this IQueryable<CampaignStageHistoryDBServer> campaignStageHistories)
        {
            return campaignStageHistories.Where(x => x.ChapterUniqueId == 0);
        }

        public static IQueryable<CampaignStageHistoryDBServer> GetNormalCampaignStageHistories(this IQueryable<CampaignStageHistoryDBServer> campaignStageHistories)
        {
            return campaignStageHistories.Where(x => x.ChapterUniqueId != 0);
        }
    }
}


