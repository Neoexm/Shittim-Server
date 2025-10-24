using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class EventContentPermanent
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public bool IsStageAllClear { get; set; }
        public bool IsReceivedCharacterReward { get; set; }
    }
}