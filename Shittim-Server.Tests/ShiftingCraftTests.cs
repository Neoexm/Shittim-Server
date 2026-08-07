using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The shifting-craft track and the batch node-craft variants.
public class ShiftingCraftTests
{
    [Fact]
    public async Task AutoBeginProcessStartsAFullyResolvedCraft()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var request = new CraftAutoBeginProcessRequest
        {
            SessionKey = Key(account),
            Count = 1,
            PresetSlotDB = new CraftPresetSlotDB
            {
                PresetNodeDBs =
                [
                    new CraftPresetNodeDB { NodeTier = CraftNodeTier.Node01, IsActivated = true },
                    new CraftPresetNodeDB { NodeTier = CraftNodeTier.Node02, IsActivated = true },
                    new CraftPresetNodeDB { NodeTier = CraftNodeTier.Node03, IsActivated = true }
                ]
            }
        };

        var response = await Handler().AutoBeginProcess(db, request, new CraftAutoBeginProcessResponse());

        Assert.NotNull(response.CraftInfoDBs);
        var started = Assert.Single(response.CraftInfoDBs);
        Assert.NotEqual(DateTime.MaxValue, started.StartTime);
        Assert.True(started.EndTime > started.StartTime);

        var slot = db.CraftInfos.Single(x => x.AccountServerId == account.ServerId);
        Assert.Contains(slot.Nodes!, x => x.NodeId != 0 && x.CraftNodeResult?.ParcelInfo != null);
    }

    [Fact]
    public async Task RewardAllClaimsTheFinishedSlotsAndKeepsTheRest()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var now = account.GameSettings.ServerDateTime();

        db.CraftInfos.Add(new CraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 1,
            StartTime = now.AddHours(-3),
            EndTime = now.AddMinutes(-1),
            Nodes =
            [
                new CraftNodeDB
                {
                    NodeTier = CraftNodeTier.Node01,
                    NodeId = 21,
                    CraftNodeResult = new CraftNodeResult
                    {
                        NodeTier = CraftNodeTier.Node01,
                        ParcelInfo = new ParcelInfo
                        {
                            Key = new ParcelKeyPair { Type = ParcelType.Item, Id = 2 },
                            Amount = 3
                        }
                    }
                }
            ]
        });
        db.CraftInfos.Add(new CraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 2,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(1),
            Nodes = []
        });
        db.SaveChanges();

        var response = await Handler().RewardAll(db, new CraftRewardAllRequest { SessionKey = Key(account) }, new CraftRewardAllResponse());

        Assert.NotNull(response.ParcelResultDB);
        Assert.NotNull(response.CraftInfos);
        Assert.Equal(2, Assert.Single(response.CraftInfos).SlotSequence);
        Assert.Equal(2, db.CraftInfos.Single(x => x.AccountServerId == account.ServerId).SlotSequence);
        Assert.Equal(3, db.GetAccountItems(account.ServerId).Single(x => x.UniqueId == 2).StackCount);
    }

    [Fact]
    public async Task ShiftingCompleteProcessAllFinishesTheRunningSlots()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var now = account.GameSettings.ServerDateTime();

        db.ShiftingCraftInfos.Add(new ShiftingCraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 1,
            CraftRecipeId = 1,
            CraftAmount = 1,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(2)
        });
        db.ShiftingCraftInfos.Add(new ShiftingCraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 2,
            CraftRecipeId = 1,
            CraftAmount = 1,
            StartTime = now.AddHours(-2),
            EndTime = now.AddMinutes(-1)
        });
        db.SaveChanges();

        var response = await Handler().ShiftingCompleteProcessAll(
            db, new CraftShiftingCompleteProcessAllRequest { SessionKey = Key(account) }, new CraftShiftingCompleteProcessAllResponse());

        Assert.NotNull(response.CraftInfoDBs);
        Assert.Equal(2, response.CraftInfoDBs.Count);
        Assert.All(
            db.ShiftingCraftInfos.Where(x => x.AccountServerId == account.ServerId).ToList(),
            x => Assert.True(x.EndTime <= account.GameSettings.ServerDateTime()));
    }

    [Fact]
    public async Task ShiftingRewardBeforeTheEndThrows()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var now = account.GameSettings.ServerDateTime();

        db.ShiftingCraftInfos.Add(new ShiftingCraftInfoDBServer
        {
            AccountServerId = account.ServerId,
            SlotSequence = 1,
            CraftRecipeId = 1,
            CraftAmount = 1,
            StartTime = now,
            EndTime = now.AddHours(1)
        });
        db.SaveChanges();

        await Assert.ThrowsAsync<WebAPIException>(() => Handler().ShiftingReward(
            db, new CraftShiftingRewardRequest { SessionKey = Key(account), SlotId = 1 }, new CraftShiftingRewardResponse()));

        Assert.Single(db.ShiftingCraftInfos.Where(x => x.AccountServerId == account.ServerId));
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
        var path = Path.Combine(Path.GetTempPath(), $"shittim-shiftingcrafttest-{Guid.NewGuid():N}.sqlite3");
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
