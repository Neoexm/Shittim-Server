using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// a craft slot that grew past the legal five nodes (the old SelectNode kept appending tier-4 dupes once the final node rolled no leaves) is resent forever by Craft_List, and the client replays a broken node animation and bails to the lobby on every menu open.
public class CraftRecoveryTests
{
    [Fact]
    public async Task CraftListDropsASlotWithDuplicateTierNodes()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        db.CraftInfos.Add(new CraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 1,
            StartTime = DateTime.MaxValue,
            EndTime = DateTime.MaxValue,
            Nodes =
            [
                new CraftNodeDB { NodeTier = CraftNodeTier.Base, NodeLevel = 1, NodeRandomSeed = 5 },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node01, NodeId = 21, NodeRandomSeed = 5 },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node02, NodeId = 31, NodeRandomSeed = 5 },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node03, NodeId = 41, NodeRandomSeed = 5 },
                new CraftNodeDB { NodeTier = CraftNodeTier.Max, NodeId = 51, NodeRandomSeed = 5 },
                new CraftNodeDB { NodeTier = CraftNodeTier.Max, NodeId = 52 },
                new CraftNodeDB { NodeTier = CraftNodeTier.Max, NodeId = 53 }
            ]
        });
        db.SaveChanges();

        var response = await Handler().CraftList(db, new CraftInfoListRequest { SessionKey = Key(account) }, new CraftInfoListResponse());

        Assert.Null(response.CraftInfos);
        Assert.Empty(db.CraftInfos.Where(x => x.AccountServerId == account.ServerId));
    }

    [Fact]
    public async Task CraftListKeepsAHealthyInProgressSlot()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        db.CraftInfos.Add(new CraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 1,
            StartTime = DateTime.MaxValue,
            EndTime = DateTime.MaxValue,
            Nodes =
            [
                new CraftNodeDB { NodeTier = CraftNodeTier.Base, NodeLevel = 2, NodeRandomSeed = 5, LeafNodeIds = [21, 22, 23, 24, 25] },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node01, NodeId = 21, LeafNodeIds = [] }
            ]
        });
        db.SaveChanges();

        var response = await Handler().CraftList(db, new CraftInfoListRequest { SessionKey = Key(account) }, new CraftInfoListResponse());

        Assert.NotNull(response.CraftInfos);
        Assert.Single(response.CraftInfos);
        Assert.Single(db.CraftInfos.Where(x => x.AccountServerId == account.ServerId));
    }

    [Fact]
    public async Task SelectingPastAMaxedNodeThrowsInsteadOfDuplicatingATier()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        // the finished shape: every tier committed, the Max node leveled and left with no leaves to offer
        db.CraftInfos.Add(new CraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 1,
            StartTime = DateTime.MaxValue,
            EndTime = DateTime.MaxValue,
            Nodes =
            [
                new CraftNodeDB { NodeTier = CraftNodeTier.Base, NodeLevel = 1, NodeRandomSeed = 5, LeafNodeIds = [21, 22, 23, 24, 25] },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node01, NodeId = 21, NodeRandomSeed = 5, LeafNodeIds = [31, 32, 33, 34, 35] },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node02, NodeId = 31, NodeRandomSeed = 5, LeafNodeIds = [41, 42, 43, 44, 45] },
                new CraftNodeDB { NodeTier = CraftNodeTier.Node03, NodeId = 41, NodeRandomSeed = 5, LeafNodeIds = [51, 52, 53, 54, 55] },
                new CraftNodeDB { NodeTier = CraftNodeTier.Max, NodeId = 51, NodeRandomSeed = 5, LeafNodeIds = [] }
            ]
        });
        db.SaveChanges();

        await Assert.ThrowsAsync<WebAPIException>(() => Handler().SelectNode(
            db, new CraftSelectNodeRequest { SessionKey = Key(account), SlotId = 1, LeafNodeIndex = 0 }, new CraftSelectNodeResponse()));

        Assert.Equal(5, db.CraftInfos.Single(x => x.AccountServerId == account.ServerId).Nodes!.Count);
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

    private static CraftHandler Handler() => new(
        null!, new FixedSessionService(), Excels, Mapper,
        new ConsumeHandler(Excels, Mapper), new ParcelHandler(Excels, Mapper), new MissionService(Excels, Mapper));

    private static SessionKey Key(AccountDBServer account) => new() { AccountServerId = account.ServerId, MxToken = "t" };

    // the handlers only ever ask it to resolve the session back to the account row
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
        var path = Path.Combine(Path.GetTempPath(), $"shittim-craftrecoverytest-{Guid.NewGuid():N}.sqlite3");
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
