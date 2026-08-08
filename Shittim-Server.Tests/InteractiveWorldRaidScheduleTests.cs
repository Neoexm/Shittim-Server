using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.FlatData;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// 854-style seasons keep a second layer of dates in InteractiveWorldRaidSeasonManageExcel - per-phase windows plus per-boss spawn/eliminate lists - and their pools live in InteractiveWorldRaidBossGroupExcel. Same throwaway SQLCipher db shape as the classic world raid tests.
[Collection("exceldb-paths")]
public class InteractiveWorldRaidScheduleTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-iwr-{Guid.NewGuid():N}");
    private readonly string _clientDb;
    private readonly string _originalDumpedDir = ExcelTableService.DumpedDir;
    private readonly Dictionary<string, string?> _savedFiles = new();

    public InteractiveWorldRaidScheduleTests()
    {
        var dumped = Path.Combine(_dir, "Dumped");
        _clientDb = Path.Combine(_dir, "client", "ExcelDB.db");
        Directory.CreateDirectory(dumped);
        Directory.CreateDirectory(Path.GetDirectoryName(_clientDb)!);

        WriteDb(_clientDb);
        File.Copy(_clientDb, Path.Combine(dumped, "ExcelDB.db"));

        foreach (var name in new[] { "worldraid_manifest.json", "worldraid_state.json", "worldraid_pending.json", "event_schedule.json" })
        {
            var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", name));
            _savedFiles[path] = File.Exists(path) ? File.ReadAllText(path) : null;
        }

        ExcelTableService.DumpedDir = dumped;
        Environment.SetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY", Key);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH", _clientDb);
        WorldRaidService.SetManifest(null, new ExcelTableService());
    }

    [Fact]
    public void TheManifestWindowLandsOnTheInteractiveSeasonRow()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);

        var row = ReadSeasons().Single(r => r.EventContentId == 854);
        Assert.Equal("2026-08-08 11:00:00", row.BeforehandExposedTime);
        Assert.Equal("2026-08-10 11:00:00", row.EventContentOpenTime);
        Assert.Equal("2026-09-14 10:59:59", row.EventContentCloseTime);
        Assert.Equal("2026-09-21 10:59:59", row.ExtensionTime);
    }

    [Fact]
    public void ManifestBossWindowsLandOnThePhaseRows()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);

        var phases = ReadPhases();

        // no start condition means the phase opens with the season itself
        var opening = phases.Single(p => p.PhaseId == 85400);
        Assert.Equal("2026-08-10 11:00:00", opening.PhaseStartTime);
        Assert.Equal("2026-08-24 10:59:59", opening.PhaseEndTime);
        Assert.All(opening.BossSpawnTime, t => Assert.Equal("2026-08-11 11:00:00", t));
        Assert.All(opening.EliminateTime, t => Assert.Equal("2026-08-24 10:59:59", t));

        // conditioned phases open a day before their first boss, the lead day every shipped phase had
        var gated = phases.Single(p => p.PhaseId == 85401);
        Assert.Equal("2026-08-24 11:00:00", gated.PhaseStartTime);
        Assert.Equal("2026-09-14 10:59:59", gated.PhaseEndTime);
        Assert.All(gated.BossSpawnTime, t => Assert.Equal("2026-08-25 11:00:00", t));

        // the replay reopens everything once the season proper closes, and runs to the extension
        var replay = phases.Single(p => p.PhaseId == 85403);
        Assert.Equal("2026-09-14 10:59:59", replay.PhaseStartTime);
        Assert.Equal("2026-09-21 10:59:59", replay.PhaseEndTime);
        Assert.All(replay.BossSpawnTime, t => Assert.Equal("2026-09-14 10:59:59", t));
        Assert.All(replay.EliminateTime, t => Assert.Equal("2026-09-21 10:59:59", t));
    }

    [Fact]
    public void EndingTheRaidPutsTheShippedPhaseDatesBack()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);
        WorldRaidService.SetManifest(null, excel);
        EventScheduleService.Set([], excel);

        var opening = ReadPhases().Single(p => p.PhaseId == 85400);
        Assert.Equal("2026-05-26 11:00:00", opening.PhaseStartTime);
        Assert.Equal("2026-06-09 10:59:59", opening.PhaseEndTime);
        Assert.Equal("2026-05-27 11:00:00", opening.BossSpawnTime[0]);

        var replay = ReadPhases().Single(p => p.PhaseId == 85403);
        Assert.Equal("2026-07-14 11:00:00", replay.PhaseStartTime);
        Assert.Equal("2999-12-31 23:59:59", replay.PhaseEndTime);
    }

    [Fact]
    public void ABareManifestSeedsPoolsOutOfTheInteractiveGroups()
    {
        var raid = Live854();
        raid.bosses = [];
        WorldRaidService.SetManifest(raid, new ExcelTableService());

        // global pool where the row has one, jp where it doesn't - the steam client is the global build
        Assert.Equal(1_270_000_000_000, WorldRaidService.RemainingHP(8540000));
        Assert.Equal(7_500_000_000_000, WorldRaidService.RemainingHP(8540100));
        // listed only in the second phase - the season's groups are the union of every phase's
        Assert.Equal(43_200_000_000_000, WorldRaidService.RemainingHP(8540800));
        Assert.Equal(43_200_000_000_000, WorldRaidService.RemainingHP(8540900));
    }

    private static WorldRaidManifest Live854()
    {
        return new WorldRaidManifest
        {
            seasonId = 854,
            name = "test",
            open = "2026-08-10 11:00:00",
            close = "2026-09-14 10:59:59",
            exposed = "2026-08-08 11:00:00",
            extension = "2026-09-21 10:59:59",
            bosses =
            [
                new WorldRaidManifestBoss { groupId = 8540000, spawnTime = "2026-08-11 11:00:00", eliminateTime = "2026-08-24 10:59:59" },
                new WorldRaidManifestBoss { groupId = 8540100, spawnTime = "2026-08-11 11:00:00", eliminateTime = "2026-08-24 10:59:59" },
                new WorldRaidManifestBoss { groupId = 8540800, spawnTime = "2026-08-25 11:00:00", eliminateTime = "2026-09-14 10:59:59" },
                new WorldRaidManifestBoss { groupId = 8540900, spawnTime = "2026-08-25 11:00:00", eliminateTime = "2026-09-14 10:59:59" },
            ],
        };
    }

    private static void WriteDb(string path)
    {
        SqliteProvider.EnsureInitialized();

        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        conn.Open();

        using (var key = conn.CreateCommand())
        {
            key.CommandText = $"PRAGMA key = \"x'{Key}'\";";
            key.ExecuteNonQuery();
        }

        using (var create = conn.CreateCommand())
        {
            // Apply widens the event item expiry out of the same file, so ItemDBSchema has to be there even with nothing in it.
            create.CommandText = "CREATE TABLE EventContentSeasonDBSchema (Bytes BLOB); CREATE TABLE ItemDBSchema (Bytes BLOB); " +
                "CREATE TABLE WorldRaidSeasonManageDBSchema (Bytes BLOB); CREATE TABLE WorldRaidBossGroupDBSchema (Bytes BLOB); " +
                "CREATE TABLE InteractiveWorldRaidSeasonManageDBSchema (Bytes BLOB); CREATE TABLE InteractiveWorldRaidBossGroupDBSchema (Bytes BLOB)";
            create.ExecuteNonQuery();
        }

        var season = new EventContentSeasonExcelT
        {
            EventContentId = 854,
            OriginalEventContentId = 854,
            Name = "Event_Name_854",
            EventContentType = EventContentType.InteractiveWorldRaid,
            BeforehandExposedTime = "2026-05-24 11:00:00",
            EventContentOpenTime = "2026-05-26 11:00:00",
            EventContentCloseNoteTime = "",
            EventContentCloseTime = "2026-06-30 10:59:59",
            ExtensionTime = "2026-07-14 10:59:59",
            BeforehandScenarioGroupId = [],
        };
        var sfbb = new FlatBufferBuilder(512);
        sfbb.Finish(EventContentSeasonExcel.Pack(sfbb, season).Value);
        Insert(conn, "EventContentSeasonDBSchema", sfbb.SizedByteArray());

        var phases = new[]
        {
            Phase(85400, 0, false, CurrencyTypes.WorldRaidTicketA, [8540000, 8540100], "2026-05-26 11:00:00", "2026-06-09 10:59:59", "2026-05-27 11:00:00", "2026-06-09 10:59:59"),
            Phase(85401, 854010000, false, CurrencyTypes.WorldRaidTicketB, [8540800, 8540900], "2026-06-09 11:00:00", "2026-06-23 10:59:59", "2026-06-10 11:00:00", "2026-06-23 10:59:59"),
            Phase(85403, 854030000, true, CurrencyTypes.WorldRaidTicketC, [8540000, 8540100, 8540800, 8540900], "2026-07-14 11:00:00", "2999-12-31 23:59:59", "2026-07-14 11:00:00", "2999-12-31 23:59:59"),
        };
        foreach (var phase in phases)
        {
            var pfbb = new FlatBufferBuilder(512);
            pfbb.Finish(InteractiveWorldRaidSeasonManageExcel.Pack(pfbb, phase).Value);
            Insert(conn, "InteractiveWorldRaidSeasonManageDBSchema", pfbb.SizedByteArray());
        }

        var groups = new[]
        {
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540000, WorldBossHP = 8_250_000_000_000, WorldBossHPGlobal = 1_270_000_000_000 },
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540100, WorldBossHP = 7_500_000_000_000 },
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540800, WorldBossHPLinkGroup = 8540800, WorldBossHP = 270_000_000_000_000, WorldBossHPGlobal = 43_200_000_000_000 },
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540900, WorldBossHPLinkGroup = 8540800, WorldBossHP = 270_000_000_000_000, WorldBossHPGlobal = 43_200_000_000_000 },
        };
        foreach (var group in groups)
        {
            var gfbb = new FlatBufferBuilder(256);
            gfbb.Finish(InteractiveWorldRaidBossGroupExcel.Pack(gfbb, group).Value);
            Insert(conn, "InteractiveWorldRaidBossGroupDBSchema", gfbb.SizedByteArray());
        }

        SqliteConnection.ClearAllPools();
    }

    private static InteractiveWorldRaidSeasonManageExcelT Phase(long phaseId, long condition, bool replay, CurrencyTypes ticket, List<long> groups, string start, string end, string spawn, string eliminate)
    {
        return new InteractiveWorldRaidSeasonManageExcelT
        {
            SeasonId = 854,
            PhaseId = phaseId,
            PhaseStartCondition = condition,
            IsReplaySeason = replay,
            EnterTicket = ticket,
            PhaseStartTime = start,
            PhaseEndTime = end,
            OpenRaidBossGroupId = groups,
            BossSpawnTime = groups.Select(_ => spawn).ToList(),
            EliminateTime = groups.Select(_ => eliminate).ToList(),
        };
    }

    private static void Insert(SqliteConnection conn, string table, byte[] bytes)
    {
        using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO {table} (Bytes) VALUES (@b)";
        insert.Parameters.Add("@b", SqliteType.Blob).Value = bytes;
        insert.ExecuteNonQuery();
    }

    private List<EventContentSeasonExcelT> ReadSeasons()
    {
        SqliteConnection.ClearAllPools();

        using var conn = ExcelTableService.OpenExcelDbConnection(_clientDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Bytes FROM EventContentSeasonDBSchema ORDER BY rowid";

        var rows = new List<EventContentSeasonExcelT>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(EventContentSeasonExcel.GetRootAsEventContentSeasonExcel(new ByteBuffer((byte[])reader[0])).UnPack());
        return rows;
    }

    private List<InteractiveWorldRaidSeasonManageExcelT> ReadPhases()
    {
        SqliteConnection.ClearAllPools();

        using var conn = ExcelTableService.OpenExcelDbConnection(_clientDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Bytes FROM InteractiveWorldRaidSeasonManageDBSchema ORDER BY rowid";

        var rows = new List<InteractiveWorldRaidSeasonManageExcelT>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(InteractiveWorldRaidSeasonManageExcel.GetRootAsInteractiveWorldRaidSeasonManageExcel(new ByteBuffer((byte[])reader[0])).UnPack());
        return rows;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY", null);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH", null);
        ExcelTableService.DumpedDir = _originalDumpedDir;

        foreach (var (path, content) in _savedFiles)
        {
            if (content != null)
                File.WriteAllText(path, content);
            else
                File.Delete(path);
        }
        WorldRaidService.Load();

        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }
}
