using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class EventContentPermanentDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long EventContentId { get; set; }
        public bool IsStageAllClear { get; set; }
        public bool IsReceivedCharacterReward { get; set; }
    }

    public static class EventContentPermanentDBServerExtensions
    {
        public static IQueryable<EventContentPermanentDBServer> GetAccountEventContentPermanents(this SchaleDataContext context, long accountId)
        {
            return context.EventContentPermanents.Where(x => x.AccountServerId == accountId);
        }
    }
}


