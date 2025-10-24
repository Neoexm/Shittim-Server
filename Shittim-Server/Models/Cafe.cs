using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents a player's cafe
    /// </summary>
    public class Cafe
    {
        [Key]
        public long CafeDBId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public long CafeId { get; set; }
        
        public DateTime LastUpdate { get; set; }
        
        public DateTime? LastSummonDate { get; set; }
        
        public int CafeRank { get; set; }
        
        public bool IsNew { get; set; }
        
        // Stored as JSON: Dictionary<long, CafeVisitCharacter>
        // Key = CharacterUniqueId, Value = { UniqueId, ServerId, IsSummon, LastInteractTime }
        public string CafeVisitCharacterDBs { get; set; } = "{}";
        
        // Stored as JSON: Production data (currency generation)
        // { ComfortValue, LastUpdate, AccumulatedCurrency }
        public string ProductionData { get; set; } = "{}";
        
        /// <summary>
        /// Last time production was collected (for idle production calculation)
        /// </summary>
        public DateTime LastProductionCollectTime { get; set; } = DateTime.UtcNow;
    }
}