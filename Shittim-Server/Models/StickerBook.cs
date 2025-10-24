using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class StickerBook
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        
        // JSON serialized
        public string? UnusedStickerIds { get; set; } // JSON: List<long>
        public string? UsedStickerIds { get; set; } // JSON: List<long>
    }
}