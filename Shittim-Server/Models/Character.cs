using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents a character owned by a player account
    /// </summary>
    public class Character
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public int StarGrade { get; set; } = 1;
        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int FavorRank { get; set; } = 1;
        public long FavorExp { get; set; } = 0;
        public int PublicSkillLevel { get; set; } = 1;
        public int ExSkillLevel { get; set; } = 1;
        public int PassiveSkillLevel { get; set; } = 1;
        public int ExtraPassiveSkillLevel { get; set; } = 1;
        public int LeaderSkillLevel { get; set; } = 1;
        public bool IsNew { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public bool IsFavorite { get; set; } = false;
        
        // Serialized as JSON in database
        public string EquipmentServerIds { get; set; } = "[0,0,0]";
        public string PotentialStats { get; set; } = "{\"1\":0,\"2\":0,\"3\":0}";
        
        // Navigation property
        public User User { get; set; }
    }
}