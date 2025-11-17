using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class CostumeDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.Costume;

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

        public long UniqueId { get; set; }
        public long BoundCharacterServerId { get; set; }
    }

    public static class CostumeDBServerExtensions
    {
        public static IQueryable<CostumeDBServer> GetAccountCostumes(this SchaleDataContext context, long accountId)
        {
            return context.Costumes.Where(x => x.AccountServerId == accountId);
        }
    }
}


