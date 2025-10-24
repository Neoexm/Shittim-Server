using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class MailDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public MailType Type { get; set; }
        public long UniqueId { get; set; }
        public string Sender { get; set; } = "Arona";
        public string Comment { get; set; } = string.Empty;
        public Dictionary<Language, string> LocalizedSender { get; set; } = Enum.GetValues<Language>().ToDictionary(lang => lang, _ => "Arona");
        public Dictionary<Language, string> LocalizedComment { get; set; } = Enum.GetValues<Language>().ToDictionary(lang => lang, _ => string.Empty);
        public DateTime SendDate { get; set; }
        public DateTime? ReceiptDate { get; set; }
        public DateTime? ExpireDate { get; set; }
        public List<ParcelInfo>? ParcelInfos { get; set; }
        public List<ParcelInfo>? RemainParcelInfos { get; set; }

        // Custom field
        public bool IsRefresher { get; set; } = false;
    }

    public static class MailDBServerExtensions
    {
        public static IQueryable<MailDBServer> GetAccountMails(this SCHALEContext context, long accountId)
        {
            return context.Mails.Where(x => x.AccountServerId == accountId);
        }
    }
}