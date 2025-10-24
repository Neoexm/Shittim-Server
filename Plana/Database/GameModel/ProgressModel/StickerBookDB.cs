using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Plana.Database.GameModel
{
    public class StickerBookDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long AccountId { get => AccountServerId; set => AccountServerId = value; }
        public List<StickerDBServer>? UnusedStickerDBs { get; set; }
        public List<StickerDBServer>? UsedStickerDBs { get; set; }
    }

    public static class StickerBookDBServerExtensions
    {
        public static IQueryable<StickerBookDBServer> GetAccountStickerBooks(this SCHALEContext context, long accountId)
        {
            return context.StickerBooks.Where(x => x.AccountServerId == accountId);
        }
    }
}