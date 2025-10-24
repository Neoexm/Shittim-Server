using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents a saved echelon preset
    /// </summary>
    public class EchelonPreset
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public int GroupIndex { get; set; }
        
        public int Index { get; set; }
        
        public string Label { get; set; } = "";
        
        public int ExtensionType { get; set; }
        
        public long LeaderUniqueId { get; set; }
        
        public long TSSInteractionUniqueId { get; set; }
        
        // Stored as JSON arrays
        public string StrikerUniqueIds { get; set; } = "[]";
        
        public string SpecialUniqueIds { get; set; } = "[]";
        
        public string CombatStyleIndex { get; set; } = "[]";
        
        public string MulliganUniqueIds { get; set; } = "[]";
    }
}