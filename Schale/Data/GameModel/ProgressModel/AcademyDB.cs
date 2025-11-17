using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Schale.Data.GameModel
{
    public class AcademyDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public DateTime LastUpdate { get; set; }
        public Dictionary<long, List<VisitingCharacterDBServer>>? ZoneVisitCharacterDBs { get; set; }
        public Dictionary<long, List<long>>? ZoneScheduleGroupRecords { get; set; }
    }

    public static class AcademyDBServerExtensions
    {
        public static IQueryable<AcademyDBServer> GetAccountAcademies(this SchaleDataContext context, long accountId)
        {
            return context.Academies.Where(x => x.AccountServerId == accountId);
        }
    }
}


