using System.ComponentModel.DataAnnotations;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    public class ConquestInfoDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public int EchelonChangeCount { get; set; }
        public List<ConquestEchelonDB> Echelons { get; set; } = [];
    }

    public class ConquestTileDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public StageDifficulty Difficulty { get; set; }
        public long TileUniqueId { get; set; }
        public long Level { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public static class ConquestDBServerExtensions
    {
        public static IQueryable<ConquestTileDBServer> GetAccountConquestTiles(this SchaleDataContext context, long accountId, long eventContentId)
        {
            return context.ConquestTiles.Where(x => x.AccountServerId == accountId && x.EventContentId == eventContentId);
        }
    }
}
