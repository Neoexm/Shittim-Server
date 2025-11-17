using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class MomoTalkChoiceDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long CharacterDBId { get; set; }
        public long MessageGroupId { get; set; }
        public long? ChosenMessageId { get; set; }
        public DateTime ChosenDate { get; set; }
    }

    public static class MomoTalkChoiceDBServerExtensions
    {
        public static IQueryable<MomoTalkChoiceDBServer> GetAccountMomoTalkChoices(this SchaleDataContext context, long accountId)
        {
            return context.MomoTalkChoices.Where(x => x.AccountServerId == accountId);
        }
    }
}


