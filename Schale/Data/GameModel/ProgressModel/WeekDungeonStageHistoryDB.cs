using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class WeekDungeonStageHistoryDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long StageUniqueId { get; set; }
        public Dictionary<StarGoalType, long> StarGoalRecord { get; set; } = [];
        public bool IsCleardEver { get; set; }

        public WeekDungeonStageHistoryDBServer() { }

        public WeekDungeonStageHistoryDBServer(long accountId, WeekDungeonExcelT excel)
        {
            AccountServerId = accountId;
            StageUniqueId = excel.StageId;
            StarGoalRecord = excel.StarGoal.ToDictionary(x => x, x => 0L);
            IsCleardEver = false;
        }
    }

    public static class WeekDungeonStageHistoryDBServerExtension
    {
        public static IQueryable<WeekDungeonStageHistoryDBServer> GetAccountWeekDungeonStageHistories(this SchaleDataContext context, long accountId)
        {
            return context.WeekDungeonStageHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}


