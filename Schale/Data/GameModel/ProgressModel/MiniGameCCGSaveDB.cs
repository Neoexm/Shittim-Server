using System.ComponentModel.DataAnnotations;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    // The card battle itself runs entirely on the client - MiniGame_CCGEndStageDual hands back a MiniGameCCGSummary it has already resolved - so there is nothing for the server to simulate and the run state round-trips as one blob.
    // Perks and RewardPoint are duplicated out of it because they outlive a run: buying a perk between runs has to stick, and the lobby reads both before any save exists.
    public class MiniGameCCGSaveDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long CCGId { get; set; }

        public MiniGameCCGSaveDB? SaveDB { get; set; }

        public List<long> Perks { get; set; } = [];
        public int RewardPoint { get; set; }
        public int ClearCount { get; set; }
        public List<long> ReceivedRewardItemIds { get; set; } = [];
    }

    public static class MiniGameCCGSaveDBServerExtensions
    {
        public static IQueryable<MiniGameCCGSaveDBServer> GetAccountMiniGameCCGSaves(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameCCGSaves.Where(x => x.AccountServerId == accountId);
        }
    }
}
