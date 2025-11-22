using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;

namespace Shittim_Server.GameClient
{
    public static class GameClientHelper
    {
        public static async Task<AccountDBServer> CreateAIClient(SchaleDataContext context, string accountName, string accountDevId)
        {
            var account = context.Accounts.FirstOrDefault(x => x.DevId == accountDevId);
            var currentTime = DateTime.Now;

            if (account != null)
            {
                account.LastConnectTime = currentTime;
                return account;
            }

            account = new AccountDBServer()
            {
                DevId = accountDevId.ToString(),
                Nickname = accountName,
                CallName = accountName,
                State = AccountState.Normal,
                Level = 90,
                Exp = 0,
                LobbyMode = 0,
                RepresentCharacterServerId = 19900006,
                MemoryLobbyUniqueId = 0,
                LastConnectTime = currentTime,
                CallNameUpdateTime = currentTime,
                CreateDate = currentTime,
                ContentInfo = new(),
                GameSettings = new(),
                BattleSummaries = new List<BattleSummaryDB>(),
                RaidSummaries = new List<RaidSummaryDB>(),
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            account = context.Accounts.FirstOrDefault(x => x.DevId == accountDevId);
            return account;
        }
    }
}
