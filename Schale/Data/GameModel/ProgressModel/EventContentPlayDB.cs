using System.ComponentModel.DataAnnotations;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    // One row per account per event, carrying every mini-mode counter the event's lobby protocols read back. The modes are mutually exclusive per event in practice but the client addresses them all by EventContentId alone,
    // so a table per mode would turn one lobby open into six lookups and six inserts for an account that has only ever touched one of them.
    public class EventContentPlayDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }

        public long BoxGachaSeed { get; set; }
        public long BoxGachaRound { get; set; }
        public int BoxGachaPurchaseCount { get; set; }
        public List<long> BoxGachaDrawnElementIds { get; set; } = [];

        public List<CardShopElementDB> CardShopElements { get; set; } = [];
        public Dictionary<Rarity, long> CardShopPurchaseCounts { get; set; } = [];
        public Dictionary<long, List<ParcelInfo>> CardShopRewardHistory { get; set; } = [];

        public long DiceRaceNode { get; set; }
        public long DiceRaceLapCount { get; set; }
        public long DiceRaceRollCount { get; set; }
        public long DiceRaceReceivedLapCount { get; set; }

        public int TreasureRound { get; set; }
        public int TreasureMetaRound { get; set; }
        public long TreasureVariationId { get; set; }

        /// <summary>
        /// Where every treasure of the current round actually sits. Server-only: the client is told about a treasure by <see cref="EventContentTreasureBoardHistory.Treasures"/> once its last cell is flipped, and about a hint through HintTreasures, so handing it the whole layout would give the board away.
        /// </summary>
        public List<EventContentTreasureObject> TreasureLayout { get; set; } = [];
        public List<EventContentTreasureCell> TreasureFlippedCells { get; set; } = [];
        public List<long> TreasureHintServerIds { get; set; } = [];

        public int FortuneGachaStackCount { get; set; }

        public List<long> RefreshShopIds { get; set; } = [];
        public long RefreshShopUseCount { get; set; }
        public DateTime RefreshShopUpdateTime { get; set; }

        public long ChangeCount { get; set; }
        public long AccumulateChangeCount { get; set; }
        public long ChangeUseAmount { get; set; }
        public DateTime ChangeUpdateTime { get; set; }
    }

    public static class EventContentPlayDBServerExtensions
    {
        public static IQueryable<EventContentPlayDBServer> GetAccountEventContentPlays(this SchaleDataContext context, long accountId)
        {
            return context.EventContentPlays.Where(x => x.AccountServerId == accountId);
        }

        public static EventContentPlayDBServer GetOrAddEventContentPlay(this SchaleDataContext context, long accountId, long eventContentId)
        {
            var play = context.EventContentPlays.FirstOrDefault(x => x.AccountServerId == accountId && x.EventContentId == eventContentId);
            if (play == null)
            {
                play = new EventContentPlayDBServer { AccountServerId = accountId, EventContentId = eventContentId };
                context.EventContentPlays.Add(play);
            }

            return play;
        }
    }

    public class EventContentCollectionDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long GroupId { get; set; }
        public long UniqueId { get; set; }
        public DateTime ReceiveDate { get; set; }
    }

    public static class EventContentCollectionDBServerExtensions
    {
        public static IQueryable<EventContentCollectionDBServer> GetAccountEventContentCollections(this SchaleDataContext context, long accountId)
        {
            return context.EventContentCollections.Where(x => x.AccountServerId == accountId);
        }
    }

    public class EventContentLocationDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public long LocationId { get; set; }
        public long Rank { get; set; }
        public long Exp { get; set; }
        public long ScheduleCount { get; set; }
        public Dictionary<long, List<VisitingCharacterDB>> ZoneVisitCharacterDBs { get; set; } = [];
    }

    public static class EventContentLocationDBServerExtensions
    {
        public static IQueryable<EventContentLocationDBServer> GetAccountEventContentLocations(this SchaleDataContext context, long accountId)
        {
            return context.EventContentLocations.Where(x => x.AccountServerId == accountId);
        }
    }
}
