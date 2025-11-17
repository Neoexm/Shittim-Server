using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class StickerDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.Sticker;

        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long StickerUniqueId { get; set; }
    }

    public static class StickerDBServerExtensions
    {
        public static IQueryable<StickerDBServer> GetAccountStickers(this SchaleDataContext context, long accountId)
        {
            return context.Stickers.Where(x => x.AccountServerId == accountId);
        }
    }
}


