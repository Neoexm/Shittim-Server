using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class IdCardBackgroundDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.IdCardBackground;

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
        public static IQueryable<IdCardBackgroundDBServer> GetAccountIdCardBackgrounds(this SchaleDataContext context, long accountId)
        {
            return context.IdCardBackgrounds.Where(x => x.AccountServerId == accountId);
        }
    }
}


