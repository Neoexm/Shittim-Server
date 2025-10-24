using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class AcademyLocation
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long LocationId { get; set; }
        public int Rank { get; set; }
        public long Exp { get; set; }
    }
}