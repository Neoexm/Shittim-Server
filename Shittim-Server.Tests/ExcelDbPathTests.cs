using System.Text;
using BlueArchiveAPI.Services;
using Xunit;

namespace Shittim_Server.Tests;

// ExcelTableService.ResourceDir and DumpedDir are where every other Excel test reads the shipped tables from, so pointing them at a fixture has to stop the rest of the suite rather than race it.
[CollectionDefinition("exceldb-paths", DisableParallelization = true)]
public class ExcelDbPathCollection
{
}

// Resources/Downloaded is the staging copy and Resources/Dumped is what the server reads.
// Getting a file into the wrong one of those, or getting the wrong file into the right one, both end at SQLite error 26 blaming the SQLCipher key.
[Collection("exceldb-paths")]
public class ExcelDbPathTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-exceldb-{Guid.NewGuid():N}");
    private readonly string _resourceDir;
    private readonly string _dumped;
    private readonly string _downloaded;
    private readonly string _originalResourceDir = ExcelTableService.ResourceDir;
    private readonly string _originalDumpedDir = ExcelTableService.DumpedDir;

    public ExcelDbPathTests()
    {
        _resourceDir = Path.Combine(_dir, "Resources");
        _dumped = Path.Combine(_resourceDir, "Dumped");
        _downloaded = Path.Combine(_resourceDir, "Downloaded");
        Directory.CreateDirectory(_dumped);
        Directory.CreateDirectory(_downloaded);

        ExcelTableService.ResourceDir = _resourceDir;
        ExcelTableService.DumpedDir = _dumped;
    }

    // A zip that got renamed, a CDN error page saved as the database, a half-finished download. All of them reach the same PRAGMA key and the same error 26, and the message sends the reader off to re-extract a key that was fine.
    [Fact]
    public void AFileThatIsNotADatabaseIsNotBlamedOnTheKey()
    {
        File.WriteAllBytes(Path.Combine(_dumped, "ExcelDB.db"), [0x50, 0x4B, 0x03, 0x04, .. new byte[2048]]);

        var ex = Assert.Throws<InvalidOperationException>(ExcelTableService.ValidateExcelDbKey);

        Assert.Contains("ExcelDB.db", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("key most likely rotated", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnHtmlErrorPageSavedAsTheDatabaseSaysWhatItActuallyIs()
    {
        File.WriteAllText(Path.Combine(_dumped, "ExcelDB.db"), "<!DOCTYPE html>" + new string('x', 600));

        var ex = Assert.Throws<InvalidOperationException>(ExcelTableService.ValidateExcelDbKey);

        Assert.DoesNotContain("key most likely rotated", ex.Message, StringComparison.Ordinal);
    }

    // The download landed but the copy into Dumped never happened. Every table then loads empty and the server starts up looking healthy, so the file one directory over has to be named.
    [Fact]
    public void AnExcelDbLeftInTheDownloadDirectoryIsPointedAt()
    {
        File.WriteAllBytes(Path.Combine(_downloaded, "ExcelDB.db"), PlainSqlite());

        var ex = Assert.Throws<InvalidOperationException>(ExcelTableService.ValidateExcelDbKey);

        Assert.Contains(_downloaded, ex.Message, StringComparison.Ordinal);
        Assert.Contains(_dumped, ex.Message, StringComparison.Ordinal);
    }

    // A .bytes-only setup is a supported layout, so an absent ExcelDB.db with Excel tables beside it is not an error.
    [Fact]
    public void ABytesOnlySetupIsLeftAlone()
    {
        Directory.CreateDirectory(Path.Combine(_dumped, "Excel"));
        File.WriteAllBytes(Path.Combine(_dumped, "Excel", "charactertable.bytes"), new byte[64]);
        File.WriteAllBytes(Path.Combine(_downloaded, "ExcelDB.db"), PlainSqlite());

        ExcelTableService.ValidateExcelDbKey();
    }

    [Fact]
    public void APlainSqliteExcelDbNeedsNoKey()
    {
        File.WriteAllBytes(Path.Combine(_dumped, "ExcelDB.db"), PlainSqlite());

        ExcelTableService.ValidateExcelDbKey();
    }

    private static byte[] PlainSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-plain-{Guid.NewGuid():N}.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE CharacterDBSchema (Bytes BLOB)";
            command.ExecuteNonQuery();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var bytes = File.ReadAllBytes(path);
        File.Delete(path);
        return bytes;
    }

    public void Dispose()
    {
        ExcelTableService.ResourceDir = _originalResourceDir;
        ExcelTableService.DumpedDir = _originalDumpedDir;

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }
}
