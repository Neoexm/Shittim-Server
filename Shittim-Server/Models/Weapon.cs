using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents a character's weapon in the database
    /// </summary>
    public class Weapon
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public long BoundCharacterServerId { get; set; }
        public bool IsLocked { get; set; }
    }
}