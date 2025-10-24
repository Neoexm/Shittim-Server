using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents a team/echelon configuration
    /// </summary>
    public class Echelon
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public int EchelonType { get; set; }
        
        public long EchelonNumber { get; set; }
        
        public long LeaderServerId { get; set; }
        
        // Stored as JSON arrays
        public string MainSlotServerIds { get; set; } = "[]";
        
        public string SupportSlotServerIds { get; set; } = "[]";
        
        public long TSSServerId { get; set; }
        
        public int UsingFlag { get; set; }
        
        public string SkillCardMulliganCharacterIds { get; set; } = "[]";
        
        // Combat style index for each character slot
        public string CombatStyleIndex { get; set; } = "[]";
    }
}