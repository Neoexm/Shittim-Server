using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class ContentInfoDB
    {
        public RaidDataInfo? RaidDataInfo { get; set; }
        public TimeAttackDungeonDataInfo? TimeAttackDungeonDataInfo { get; set; }
        public EliminateRaidDataInfo? EliminateRaidDataInfo { get; set; }
        public ArenaDataInfo? ArenaDataInfo { get; set; }
        public MultiFloorRaidDataInfo? MultiFloorRaidDataInfo { get; set; }
    }

    public class RaidDataInfo
    {
        public long SeasonId { get; set; } = 1;
        public long CurrentRaidUniqueId { get; set; }
        public long TimeBonus { get; set; }
        public Difficulty CurrentDifficulty { get; set; }
        public long BestRankingPoint { get; set; } = 0;
        public long TotalRankingPoint { get; set; } = 0;
    }

    public class EliminateRaidDataInfo : RaidDataInfo
    {
        public Dictionary<int, int> SweepPointByRaidUniqueId { get; set; } = [];
    }

    public class TimeAttackDungeonDataInfo
    {
        public long SeasonId { get; set; } = 1;
        public long SeasonBestRecord { get; set; } = 0;
    }

    public class ArenaDataInfo
    {
        public long SeasonId { get; set; } = 1;
    }

    public class MultiFloorRaidDataInfo
    {
        public long SeasonId { get; set; } = 1;
    }
}


