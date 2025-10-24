using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents equipment (gear pieces) in the player's inventory
    /// </summary>
    public class Equipment
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public long StackCount { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public int Tier { get; set; }
        public long BoundCharacterServerId { get; set; }
        public bool IsNew { get; set; }
        public bool IsLocked { get; set; }
    }
}