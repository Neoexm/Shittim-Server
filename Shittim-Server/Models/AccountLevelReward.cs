using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Tracks account level rewards that have been claimed
    /// </summary>
    public class AccountLevelReward
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        public long RewardId { get; set; }
        
        // Navigation property
        public User User { get; set; }
    }
}