using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class AccountAttachment
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long AccountId { get; set; }
        public long EmblemUniqueId { get; set; }
    }
}