using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class EliminateRaidLobbyInfoDBServer : RaidLobbyInfoDBServer
    {
        public List<string> OpenedBossGroups { get; set; } = [];
        public Dictionary<string, long> BestRankingPointPerBossGroup { get; set; } = [];
    }

    public static class EliminateRaidLobbyInfoDBServerExtensions
    {
        public static IQueryable<EliminateRaidLobbyInfoDBServer> GetAccountEliminateRaidLobbyInfos(this SchaleDataContext context, long accountId)
        {
            return context.EliminateRaidLobbyInfos.Where(x => x.AccountServerId == accountId);
        }
    }
}


