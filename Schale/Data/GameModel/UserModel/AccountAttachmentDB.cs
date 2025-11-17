using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class AccountAttachmentDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long AccountId { get => AccountServerId; set => AccountServerId = value; }
        public long EmblemUniqueId { get; set; }
    }

    public static class AccountAttachmentDBServerExtensions
    {
        public static IQueryable<AccountAttachmentDBServer> GetAccountAttachments(this SchaleDataContext context, long accountId)
        {
            return context.AccountAttachments.Where(x => x.AccountServerId == accountId);
        }
    }
}


