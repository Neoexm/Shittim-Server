using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class SingleRaidLobbyInfoDBServer : RaidLobbyInfoDBServer
    {
        public List<Difficulty> ClearDifficulty { get; set; } = [];
    }

    public static class SingleRaidLobbyInfoDBServerExtensions
    {
        public static IQueryable<SingleRaidLobbyInfoDBServer> GetAccountSingleRaidLobbyInfos(this SchaleDataContext context, long accountId)
        {
            return context.SingleRaidLobbyInfos.Where(x => x.AccountServerId == accountId);
        }
    }
}


