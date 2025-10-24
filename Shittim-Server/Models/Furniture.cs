using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents furniture in cafe or inventory
    /// </summary>
    public class Furniture
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public long UniqueId { get; set; }
        
        public long CafeDBId { get; set; }
        
        public int Location { get; set; } // FurnitureLocation enum
        
        public float PositionX { get; set; }
        
        public float PositionY { get; set; }
        
        public float Rotation { get; set; }
        
        public long ItemDeploySequence { get; set; }
        
        public int StackCount { get; set; }
    }
}