using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Plana.Database.GameModel
{
    public class AttendanceHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long AttendanceBookUniqueId { get; set; }
        public Dictionary<long, DateTime>? AttendedDay { get; set; }
        public bool Expired { get; set; }
        public long LastAttendedDay { get; set; }
        public DateTime LastAttendedDate { get; set; }
        public Dictionary<long, Nullable<DateTime>>? AttendedDayNullable { get; set; }
    }

    public static class AttendanceHistoryDBServerExtensions
    {
        public static IQueryable<AttendanceHistoryDBServer> GetAccountAttendanceHistories(this SCHALEContext context, long accountId)
        {
            return context.AttendanceHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}