using System.ComponentModel.DataAnnotations;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    // rhythm and every other score-only minigame; UniqueId is the MiniGameRhythmExcel row, or the stage id for the ones that reuse EventContentStageExcel
    public class MiniGameHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long HighScore { get; set; }
        public long AccumulatedScore { get; set; }
        public DateTime ClearDate { get; set; }
        public bool IsFullCombo { get; set; }
    }

    public static class MiniGameHistoryDBServerExtensions
    {
        public static IQueryable<MiniGameHistoryDBServer> GetAccountMiniGameHistories(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameHistories.Where(x => x.AccountServerId == accountId);
        }
    }

    public class MiniGameShootingHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long ArriveSection { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }

    public static class MiniGameShootingHistoryDBServerExtensions
    {
        public static IQueryable<MiniGameShootingHistoryDBServer> GetAccountMiniGameShootingHistories(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameShootingHistories.Where(x => x.AccountServerId == accountId);
        }
    }

    public class MiniGameDefenseStageHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long StageId { get; set; }
        public bool Star1Flag { get; set; }
        public bool Star2Flag { get; set; }
        public bool Star3Flag { get; set; }
        public bool FirstClearRewardReceive { get; set; }
        public bool StarRewardReceive { get; set; }
    }

    public static class MiniGameDefenseStageHistoryDBServerExtensions
    {
        public static IQueryable<MiniGameDefenseStageHistoryDBServer> GetAccountMiniGameDefenseStageHistories(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameDefenseStageHistories.Where(x => x.AccountServerId == accountId);
        }
    }

    public class MiniGameRoadPuzzleHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public long Round { get; set; }
        public DateTime ClearDate { get; set; }
    }

    // the run in progress. MiniGame_RoadPuzzleGetInfo answers with this and nothing else, so an account that has never opened the puzzle has no row rather than an empty board.
    public class MiniGameRoadPuzzleSaveDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public RoadPuzzleBoardSaveDB? SaveDB { get; set; }
    }

    public static class MiniGameRoadPuzzleDBServerExtensions
    {
        public static IQueryable<MiniGameRoadPuzzleHistoryDBServer> GetAccountMiniGameRoadPuzzleHistories(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameRoadPuzzleHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}
