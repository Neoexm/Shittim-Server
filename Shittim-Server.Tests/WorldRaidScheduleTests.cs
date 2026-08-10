using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.FlatData;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// A live manifest hands its real window to the WorldRaid season rows EventScheduleService rewrites, and a manifest that names no bosses seeds its pools out of the world raid excel - both sides run against the same throwaway SQLCipher db shape the event schedule tests use.
[Collection("exceldb-paths")]
public class WorldRaidScheduleTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-wrsched-{Guid.NewGuid():N}");
    private readonly string _clientDb;
    private readonly string _originalDumpedDir = ExcelTableService.DumpedDir;
    private readonly Dictionary<string, string?> _savedFiles = new();

    public WorldRaidScheduleTests()
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
    public void TheManifestWindowLandsOnTheWorldRaidSeasonRows()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live814(), excel);
        EventScheduleService.Set([], excel);

        var rows = ReadBack().Where(r => r.EventContentId == 814).ToList();
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            Assert.Equal(Local("2026-07-30 11:00:00"), row.BeforehandExposedTime);
            Assert.Equal(Local("2026-08-01 11:00:00"), row.EventContentOpenTime);
            Assert.Equal(Local("2026-08-13 10:59:59"), row.EventContentCloseTime);
            // no extension in the manifest, so rewardable ends when the raid does
            Assert.Equal(Local("2026-08-13 10:59:59"), row.ExtensionTime);
            Assert.Equal("", row.EventContentCloseNoteTime);
        }

        var bystander = ReadBack().Single(r => r.EventContentId == 831);
        Assert.Equal("2024-06-04 11:00:00", bystander.EventContentOpenTime);
    }

    [Fact]
    public void EndingTheRaidPutsTheShippedDatesBack()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live814(), excel);
        EventScheduleService.Set([], excel);
        WorldRaidService.SetManifest(null, excel);
        EventScheduleService.Set([], excel);

        var row = ReadBack().Single(r => r.EventContentId == 814 && r.EventContentType == EventContentType.WorldRaid);
        Assert.Equal("2023-01-18 11:00:00", row.EventContentOpenTime);
        Assert.Equal("2023-01-31 10:59:59", row.EventContentCloseTime);
    }

    [Fact]
    public void ABareManifestSeedsItsPoolsOutOfTheExcel()
    {
        var raid = Live814();
        raid.bosses = [];
        WorldRaidService.SetManifest(raid, new ExcelTableService());

        // the asia column, not the jp one the row leads with
        Assert.Equal(3_000_000_000_000, WorldRaidService.RemainingHP(814000));
        Assert.Equal(3_000_000_000_000, WorldRaidService.RemainingHP(814100));
    }

    [Fact]
    public void AManifestPoolBeatsTheExcelOne()
    {
        var raid = Live814();
        raid.bosses = [new WorldRaidManifestBoss { groupId = 814000, totalHP = 50_000_000_000 }];
        WorldRaidService.SetManifest(raid, new ExcelTableService());

        Assert.Equal(50_000_000_000, WorldRaidService.RemainingHP(814000));
        Assert.Equal(3_000_000_000_000, WorldRaidService.RemainingHP(814100));
    }

    // manifest times are utc; the rows land in local wall clock, so expectations convert the same way
    private static string Local(string utc) => DateTime.Parse(utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static WorldRaidManifest Live814()
    {
        return new WorldRaidManifest
        {
            seasonId = 814,
            name = "test",
            open = "2026-08-01 11:00:00",
            close = "2026-08-13 10:59:59",
            exposed = "2026-07-30 11:00:00",
            bosses = [new WorldRaidManifestBoss { groupId = 814000, totalHP = 1_000_000 }],
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
                "CREATE TABLE WorldRaidSeasonManageDBSchema (Bytes BLOB); CREATE TABLE WorldRaidBossGroupDBSchema (Bytes BLOB)";
            create.ExecuteNonQuery();
        }

        foreach (var row in Seasons())
        {
            var fbb = new FlatBufferBuilder(512);
            fbb.Finish(EventContentSeasonExcel.Pack(fbb, row).Value);
            Insert(conn, "EventContentSeasonDBSchema", fbb.SizedByteArray());
        }

        var manage = new WorldRaidSeasonManageExcelT { SeasonId = 814, OpenRaidBossGroupId = [814000, 814100] };
        var mfbb = new FlatBufferBuilder(256);
        mfbb.Finish(WorldRaidSeasonManageExcel.Pack(mfbb, manage).Value);
        Insert(conn, "WorldRaidSeasonManageDBSchema", mfbb.SizedByteArray());

        foreach (var groupId in new long[] { 814000, 814100 })
        {
            var boss = new WorldRaidBossGroupExcelT { WorldRaidBossGroupId = groupId, WorldBossHP = 9_000_000_000_000, WorldBossHPAsia = 3_000_000_000_000 };
            var bfbb = new FlatBufferBuilder(256);
            bfbb.Finish(WorldRaidBossGroupExcel.Pack(bfbb, boss).Value);
            Insert(conn, "WorldRaidBossGroupDBSchema", bfbb.SizedByteArray());
        }

        SqliteConnection.ClearAllPools();
    }

    private static void Insert(SqliteConnection conn, string table, byte[] bytes)
    {
        using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO {table} (Bytes) VALUES (@b)";
        insert.Parameters.Add("@b", SqliteType.Blob).Value = bytes;
        insert.ExecuteNonQuery();
    }

    private static List<EventContentSeasonExcelT> Seasons()
    {
        return
        [
            Season(814, EventContentType.WorldRaid, "2023-01-11 11:00:00", "2023-01-18 11:00:00", "2023-01-31 10:59:59", "2023-02-07 10:59:59"),
            Season(814, EventContentType.WorldRaidEntrance, "2023-01-11 11:00:00", "2023-01-18 11:00:00", "2023-01-31 10:59:59", "2023-02-07 10:59:59"),
            Season(831, EventContentType.Stage, "2024-05-28 11:00:00", "2024-06-04 11:00:00", "2024-06-18 10:59:59", "2024-06-25 10:59:59"),
        ];
    }

    private static EventContentSeasonExcelT Season(long id, EventContentType type, string exposed, string open, string close, string extension)
    {
        return new EventContentSeasonExcelT
        {
            EventContentId = id,
            OriginalEventContentId = id,
            Name = $"Event_Name_{id}",
            EventContentType = type,
            BeforehandExposedTime = exposed,
            EventContentOpenTime = open,
            EventContentCloseNoteTime = "",
            EventContentCloseTime = close,
            ExtensionTime = extension,
            BeforehandScenarioGroupId = [],
        };
    }

    private List<EventContentSeasonExcelT> ReadBack()
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
