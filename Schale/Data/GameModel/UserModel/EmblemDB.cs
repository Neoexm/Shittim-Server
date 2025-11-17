using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class EmblemDBServer : ParcelBase
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public override ParcelType Type => ParcelType.Emblem;

        public long UniqueId { get; set; }
        public DateTime ReceiveDate { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public static class EmblemDBServerExtensions
    {
        public static IQueryable<EmblemDBServer> GetAccountEmblems(this SchaleDataContext context, long accountId)
        {
            return context.Emblems.Where(x => x.AccountServerId == accountId);
        }

        public static List<EmblemDBServer> AddEmblems(this SchaleDataContext context, long accountId, params EmblemDBServer[] emblems)
        {
            if (emblems == null || emblems.Length == 0)
                return [];

            foreach (var emblem in emblems)
            {
                emblem.AccountServerId = accountId;
                context.Emblems.Add(emblem);
            }

            return [.. emblems];
        }
    }
}


