using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class MomoTalkOutline
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long CharacterId { get; set; }
        public long LatestMessageId { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public bool IsNew { get; set; }
        public int FavorLevel { get; set; }
    }
}