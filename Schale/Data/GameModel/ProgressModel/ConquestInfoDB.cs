using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    // One row per (account, conquest event). The JSON columns hold wire-shaped types, so responses serve
    // them without a mapping step.
    public class ConquestInfoDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        public long EventContentId { get; set; }
        public int EventGauge { get; set; }
        public int EventSpawnCount { get; set; }
        public int EchelonChangeCount { get; set; }
        public int TodayConquestRentCount { get; set; }
        public int TodayOperationRentCount { get; set; }
        public long CumulatedConditionValue { get; set; }
        public long ReceivedCalculateRewardConditionAmount { get; set; }
        public bool FirstEnterDone { get; set; }

        public Dictionary<StageDifficulty, int> StepByDifficulty { get; set; } = [];
        public List<ConquestTileDB> Tiles { get; set; } = [];
        public List<ConquestEchelonDB> Echelons { get; set; } = [];
        public ConquestStageSaveDB? OpenBattle { get; set; }

        public ConquestInfoDB ToInfoDB(long calculateConditionAmount) => new()
        {
            EventContentId = EventContentId,
            EventGauge = EventGauge,
            EventSpawnCount = EventSpawnCount,
            EchelonChangeCount = EchelonChangeCount,
            TodayConquestRentCount = TodayConquestRentCount,
            TodayOperationRentCount = TodayOperationRentCount,
            CumulatedConditionValue = CumulatedConditionValue,
            ReceivedCalculateRewardConditionAmount = ReceivedCalculateRewardConditionAmount,
            CalculateRewardConditionValue = calculateConditionAmount
        };
    }

    public static class ConquestInfoDBServerExtensions
    {
        public static IQueryable<ConquestInfoDBServer> GetAccountConquestInfos(this SchaleDataContext context, long accountId)
        {
            return context.ConquestInfos.Where(x => x.AccountServerId == accountId);
        }
    }
}
