using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Tracks tutorial completion status for a player account
    /// </summary>
    public class AccountTutorial
    {
        [Key]
        public long AccountServerId { get; set; }
        
        // Serialized as JSON in database - contains list of tutorial IDs
        public string TutorialIds { get; set; } = "[]";
        
        // Navigation property
        public User User { get; set; }
    }
}