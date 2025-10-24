using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class Academy
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public DateTime LastUpdate { get; set; }
        
        // JSON serialized
        public string? ZoneVisitCharacterIds { get; set; } // JSON: Dictionary<long, List<long>>
        public string? ZoneScheduleGroupRecords { get; set; } // JSON: Dictionary<long, long>
    }
}