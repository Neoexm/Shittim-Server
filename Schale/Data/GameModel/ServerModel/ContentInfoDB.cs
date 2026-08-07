using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    public class ContentInfoDB
    {
        // Rows written before one of these blocks existed come back without the key at all, and the column is serialised with WhenWritingNull so a null block never gets written back either. Login sync dereferences them unguarded, so they have to default to something.
        public RaidDataInfo RaidDataInfo { get; set; } = new();
        public TimeAttackDungeonDataInfo TimeAttackDungeonDataInfo { get; set; } = new();
        public EliminateRaidDataInfo EliminateRaidDataInfo { get; set; } = new();
        public ArenaDataInfo ArenaDataInfo { get; set; } = new();
        public MultiFloorRaidDataInfo MultiFloorRaidDataInfo { get; set; } = new();

        // EventContentId -> claimed EventContentStageTotalRewardExcel row ids. Lives in the ContentInfo JSON column so no schema change is needed.
        public Dictionary<long, List<long>> ReceivedEventStageTotalRewards { get; set; } = [];

        public OptionDB Option { get; set; } = new();
        public List<CraftPresetSlotDB> CraftPresets { get; set; } = [];
        public List<ShiftingCraftInfoDB> ShiftingCrafts { get; set; } = [];
        public FriendIdCardDB IdCard { get; set; } = new();
        public List<FieldSnapshot> FieldSeasons { get; set; } = [];
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
        public long CumulativeTimeReward { get; set; }
        public DateTime? TimeRewardLastUpdateTime { get; set; }
        public DateTime? LastDailyRewardClaimTime { get; set; }
        public DateTime? BattleEnterActiveTime { get; set; }
    }

    public class MultiFloorRaidDataInfo
    {
        public long SeasonId { get; set; } = 1;
    }
}


