using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class RaidSummaryDB
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long SeasonId { get; set; }
        public long RaidStageId { get; set; }
        public long SnapshotId { get; set; } = 1;
        public long RaidDBId { get; set; }
        public long BattleRaidDBId { get; set; }
        public long CurrentTeam { get; set; } = 1;
        public bool IsMock { get; set; }
        public long Score { get; set; } = 0;
        public BattleStatus BattleStatus { get; set; } = BattleStatus.Pending;
        public Difficulty Difficulty { get; set; }
        public ContentTypeSummary ContentType { get; set; }
        public List<string> BattleSummaryIds { get; set; } = [];
        public Dictionary<long, List<string>> BattleSnapshotDatas { get; set; } = [];
    }

    public enum ContentTypeSummary : int
    {
        Raid,
        EliminateRaid
    }

    public enum BattleStatus : int
    {
        Pending,
        Win,
        Lose,
    }
}


