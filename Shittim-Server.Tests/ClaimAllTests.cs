using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class ClaimAllTests
{
    [Fact]
    public async Task ClaimAllOnTheAllTabClaimsACompletedCafeDaily()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var daily = CompletedCafeDaily(db, account);

        var response = await Handler().MultipleReward(db,
            new MissionMultipleRewardRequest { SessionKey = Key(account), MissionCategory = MissionCategory.All },
            new MissionMultipleRewardResponse());

        Assert.Contains(response.AddedHistoryDBs!, x => x.MissionUniqueId == daily.Id);
        Assert.Empty(db.MissionProgresses.Where(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == daily.Id));
        Assert.Single(db.MissionHistories.Where(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == daily.Id));
    }

    [Fact]
    public async Task ClaimAllOnTheDailyTabClaimsACompletedCafeDaily()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var daily = CompletedCafeDaily(db, account);

        var response = await Handler().MultipleReward(db,
            new MissionMultipleRewardRequest { SessionKey = Key(account), MissionCategory = MissionCategory.Daily },
            new MissionMultipleRewardResponse());

        Assert.Contains(response.AddedHistoryDBs!, x => x.MissionUniqueId == daily.Id);
    }

    [Fact]
    public async Task AZeroEventContentIdDoesNotFilterEveryMissionOut()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var daily = CompletedCafeDaily(db, account);

        var response = await Handler().MultipleReward(db,
            new MissionMultipleRewardRequest { SessionKey = Key(account), MissionCategory = MissionCategory.All, EventContentId = 0 },
            new MissionMultipleRewardResponse());

        Assert.Contains(response.AddedHistoryDBs!, x => x.MissionUniqueId == daily.Id);
    }

    private static MissionExcelT CompletedCafeDaily(SchaleDataContext db, AccountDBServer account)
    {
        var daily = Excels.GetTable<MissionExcelT>().First(x =>
            x.Category == MissionCategory.Daily && x.CompleteConditionType == MissionCompleteConditionType.Reset_CafeInteractionCount);

        db.MissionProgresses.Add(new MissionProgressDBServer
        {
            AccountServerId = account.ServerId,
            MissionUniqueId = daily.Id,
            StartTime = DateTime.UtcNow,
            ProgressParameters = new Dictionary<long, long> { [0] = daily.CompleteConditionCount },
            Complete = true
        });
        db.SaveChanges();
        return daily;
    }

    private static readonly ExcelTableService Excels = LoadExcels();

    private static ExcelTableService LoadExcels()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        ExcelTableService.DumpedDir = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.First(Directory.Exists);

        return new ExcelTableService();
    }

    private static MissionHandler Handler() => new(
        null!, new FixedSessionService(), Mapper, Excels,
        new ParcelHandler(Excels, Mapper), new MissionService(Excels, Mapper));

    private static SessionKey Key(AccountDBServer account) => new() { AccountServerId = account.ServerId, MxToken = "t" };

    private class FixedSessionService : ISessionKeyService
    {
        public Task<AccountDBServer> GetAuthenticatedUser(SchaleDataContext context, SessionKey? sessionKey) =>
            Task.FromResult(context.Accounts.Single(x => x.ServerId == sessionKey!.AccountServerId));

        public Task<SessionKey?> GenerateSession(long publisherAccountId, string? customToken = null) => throw new NotSupportedException();
        public bool ValidateRequest(RequestPacket request) => true;
        public void RevokeSession(long userId) { }
        public int PurgeExpiredSessions(TimeSpan maxInactivity) => 0;
    }

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-claimalltest-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db)
    {
        var account = new AccountDBServer { ServerId = 1, Nickname = "Sensei1" };
        db.Accounts.Add(account);
        db.Currencies.Add(new AccountCurrencyDBServer(1));
        db.SaveChanges();
        return account;
    }

    private static readonly IMapper Mapper = BuildMapper();

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
