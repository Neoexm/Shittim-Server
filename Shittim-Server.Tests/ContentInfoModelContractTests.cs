using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Xunit;

namespace Shittim_Server.Tests;

// ContentInfo is a JSON column, so a build that adds a block to ContentInfoDB cannot see rows written before that block existed: the key is simply absent and System.Text.Json leaves the property at its default. The column is also written back with JsonIgnoreCondition.WhenWritingNull, so once a block is null it stops being serialised and stays missing. Account_LoginSync reads account.ContentInfo.ArenaDataInfo.SeasonId with no guard, so a null block is a NullReferenceException on every login attempt with no way back into the account.
public class ContentInfoModelContractTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"shittim-contentinfo-{Guid.NewGuid():N}.sqlite3");

    [Fact]
    public async Task AnAccountRowWrittenBeforeTheContentBlocksExistedStillLoads()
    {
        await SeedAccountWithStoredContentInfo("{\"ReceivedEventStageTotalRewards\":{}}");

        using var db = NewContext();
        var account = db.Accounts.Single();

        // The exact dereference AccountHandler performs when it builds ArenaLoginResponse.
        Assert.Equal(1, account.ContentInfo.ArenaDataInfo.SeasonId);
        Assert.Null(account.ContentInfo.ArenaDataInfo.TimeRewardLastUpdateTime);
        Assert.NotNull(account.ContentInfo.RaidDataInfo);
        Assert.NotNull(account.ContentInfo.EliminateRaidDataInfo);
        Assert.NotNull(account.ContentInfo.TimeAttackDungeonDataInfo);
        Assert.NotNull(account.ContentInfo.MultiFloorRaidDataInfo);
    }

    [Fact]
    public async Task AStoredSeasonSurvivesTheDefault()
    {
        await SeedAccountWithStoredContentInfo("{\"ArenaDataInfo\":{\"SeasonId\":7,\"CumulativeTimeReward\":42}}");

        using var db = NewContext();
        var arena = db.Accounts.Single().ContentInfo.ArenaDataInfo;

        Assert.Equal(7, arena.SeasonId);
        Assert.Equal(42, arena.CumulativeTimeReward);
    }

    [Fact]
    public async Task AnAccountThatNeverHadAContentInfoColumnValueStillLoads()
    {
        await SeedAccountWithStoredContentInfo("{}");

        using var db = NewContext();
        Assert.Equal(1, db.Accounts.Single().ContentInfo.RaidDataInfo.SeasonId);
    }

    // The contract the three cases above rest on. A block added to ContentInfoDB without an initialiser reintroduces the login break for every account that predates it, and nothing else in the build would catch it.
    [Fact]
    public void EveryContentBlockDeserialisesToSomethingFromAnEmptyObject()
    {
        var missing = typeof(ContentInfoDB)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !p.PropertyType.IsValueType && p.PropertyType != typeof(string))
            .Where(p => p.GetValue(new ContentInfoDB()) == null)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(missing);
    }

    private async Task SeedAccountWithStoredContentInfo(string json)
    {
        using var seed = NewContext();
        seed.Database.EnsureCreated();
        seed.Accounts.Add(new AccountDBServer { ServerId = 1, Nickname = "Sensei" });
        await seed.SaveChangesAsync();

        await seed.Database.ExecuteSqlRawAsync("UPDATE \"Accounts\" SET \"ContentInfo\" = {0} WHERE \"ServerId\" = 1", json);
    }

    private SchaleDataContext NewContext() => new(
        new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={_path}").Options);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
