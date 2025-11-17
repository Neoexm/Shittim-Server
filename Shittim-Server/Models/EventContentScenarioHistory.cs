using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class EventContentScenarioHistory
    {
        [Key]
        public long Id { get; set; }
        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long StageUniqueId { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
    }
}
