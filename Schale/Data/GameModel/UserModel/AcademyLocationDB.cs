using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class AcademyLocationDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long LocationId { get; set; }
        public long Rank { get; set; }
        public long Exp { get; set; }
    }

    public static class AcademyLocationDBServerExtensions
    {
        public static IQueryable<AcademyLocationDBServer> GetAccountAcademyLocations(this SchaleDataContext context, long accountId)
        {
            return context.AcademyLocations.Where(x => x.AccountServerId == accountId);
        }
    }
}


