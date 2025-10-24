using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents an item in the player's inventory
    /// </summary>
    public class Item
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public long StackCount { get; set; }
        public bool IsNew { get; set; }
        public bool IsLocked { get; set; }
    }
}