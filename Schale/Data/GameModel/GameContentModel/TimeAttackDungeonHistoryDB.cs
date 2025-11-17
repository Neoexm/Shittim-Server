using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class TimeAttackDungeonBattleHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [JsonIgnore]
        [Key]
        public long ServerId { get; set; }

        public TimeAttackDungeonType DungeonType { get; set; }
        public long GeasId { get; set; }
        public long DefaultPoint { get; set; }
        public long ClearTimePoint { get; set; }
        public long EndFrame { get; set; }
        public long TotalPoint { get; set; }
        public List<TimeAttackDungeonCharacterDBServer>? MainCharacterDBs { get; set; }
        public List<TimeAttackDungeonCharacterDBServer>? SupportCharacterDBs { get; set; }
    }

    public static class TimeAttackDungeonBattleHistoryDBServerExtensions
    {
        public static IQueryable<TimeAttackDungeonBattleHistoryDBServer> GetAccountTimeAttackDungeonBattleHistoryDBs(this SchaleDataContext context, long accountId)
        {
            return context.TimeAttackDungeonBattleHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}


