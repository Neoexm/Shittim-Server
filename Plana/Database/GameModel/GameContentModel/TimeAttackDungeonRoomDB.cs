using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;

namespace Plana.Database.GameModel
{
    public class TimeAttackDungeonRoomDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [JsonIgnore]
        [Key]
        public long ServerId { get; set; }

        public long AccountId { get => AccountServerId; set => AccountServerId = value; }
        public long SeasonId { get; set; }
        public long RoomId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime RewardDate { get; set; }
        public bool IsPractice { get; set; }
        public List<DateTime>? SweepHistoryDates { get; set; }
        public List<TimeAttackDungeonBattleHistoryDBServer>? BattleHistoryDBs { get; set; }
        public int PlayCount { get; set; }
        public long TotalPointSum { get; set; }
        public bool IsRewardReceived { get; set; }
        public bool IsOpened { get; set; }
        public bool CanUseAssist { get; set; }
        public bool IsPlayCountOver { get; set; }
    }

    public static class TimeAttackDungeonRoomDBServerExtensions
    {
        public static IQueryable<TimeAttackDungeonRoomDBServer> GetAccountTimeAttackDungeonRooms(this SCHALEContext context, long accountId)
        {
            return context.TimeAttackDungeonRooms.Where(x => x.AccountServerId == accountId);
        }
    }
}