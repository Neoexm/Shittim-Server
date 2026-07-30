using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Xunit;

namespace Shittim_Server.Tests;

// The server has no EF migrations and relies on EnsureCreated(), which builds the schema for a brand-new database and never touches an existing one again. SchemaReconciler closes both the missing-table and the missing-column case from the EF model alone, and has to stay safe to run on every startup.
public class SchemaReconcilerTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"shittim-schematest-{Guid.NewGuid():N}.sqlite3");

    [Fact]
    public async Task AddsATableMissingFromAnExistingDatabase()
    {
        // Stand up a database, then drop a table to stand in for "this entity did not exist when the player's database was created".
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
        // The case CREATE TABLE IF NOT EXISTS blocks cannot cover: the table exists, but a property was added to the entity afterwards. SQLite cannot drop a column, so the table is rebuilt without it to simulate the older schema.
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

        // A required column added to a populated table needs a default or SQLite refuses.
        var row = db.ShopPurchaseHistories.Single();
        Assert.Equal(7, row.PurchaseCount);
        Assert.Equal(1, row.AccountServerId);
    }

    [Fact]
    public async Task IsANoOpOnAnUpToDateDatabase()
    {
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

        Assert.Empty(await SchemaReconciler.ReconcileAsync(db));
    }

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
