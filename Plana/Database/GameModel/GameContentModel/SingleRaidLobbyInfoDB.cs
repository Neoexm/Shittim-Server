using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;

namespace Plana.Database.GameModel
{
    public class SingleRaidLobbyInfoDBServer : RaidLobbyInfoDBServer
    {
        public List<Difficulty> ClearDifficulty { get; set; } = [];
    }

    public static class SingleRaidLobbyInfoDBServerExtensions
    {
        public static IQueryable<SingleRaidLobbyInfoDBServer> GetAccountSingleRaidLobbyInfos(this SCHALEContext context, long accountId)
        {
            return context.SingleRaidLobbyInfos.Where(x => x.AccountServerId == accountId);
        }
    }
}