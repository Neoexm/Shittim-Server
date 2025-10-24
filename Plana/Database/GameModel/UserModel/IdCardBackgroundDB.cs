using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class IdCardBackgroundDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type { get => ParcelType.IdCardBackground; }

        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        [Key]
        public long ServerId { get; set; }

        public long UniqueId { get; set; }
    }

    public static class IdCardBackgroundDBServerExtensions
    {
        public static IQueryable<IdCardBackgroundDBServer> GetAccountIdCardBackgrounds(this SCHALEContext context, long accountId)
        {
            return context.IdCardBackgrounds.Where(x => x.AccountServerId == accountId);
        }
    }
}