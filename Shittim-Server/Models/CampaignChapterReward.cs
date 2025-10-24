using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Tracks chapter clear rewards that have been claimed
    /// </summary>
    public class CampaignChapterReward
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long ChapterUniqueId { get; set; }
        public int RewardType { get; set; } // Maps to StageDifficulty enum
        public DateTime ReceiveDate { get; set; }
    }
}