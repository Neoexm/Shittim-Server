using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.FlatData;

namespace Shittim_Server.Services;

public class AronaAI
{
    private readonly IDbContextFactory<SchaleDataContext> _contextFactory;
    private readonly AronaService _aronaService;

    public static string AccountDevId = "aronauwu";
    public static string AccountName = "Arona";
    public static long AccountId;

    public AronaAI(IDbContextFactory<SchaleDataContext> contextFactory, AronaService aronaService)
    {
        _contextFactory = contextFactory;
        _aronaService = aronaService;
    }

    public async Task InitializeArona()
    {
        var context = await _contextFactory.CreateDbContextAsync();

        var account = await CreateAIAccount(context);
        AccountId = account.ServerId;

        await _aronaService.CreateArenaEchelon(context, account);

        _aronaService.CreateBatchAssistCharacters();
    }

    private async Task<AccountDBServer> CreateAIAccount(SchaleDataContext context)
    {
        var account = context.Accounts.FirstOrDefault(x => x.DevId == AccountDevId);
        var currentTime = DateTime.UtcNow;

        if (account != null)
        {
            account.LastConnectTime = currentTime;
            await context.SaveChangesAsync();
            return account;
        }

        account = new AccountDBServer
        {
            DevId = AccountDevId,
            Nickname = AccountName,
            CallName = AccountName,
            State = AccountState.Normal,
            Level = 90,
            Exp = 0,
            LobbyMode = 0,
            RepresentCharacterServerId = 19900006,
            MemoryLobbyUniqueId = 0,
            LastConnectTime = currentTime,
            CallNameUpdateTime = currentTime,
            CreateDate = currentTime,
            ContentInfo = new ContentInfoDB(),
            GameSettings = new AccountGameSettingDB(),
            BattleSummaries = new List<BattleSummaryDB>(),
            RaidSummaries = new List<RaidSummaryDB>(),
        };

        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        account = context.Accounts.FirstOrDefault(x => x.DevId == AccountDevId);
        return account!;
    }
}
