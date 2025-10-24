using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class MissionProgress
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long MissionId { get; set; }
        public long ProgressCount { get; set; }
        public bool Complete { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? ExpiryTime { get; set; }
    }
}