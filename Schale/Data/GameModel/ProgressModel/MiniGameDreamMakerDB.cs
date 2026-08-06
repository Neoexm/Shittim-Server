using System.ComponentModel.DataAnnotations;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class MiniGameDreamMakerDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long Round { get; set; }
        public long Multiplier { get; set; }
        public long DayOfNumber { get; set; }
        public long ActionCount { get; set; }
        public long CurrentRoundEndingId { get; set; }
        public bool EndingRewardReceived { get; set; }

        public Dictionary<DreamMakerParameterType, long> ParameterBases { get; set; } = [];
        public Dictionary<DreamMakerParameterType, long> ParameterAmounts { get; set; } = [];

        // MiniGameDreamCollectionScenarioExcel ids seen, kept across rounds because the mission that counts them never resets. Nothing on the wire carries these - the client plays the scenario from the AttendSchedule response and remembers it locally.
        public List<long> CollectionScenarios { get; set; } = [];
    }

    public class MiniGameDreamMakerEndingDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long EndingId { get; set; }
        public long ClearCount { get; set; }
        public DateTime ClearDate { get; set; }
    }

    public static class MiniGameDreamMakerDBServerExtensions
    {
        public static IQueryable<MiniGameDreamMakerDBServer> GetAccountMiniGameDreamMakers(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameDreamMakers.Where(x => x.AccountServerId == accountId);
        }

        public static IQueryable<MiniGameDreamMakerEndingDBServer> GetAccountMiniGameDreamMakerEndings(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameDreamMakerEndings.Where(x => x.AccountServerId == accountId);
        }
    }
}
