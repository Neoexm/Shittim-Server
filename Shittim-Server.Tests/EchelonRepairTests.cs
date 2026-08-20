using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MappingProfiles;
using Schale.MX.NetworkProtocol;
using Shittim.Services;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// a character row that goes missing leaves its ServerId behind in every echelon slot that held it and in RepresentCharacterServerId, and the client stops on a blank lobby instead of reporting it, so an account in that state can never be fixed from in game.
public class EchelonRepairTests
{
    [Fact]
    public async Task LoginSyncClearsEchelonSlotsPointingAtACharacterThatIsGone()
    {
        using var db = await NewContext();
        var account = db.Accounts.Single();
        var echelon = db.Echelons.Single(x => x.AccountServerId == account.ServerId);

        var lost = echelon.LeaderServerId;
        var survivor = echelon.MainSlotServerIds.First(x => x != 0 && x != lost);
        db.Characters.Remove(db.Characters.Single(x => x.ServerId == lost));
        account.RepresentCharacterServerId = lost;
        db.SaveChanges();

        await Handler().LoginSync(db, new AccountLoginSyncRequest { SessionKey = Key(account) }, new AccountLoginSyncResponse());

        echelon = db.Echelons.Single(x => x.AccountServerId == account.ServerId);
        Assert.DoesNotContain(lost, echelon.MainSlotServerIds.Concat(echelon.SupportSlotServerIds));
        Assert.Equal(survivor, echelon.LeaderServerId);
        Assert.NotEqual(lost, db.Accounts.Single().RepresentCharacterServerId);
        Assert.Contains(db.Accounts.Single().RepresentCharacterServerId, db.Characters.Select(x => x.ServerId).ToList());
    }

    [Fact]
    public async Task LoginSyncLeavesAnEchelonWhoseSlotsAllResolve()
    {
        using var db = await NewContext();
        var account = db.Accounts.Single();
        var echelon = db.Echelons.Single(x => x.AccountServerId == account.ServerId);
        var main = echelon.MainSlotServerIds.ToList();
        var support = echelon.SupportSlotServerIds.ToList();
        var leader = echelon.LeaderServerId;

        await Handler().LoginSync(db, new AccountLoginSyncRequest { SessionKey = Key(account) }, new AccountLoginSyncResponse());

        echelon = db.Echelons.Single(x => x.AccountServerId == account.ServerId);
        Assert.Equal(main, echelon.MainSlotServerIds);
        Assert.Equal(support, echelon.SupportSlotServerIds);
        Assert.Equal(leader, echelon.LeaderServerId);
    }

    private static AccountHandler Handler() => new(
        null!, new FixedSessionService(), Excels, Mapper,
        LoggerFactory.Create(b => { }).CreateLogger<AccountHandler>(),
        new MissionService(Excels, Mapper), new AttendanceService(Excels),
        new ParcelHandler(Excels, Mapper),
        new CafeManager(Excels, new ParcelHandler(Excels, Mapper), new ConsumeHandler(Excels, Mapper), Mapper));

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

    private static async Task<SchaleDataContext> NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-echelonrepair-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();

        var account = new AccountDBServer { ServerId = 1, Nickname = "Sensei1", Level = 1 };
        context.Accounts.Add(account);
        context.SaveChanges();
        await AccountService.CreateAccount(context, account, Excels, new ParcelHandler(Excels, Mapper));

        return context;
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

    private static readonly IMapper Mapper = BuildMapper();

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
