using System.ComponentModel.DataAnnotations;
using Schale.FlatData;
using Schale.MX.TableBoard;

namespace Schale.Data.GameModel
{
    public class MiniGameTableBoardDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }

        public int Round { get; set; }
        public int ThemaIndex { get; set; }
        public TBGThemaType CurrentThemaMapType { get; set; }

        public TBGHexaMapDB? MainMap { get; set; }
        public TBGHexaMapDB? HiddenMap { get; set; }
        public TBGPlayerDB? Player { get; set; }

        public Dictionary<int, TBGThemaClearRecord> BestClearRecord { get; set; } = [];
        public List<int> HiddenTreasureRecord { get; set; } = [];
        public List<int> HiddenPotalOpenConditionRecord { get; set; } = [];

        // the encounter is stored flat rather than as the TBGEncounterDB subclass the wire carries, because the JSON column goes through System.Text.Json and it would deserialise every one of the four back as the base type, losing the stage the client is mid-way through.
        public bool HasEncounter { get; set; }
        public long EncounterId { get; set; }
        public long EncounterInvokerServerId { get; set; }
        public int EncounterObjectType { get; set; }
        public int EncounterStage { get; set; }
        public int EncounterOptionChoice { get; set; }
        public bool EncounterIsRealTreasure { get; set; }
        public bool EncounterShouldDecreaseItemEffectCounter { get; set; }
        public Dictionary<int, long> EncounterRewards { get; set; } = [];
    }

    public static class MiniGameTableBoardDBServerExtensions
    {
        public static IQueryable<MiniGameTableBoardDBServer> GetAccountMiniGameTableBoards(this SchaleDataContext context, long accountId)
        {
            return context.MiniGameTableBoards.Where(x => x.AccountServerId == accountId);
        }
    }
}
