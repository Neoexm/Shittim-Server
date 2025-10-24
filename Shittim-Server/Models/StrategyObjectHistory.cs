using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Tracks collected strategy objects in campaign stages
    /// </summary>
    public class StrategyObjectHistory
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long StrategyObjectId { get; set; }
    }
}