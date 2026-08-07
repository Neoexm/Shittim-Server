using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.FlatData;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// Switching an event on rewrites the dates in the installed client's ExcelDB, and switching it off has to put back exactly what shipped - so both directions run here against a throwaway SQLCipher database with the same table name and row shape the client uses.
[Collection("exceldb-paths")]
public class EventSchedulePatchTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-evsched-{Guid.NewGuid():N}");
    private readonly string _clientDb;
    private readonly string _dumped;
    private readonly string _originalDumpedDir = ExcelTableService.DumpedDir;
    private readonly string _stateFile = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "event_schedule.json"));
    private readonly string? _savedState;

    public EventSchedulePatchTests()
    {
        _dumped = Path.Combine(_dir, "Dumped");
        _clientDb = Path.Combine(_dir, "client", "ExcelDB.db");
        Directory.CreateDirectory(_dumped);
        Directory.CreateDirectory(Path.GetDirectoryName(_clientDb)!);

        WriteSeasonDb(_clientDb, Shipped());
        File.Copy(_clientDb, Path.Combine(_dumped, "ExcelDB.db"));

        if (File.Exists(_stateFile))
            _savedState = File.ReadAllText(_stateFile);

        ExcelTableService.DumpedDir = _dumped;
        Environment.SetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY", Key);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH", _clientDb);
    }

    [Fact]
    public void ForcingAnEventOpenWidensEveryOneOfItsSeasonRows()
    {
        EventScheduleService.Set([828L], new ExcelTableService());

        foreach (var row in ReadBack().Where(r => r.EventContentId == 828))
        {
            Assert.Equal("2020-01-01 00:00:00", row.BeforehandExposedTime);
            Assert.Equal("2020-01-01 00:00:00", row.EventContentOpenTime);
            Assert.Equal("2099-12-31 23:59:59", row.EventContentCloseTime);
            Assert.Equal("2099-12-31 23:59:59", row.ExtensionTime);
            Assert.Equal("", row.EventContentCloseNoteTime);
        }
    }

    [Fact]
    public void EventsThatWereNotAskedForKeepTheirDates()
    {
        EventScheduleService.Set([828L], new ExcelTableService());

        var untouched = ReadBack().Single(r => r.EventContentId == 831);
        Assert.Equal("2024-06-04 11:00:00", untouched.EventContentOpenTime);
        Assert.Equal("2024-06-18 10:59:59", untouched.EventContentCloseTime);
    }

    // Everything but the five date strings has to survive the unpack-repack round trip, or switching an event on quietly rewrites the rest of the row.
    [Fact]
    public void TheRestOfTheRowSurvivesTheRewrite()
    {
        EventScheduleService.Set([828L], new ExcelTableService());

        var stage = ReadBack().Single(r => r.EventContentId == 828 && r.EventContentType == EventContentType.Stage);
        Assert.Equal("Event_Name_828", stage.Name);
        Assert.True(stage.EventDisplay);
        Assert.Equal(EventContentReleaseType.None, stage.EventContentReleaseType);
        Assert.Equal(9828, stage.EventItemId);
    }

    [Fact]
    public void SwitchingEverythingBackOffRestoresTheShippedDates()
    {
        var excel = new ExcelTableService();
        EventScheduleService.Set([828L, 831L], excel);
        EventScheduleService.Set([], excel);

        var restored = ReadBack().ToDictionary(r => (r.EventContentId, r.EventContentType));
        foreach (var original in Shipped())
        {
            var row = restored[(original.EventContentId, original.EventContentType)];
            Assert.Equal(original.BeforehandExposedTime, row.BeforehandExposedTime);
            Assert.Equal(original.EventContentOpenTime, row.EventContentOpenTime);
            Assert.Equal(original.EventContentCloseNoteTime, row.EventContentCloseNoteTime);
            Assert.Equal(original.EventContentCloseTime, row.EventContentCloseTime);
            Assert.Equal(original.ExtensionTime, row.ExtensionTime);
        }
    }

    [Fact]
    public void ApplyingTheSameScheduleTwiceChangesNothing()
    {
        var excel = new ExcelTableService();
        EventScheduleService.Set([828L], excel);
        var first = ReadRaw();
        EventScheduleService.Apply(excel);

        Assert.Equal(first, ReadRaw());
    }

    private static List<EventContentSeasonExcelT> Shipped()
    {
        return
        [
            Season(828, EventContentType.Stage, "2024-05-14 11:00:00", "2024-05-21 11:00:00", "2024-06-04 10:59:59", "2024-06-11 10:59:59", "2024-06-01 11:00:00", display: true),
            Season(828, EventContentType.Shop, "2024-05-21 11:00:00", "2024-05-21 11:00:00", "2024-06-04 10:59:59", "2024-06-11 10:59:59", ""),
            Season(828, EventContentType.MiniGameDefense, "2024-05-21 11:00:00", "2024-05-21 11:00:00", "2024-06-04 10:59:59", "2024-06-11 10:59:59", ""),
            Season(831, EventContentType.Stage, "2024-05-28 11:00:00", "2024-06-04 11:00:00", "2024-06-18 10:59:59", "2024-06-25 10:59:59", "2024-06-15 11:00:00", display: true),
        ];
    }

    private static EventContentSeasonExcelT Season(long id, EventContentType type, string exposed, string open, string close, string extension, string closeNote, bool display = false)
    {
        return new EventContentSeasonExcelT
        {
            EventContentId = id,
            OriginalEventContentId = id,
            Name = $"Event_Name_{id}",
            EventContentType = type,
            EventDisplay = display,
            EventItemId = 9000 + id,
            EventContentReleaseType = EventContentReleaseType.None,
            BeforehandExposedTime = exposed,
            EventContentOpenTime = open,
            EventContentCloseNoteTime = closeNote,
            EventContentCloseTime = close,
            ExtensionTime = extension,
            BeforehandScenarioGroupId = [],
        };
    }

    private static void WriteSeasonDb(string path, List<EventContentSeasonExcelT> rows)
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
            create.CommandText = "CREATE TABLE EventContentSeasonDBSchema (Bytes BLOB); CREATE TABLE ItemDBSchema (Bytes BLOB)";
            create.ExecuteNonQuery();
        }

        foreach (var row in rows)
        {
            var fbb = new FlatBufferBuilder(512);
            fbb.Finish(EventContentSeasonExcel.Pack(fbb, row).Value);

            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO EventContentSeasonDBSchema (Bytes) VALUES (@b)";
            insert.Parameters.Add("@b", SqliteType.Blob).Value = fbb.SizedByteArray();
            insert.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
    }

    private List<EventContentSeasonExcelT> ReadBack()
    {
        return ReadRaw().Select(b => EventContentSeasonExcel.GetRootAsEventContentSeasonExcel(new ByteBuffer(b)).UnPack()).ToList();
    }

    private List<byte[]> ReadRaw()
    {
        SqliteConnection.ClearAllPools();

        using var conn = ExcelTableService.OpenExcelDbConnection(_clientDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Bytes FROM EventContentSeasonDBSchema ORDER BY rowid";

        var rows = new List<byte[]>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add((byte[])reader[0]);
        return rows;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY", null);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH", null);
        ExcelTableService.DumpedDir = _originalDumpedDir;

        if (_savedState != null)
            File.WriteAllText(_stateFile, _savedState);
        else
            File.Delete(_stateFile);

        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }
}
