using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class Mail
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public int Type { get; set; } // MailType enum
        public long UniqueId { get; set; }
        public string Sender { get; set; } = "";
        public string Comment { get; set; } = "";
        public DateTime SendDate { get; set; }
        public DateTime ExpireDate { get; set; }
        public bool IsRead { get; set; }
        public bool IsReceived { get; set; }
        
        // JSON serialized
        public string? ParcelInfos { get; set; } // JSON: List<ParcelInfo>
        public string? RemainParcelInfos { get; set; } // JSON: List<ParcelInfo>
    }
}