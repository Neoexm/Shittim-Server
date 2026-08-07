using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class MomoTalkOutLineDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long CharacterDBId { get; set; }
        public long CharacterId { get; set; }
        public long LatestMessageGroupId { get; set; }

        // Set when a MomoTalk_Read stopped because the next group opens with a FavorRankUp gate the student has
        // not reached. LatestMessageGroupId alone cannot distinguish that state from "next unread group", and
        // the login sync must not advance an unread group - that would skip its story.
        [JsonIgnore]
        public long? PendingGateGroupId { get; set; }

        public long? ChosenMessageId { get; set; }
        public List<long> ScheduleIds { get; set; } = [];
        public DateTime LastUpdateDate { get; set; }
    }

    public static class MomoTalkOutLineDBServerExtensions
    {
        public static IQueryable<MomoTalkOutLineDBServer> GetAccountMomoTalkOutLines(this SchaleDataContext context, long accountId)
        {
            return context.MomoTalkOutLines.Where(x => x.AccountServerId == accountId);
        }
    }
}


