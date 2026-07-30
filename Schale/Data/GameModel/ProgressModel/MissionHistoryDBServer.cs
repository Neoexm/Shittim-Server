using System.ComponentModel.DataAnnotations;

namespace Schale.Data.GameModel
{
    // Persisted mission claim history. Official's account-wide Mission_List returns every claimed mission id in MissionHistoryUniqueIds (a freshly claimed id appears in the next call),
    // so the claims made through Mission_Reward / Mission_MultipleReward must outlive the response.
    public class MissionHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long MissionUniqueId { get; set; }
        public DateTime CompleteTime { get; set; }
    }

    public static class MissionHistoryDBServerExtensions
    {
        public static IQueryable<MissionHistoryDBServer> GetAccountMissionHistories(this SchaleDataContext context, long accountId)
        {
            return context.MissionHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}
