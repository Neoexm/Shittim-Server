using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Xunit;

namespace Shittim_Server.Tests;

/// <summary>
/// The server has no EF migrations and relies on <c>EnsureCreated()</c>, which builds the schema for a
/// brand-new database and then never touches an existing one again. Every entity added after a
/// player's database was created therefore needed a hand-written <c>CREATE TABLE IF NOT EXISTS</c> in
/// GameServer, and a new *column* on an existing entity had no escape hatch at all — it simply went
/// missing and every query touching it failed at runtime.
///
/// These tests hold <see cref="SchemaReconciler"/> to the contract that replaced those blocks: it must
/// close both gaps from the model alone, and be safe to run on every startup.
/// </summary>
public class SchemaReconcilerTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"shittim-schematest-{Guid.NewGuid():N}.sqlite3");

    [Fact]
    public async Task AddsATableMissingFromAnExistingDatabase()
    {
        // Stand up a database, then drop a table to stand in for "this entity did not exist when the
        // player's database was created".
        using (var seed = NewContext())
        {
            seed.Database.EnsureCreated();
            await seed.Database.ExecuteSqlRawAsync("DROP TABLE \"ShopPurchaseHistories\"");
            Assert.DoesNotContain("ShopPurchaseHistories", await TableNames(seed));
        }

        using var db = NewContext();
        var applied = await SchemaReconciler.ReconcileAsync(db);

        Assert.Contains("created table ShopPurchaseHistories", applied);
        Assert.Contains("ShopPurchaseHistories", await TableNames(db));

        // And the recreated table is actually usable, not just present.
        db.ShopPurchaseHistories.Add(new Schale.Data.GameModel.ShopPurchaseHistoryDBServer
        {
            AccountServerId = 1,
            ShopUniqueId = 21,
            PurchaseCount = 3,
            PeriodStart = new DateTime(2026, 7, 26)
        });
        await db.SaveChangesAsync();
        Assert.Equal(3, db.ShopPurchaseHistories.Single().PurchaseCount);
    }

    [Fact]
    public async Task AddsAColumnMissingFromAnExistingTable()
    {
        // The case the old hand-written CREATE TABLE blocks could not cover: the table exists, but a
        // property was added to the entity afterwards. SQLite cannot drop a column, so the table is
        // rebuilt without it to simulate the older schema.
        using (var seed = NewContext())
        {
            seed.Database.EnsureCreated();
            await seed.Database.ExecuteSqlRawAsync("DROP TABLE \"ShopPurchaseHistories\"");
            await seed.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE ""ShopPurchaseHistories"" (
                    ""ServerId"" INTEGER NOT NULL CONSTRAINT ""PK_ShopPurchaseHistories"" PRIMARY KEY AUTOINCREMENT,
                    ""AccountServerId"" INTEGER NOT NULL,
                    ""ShopUniqueId"" INTEGER NOT NULL,
                    ""PurchaseCount"" INTEGER NOT NULL
                )");
            await seed.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"ShopPurchaseHistories\" (\"AccountServerId\", \"ShopUniqueId\", \"PurchaseCount\") VALUES (1, 21, 7)");

            Assert.DoesNotContain("PeriodStart", await ColumnNames(seed, "ShopPurchaseHistories"));
        }

        using var db = NewContext();
        var applied = await SchemaReconciler.ReconcileAsync(db);

        Assert.Contains("added column ShopPurchaseHistories.PeriodStart", applied);
        Assert.Contains("PeriodStart", await ColumnNames(db, "ShopPurchaseHistories"));

        // The pre-existing row survives and reads back through EF, which is the whole point: a
        // required column added to a populated table needs a default or SQLite refuses it.
        var row = db.ShopPurchaseHistories.Single();
        Assert.Equal(7, row.PurchaseCount);
        Assert.Equal(1, row.AccountServerId);
    }

    [Fact]
    public async Task IsANoOpOnAnUpToDateDatabase()
    {
        // Runs on every startup, so a database matching the model must produce no statements at all.
        using var db = NewContext();
        db.Database.EnsureCreated();

        Assert.Empty(await SchemaReconciler.ReconcileAsync(db));
    }

    [Fact]
    public async Task IsIdempotent()
    {
        using (var seed = NewContext())
        {
            seed.Database.EnsureCreated();
            await seed.Database.ExecuteSqlRawAsync("DROP TABLE \"ShopPurchaseHistories\"");
        }

        using var db = NewContext();
        Assert.NotEmpty(await SchemaReconciler.ReconcileAsync(db));

        // A second pass has nothing left to do; if it re-issued the CREATE it would throw.
        Assert.Empty(await SchemaReconciler.ReconcileAsync(db));
    }

    // ------------------------------------------------------------------------------ fixtures

    private SchaleDataContext NewContext() => new(
        new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={_path}").Options);

    private static async Task<List<string>> TableNames(SchaleDataContext db) =>
        await db.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();

    private static async Task<List<string>> ColumnNames(SchaleDataContext db, string table) =>
        await db.Database.SqlQueryRaw<string>(
            $"SELECT name AS Value FROM pragma_table_info('{table}')").ToListAsync();

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
