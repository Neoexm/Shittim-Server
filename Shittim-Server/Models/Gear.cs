using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents character gear (unique equipment)
    /// </summary>
    public class Gear
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public long UniqueId { get; set; }
        
        public int Level { get; set; }
        
        public long Exp { get; set; }
        
        public int Tier { get; set; }
        
        public long SlotIndex { get; set; }
        
        public long BoundCharacterServerId { get; set; }
        
        public bool IsLocked { get; set; }
    }
}