using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Tracks player's campaign stage completion history
    /// </summary>
    public class CampaignStageHistory
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long StoryUniqueId { get; set; }
        public long ChapterUniqueId { get; set; }
        public long StageUniqueId { get; set; }
        public long TacticClearCountWithRankSRecord { get; set; }
        public long ClearTurnRecord { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public DateTime LastPlay { get; set; }
        public long TodayPlayCount { get; set; }
        public long TodayPurchasePlayCountHardStage { get; set; }
        public DateTime? FirstClearRewardReceive { get; set; }
        public DateTime? StarRewardReceive { get; set; }
        public long TodayPlayCountForUI { get; set; }
    }
}