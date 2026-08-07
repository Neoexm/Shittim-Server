using System.Text;
using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.ModelMapping;
using Schale.MappingProfiles;
using Schale.Data.GameModel;
using Schale.FlatData;
using Shittim.Commands;
using Shittim.GameMasters;
using Shittim.Services.WebClient;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class RaidSeasonChangeTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"shittim-season-{Guid.NewGuid():N}.sqlite3");
    private readonly ExcelTableService _excels;

    public RaidSeasonChangeTests()
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

        _excels = new ExcelTableService();

        if (CommandFactory.commands.Count == 0)
            CommandFactory.LoadCommands();

        using var seed = NewContext();
        seed.Database.EnsureCreated();
        seed.Accounts.Add(new AccountDBServer(12345) { ServerId = 1, Nickname = "MakiChan" });
        seed.SaveChanges();
    }

    [Fact]
    public async Task TotalAssaultSeasonChangeReachesTheLobby()
    {
        var manager = new RaidManager(_excels);

        using (var db = NewContext())
            await manager.GetUpdatedLobby(db, db.GetAccount(1));

        using (var db = NewContext())
            await ContentGM.SetRaidSeason(db, db.GetAccount(1), _excels, 4);

        using (var db = NewContext())
        {
            var lobby = await manager.GetUpdatedLobby(db, db.GetAccount(1));
            Assert.Equal(4, lobby.SeasonId);
            Assert.Equal("Chesed_Outdoor", lobby.PlayableHighestDifficulty.Keys.Single());
        }
    }

    [Fact]
    public async Task ALobbyLeftOnAnOlderSeasonPicksUpTheNewBoss()
    {
        var manager = new RaidManager(_excels);

        using (var db = NewContext())
            await manager.GetUpdatedLobby(db, db.GetAccount(1));

        using (var db = NewContext())
        {
            var account = db.GetAccount(1);
            account.ContentInfo.RaidDataInfo.SeasonId = 4;
            await db.SaveChangesAsync();
        }

        using (var db = NewContext())
        {
            var lobby = await manager.GetUpdatedLobby(db, db.GetAccount(1));
            Assert.Equal("Chesed_Outdoor", lobby.PlayableHighestDifficulty.Keys.Single());
            // A new season starts with nothing claimed, so Raid_SeasonReward can pay out.
            Assert.Empty(lobby.ReceiveRewardIds);
        }
    }

    [Fact]
    public async Task GrandAssaultSeasonChangeReachesTheLobby()
    {
        var manager = new EliminateRaidManager(_excels);

        using (var db = NewContext())
            await manager.GetUpdatedLobby(db, db.GetAccount(1));

        using (var db = NewContext())
            await ContentGM.SetEliminateRaidSeason(db, db.GetAccount(1), _excels, 4);

        using (var db = NewContext())
        {
            var lobby = await manager.GetUpdatedLobby(db, db.GetAccount(1));
            var season = _excels.GetTable<EliminateRaidSeasonManageExcelT>().First(x => x.SeasonId == 4);
            Assert.Equal(4, lobby.SeasonId);
            Assert.Equal(season.OpenRaidBossGroup01, lobby.OpenedBossGroups.First());
            Assert.Contains(season.OpenRaidBossGroup01, lobby.BestRankingPointPerBossGroup.Keys);
            Assert.Contains(season.OpenRaidBossGroup01, lobby.PlayableHighestDifficulty.Keys);

            var wire = lobby.ToMap(Mapper());
            Assert.Equal(4, wire.SeasonId);
            Assert.Equal(season.OpenRaidBossGroup01, wire.OpenedBossGroups.First());
            Assert.Contains(season.OpenRaidBossGroup01, wire.PlayableHighestDifficulty.Keys);
        }
    }

    private static IMapper Mapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    // The lobby is the only thing the client reads, so a season that only lands on the account row still shows the old boss.
    [Fact]
    public async Task ASeasonSetWhileNoLobbyRowExistsStillShowsTheRightBoss()
    {
        using (var db = NewContext())
            await ContentGM.SetEliminateRaidSeason(db, db.GetAccount(1), _excels, 4);

        using (var db = NewContext())
        {
            var lobby = await new EliminateRaidManager(_excels).GetUpdatedLobby(db, db.GetAccount(1));
            var season = _excels.GetTable<EliminateRaidSeasonManageExcelT>().First(x => x.SeasonId == 4);
            Assert.Equal(season.OpenRaidBossGroup01, lobby.OpenedBossGroups.First());
        }
    }

    [Fact]
    public async Task EverySeasonAppliesThroughTheChatCommand()
    {
        var failures = new StringBuilder();

        using (var db = NewContext())
        {
            await new RaidManager(_excels).GetUpdatedLobby(db, db.GetAccount(1));
            await new EliminateRaidManager(_excels).GetUpdatedLobby(db, db.GetAccount(1));
        }

        foreach (var seasonId in _excels.GetTable<RaidSeasonManageExcelT>().Select(x => x.SeasonId))
            await Run("total", seasonId, failures);

        foreach (var seasonId in _excels.GetTable<EliminateRaidSeasonManageExcelT>().Select(x => x.SeasonId))
            await Run("grand", seasonId, failures);

        Assert.True(failures.Length == 0, failures.ToString());
    }

    private async Task Run(string type, long seasonId, StringBuilder failures)
    {
        using var output = new MemoryStream();
        var writer = new StreamWriter(output) { AutoFlush = true };
        var connection = new WebClientConnection(new Factory(_path), null, _excels, writer, 1);

        var cmd = CommandFactory.CreateCommand("setseason", connection, [type, seasonId.ToString()]);
        try
        {
            await cmd.Execute();
        }
        catch (Exception ex)
        {
            failures.AppendLine($"{type} {seasonId}: {ex}");
            return;
        }

        var text = Encoding.UTF8.GetString(output.ToArray());
        if (!text.Contains($"set to {seasonId}"))
            failures.AppendLine($"{type} {seasonId}: {text.Replace(Environment.NewLine, " / ").Trim()}");
    }

    private SchaleDataContext NewContext() => new(
        new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={_path}").Options);

    private sealed class Factory(string path) : IDbContextFactory<SchaleDataContext>
    {
        public SchaleDataContext CreateDbContext() => new(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }
}
