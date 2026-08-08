using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The pool is static state backed by json files next to the server exe, so every test snapshots those files and puts them back. The excel side points at an empty directory - a manifest that names its bosses never opens the tables, and that is the shape the coordinator sends.
[Collection("exceldb-paths")]
public class WorldRaidStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-worldraid-{Guid.NewGuid():N}");
    private readonly string _originalDumpedDir = ExcelTableService.DumpedDir;
    private readonly string _savedUrl = Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl;
    private readonly Dictionary<string, string?> _savedFiles = new();

    public WorldRaidStateTests()
    {
        foreach (var name in new[] { "worldraid_manifest.json", "worldraid_state.json", "worldraid_pending.json" })
        {
            var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", name));
            _savedFiles[path] = File.Exists(path) ? File.ReadAllText(path) : null;
        }

        Directory.CreateDirectory(_dir);
        ExcelTableService.DumpedDir = _dir;
        Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl = "";
        WorldRaidService.SetManifest(null, new ExcelTableService());
    }

    [Fact]
    public void ANewSeasonSeedsEveryPoolTheManifestNames()
    {
        WorldRaidService.SetManifest(Raid(814, (814000, 5_000_000_000), (814100, 3_000_000_000)), new ExcelTableService());

        Assert.Equal(814, WorldRaidService.State!.seasonId);
        Assert.Equal(5_000_000_000, WorldRaidService.RemainingHP(814000));
        Assert.Equal(3_000_000_000, WorldRaidService.RemainingHP(814100));
        Assert.Equal(0, WorldRaidService.Participants(814000));
    }

    [Fact]
    public void RepublishingTheSameSeasonKeepsTheRunningPools()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Raid(814, (814000, 5_000_000_000)), excel);
        WorldRaidService.AddDamage(814000, 1_000_000_000);

        var edited = Raid(814, (814000, 5_000_000_000));
        edited.close = "2026-08-20 10:59:59";
        WorldRaidService.SetManifest(edited, excel);

        Assert.Equal(4_000_000_000, WorldRaidService.RemainingHP(814000));
    }

    [Fact]
    public void SwitchingSeasonsStartsOver()
    {
        var excel = new ExcelTableService();
        Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl = "http://coordinator.example:5200";
        WorldRaidService.SetManifest(Raid(814, (814000, 5_000_000_000)), excel);
        WorldRaidService.AddDamage(814000, 1_000_000_000);

        WorldRaidService.SetManifest(Raid(823, (823000, 7_000_000_000)), excel);

        Assert.Equal(823, WorldRaidService.State!.seasonId);
        Assert.Null(WorldRaidService.RemainingHP(814000));
        Assert.Equal(7_000_000_000, WorldRaidService.RemainingHP(823000));
        Assert.Empty(WorldRaidService.PendingSnapshot());
    }

    [Fact]
    public void DamagePastZeroClampsInsteadOfGoingNegative()
    {
        WorldRaidService.SetManifest(Raid(814, (814000, 1000)), new ExcelTableService());
        WorldRaidService.AddDamage(814000, 5000);

        Assert.Equal(0, WorldRaidService.RemainingHP(814000));
        Assert.Equal(1, WorldRaidService.Participants(814000));
    }

    [Fact]
    public void NothingQueuesWhenNoCoordinatorIsConfigured()
    {
        WorldRaidService.SetManifest(Raid(814, (814000, 5000)), new ExcelTableService());
        WorldRaidService.AddDamage(814000, 500);

        Assert.Equal(4500, WorldRaidService.RemainingHP(814000));
        Assert.Empty(WorldRaidService.PendingSnapshot());
    }

    [Fact]
    public void DamageDealtDuringAFlushStaysQueued()
    {
        Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl = "http://coordinator.example:5200";
        WorldRaidService.SetManifest(Raid(814, (814000, 10000)), new ExcelTableService());
        WorldRaidService.AddDamage(814000, 300);
        WorldRaidService.AddDamage(814000, 200);

        WorldRaidService.ConfirmFlushed(814000, 300);
        Assert.Equal(200, WorldRaidService.PendingSnapshot()[814000]);

        WorldRaidService.ConfirmFlushed(814000, 200);
        Assert.False(WorldRaidService.PendingSnapshot().ContainsKey(814000));
    }

    [Fact]
    public void ARemoteAnswerFromTheWrongSeasonIsIgnored()
    {
        WorldRaidService.SetManifest(Raid(814, (814000, 5000)), new ExcelTableService());

        WorldRaidService.ApplyRemoteState(new WorldRaidWorldState { seasonId = 823, bosses = { [814000] = new WorldRaidBossState { remainingHP = 1 } } });
        Assert.Equal(5000, WorldRaidService.RemainingHP(814000));

        WorldRaidService.ApplyRemoteState(new WorldRaidWorldState { seasonId = 814, bosses = { [814000] = new WorldRaidBossState { remainingHP = 42, participants = 7 } } });
        Assert.Equal(42, WorldRaidService.RemainingHP(814000));
        Assert.Equal(7, WorldRaidService.Participants(814000));
    }

    [Fact]
    public void ABossWithoutItsOwnWindowRunsTheWholeSeason()
    {
        var raid = Raid(814, (814000, 5000), (814100, 5000));
        raid.bosses[0].spawnTime = "2026-08-05 11:00:00";
        raid.bosses[0].eliminateTime = "2026-08-09 10:59:59";
        WorldRaidService.SetManifest(raid, new ExcelTableService());

        var scheduled = WorldRaidService.BossWindow(814000)!.Value;
        Assert.Equal(new DateTime(2026, 8, 5, 11, 0, 0), scheduled.Spawn);
        Assert.Equal(new DateTime(2026, 8, 9, 10, 59, 59), scheduled.Eliminate);

        var wholeSeason = WorldRaidService.BossWindow(814100)!.Value;
        Assert.Equal(new DateTime(2026, 8, 1, 11, 0, 0), wholeSeason.Spawn);
        Assert.Equal(new DateTime(2026, 8, 13, 10, 59, 59), wholeSeason.Eliminate);
    }

    [Fact]
    public void SeasonActiveEndsExactlyAtClose()
    {
        WorldRaidService.SetManifest(Raid(814, (814000, 5000)), new ExcelTableService());

        Assert.False(WorldRaidService.SeasonActive(new DateTime(2026, 7, 31, 12, 0, 0)));
        Assert.True(WorldRaidService.SeasonActive(new DateTime(2026, 8, 5, 12, 0, 0)));
        Assert.False(WorldRaidService.SeasonActive(new DateTime(2026, 8, 13, 10, 59, 59)));
    }

    [Fact]
    public void EndingTheRaidForgetsEverything()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Raid(814, (814000, 5000)), excel);
        WorldRaidService.SetManifest(null, excel);

        Assert.Null(WorldRaidService.Manifest);
        Assert.Null(WorldRaidService.RemainingHP(814000));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "worldraid_state.json"))));
    }

    private static WorldRaidManifest Raid(long seasonId, params (long groupId, long hp)[] bosses)
    {
        return new WorldRaidManifest
        {
            seasonId = seasonId,
            name = "test",
            open = "2026-08-01 11:00:00",
            close = "2026-08-13 10:59:59",
            bosses = bosses.Select(b => new WorldRaidManifestBoss { groupId = b.groupId, totalHP = b.hp }).ToList(),
        };
    }

    public void Dispose()
    {
        Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl = _savedUrl;
        ExcelTableService.DumpedDir = _originalDumpedDir;

        foreach (var (path, content) in _savedFiles)
        {
            if (content != null)
                File.WriteAllText(path, content);
            else
                File.Delete(path);
        }
        WorldRaidService.Load();

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }
}
