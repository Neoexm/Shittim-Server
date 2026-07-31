using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class ShopPurchaseLimitTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-30)]
    [InlineData(long.MinValue)]
    public void ANonPositiveCountIsRefused(long count)
    {
        // A non-positive count is what inverted the consume into a grant.
        var ex = Assert.Throws<WebAPIException>(() => ShopHandler.ValidatePurchaseCount(count));
        Assert.Equal(WebAPIErrorCode.ShopInvalidCostOrReward, ex.ErrorCode);
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MaxValue / 2)]
    [InlineData(ShopHandler.MaxPurchaseCountPerRequest + 1)]
    public void ACountBigEnoughToOverflowTheCostIsRefused(long count)
    {
        // The bound has to be two-sided: `> 0` alone still allows a count large enough that cost * count overflows to a negative number, which re-opens the same hole.
        var ex = Assert.Throws<WebAPIException>(() => ShopHandler.ValidatePurchaseCount(count));
        Assert.Equal(WebAPIErrorCode.ShopInvalidCostOrReward, ex.ErrorCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(ShopHandler.MaxPurchaseCountPerRequest)]
    public void RealisticCountsPass(long count)
    {
        ShopHandler.ValidatePurchaseCount(count);
    }

    [Fact]
    public void AShopThatNeverResetsIsASingleWindow()
    {
        Assert.Equal(
            DateTime.MinValue,
            ShopManager.PeriodStart(PurchaseCountResetType.None, new DateTime(2026, 7, 26, 12, 0, 0)));
    }

    [Theory]
    // The game day rolls at 04:00, so anything before it belongs to the previous day's window.
    [InlineData("2026-07-26T03:59:59", "2026-07-25")]
    [InlineData("2026-07-26T04:00:00", "2026-07-26")]
    [InlineData("2026-07-26T00:00:00", "2026-07-25")]
    [InlineData("2026-07-26T23:59:59", "2026-07-26")]
    public void TheDailyWindowRollsAtFourAm(string now, string expected)
    {
        Assert.Equal(
            DateTime.Parse(expected),
            ShopManager.PeriodStart(PurchaseCountResetType.Day, DateTime.Parse(now)));
    }

    [Theory]
    // Weeks start Monday. 2026-07-26 is a Sunday, so it belongs to the week beginning the 20th.
    [InlineData("2026-07-26T12:00:00", "2026-07-20")]
    [InlineData("2026-07-20T12:00:00", "2026-07-20")]
    [InlineData("2026-07-27T12:00:00", "2026-07-27")]
    // 04:00 Monday is the first moment of the new week; 03:00 Monday still belongs to the old one.
    [InlineData("2026-07-27T04:00:00", "2026-07-27")]
    [InlineData("2026-07-27T03:00:00", "2026-07-20")]
    public void WeeklyWindowsStartOnMonday(string now, string expected)
    {
        Assert.Equal(
            DateTime.Parse(expected),
            ShopManager.PeriodStart(PurchaseCountResetType.Week, DateTime.Parse(now)));
    }

    [Theory]
    [InlineData("2026-07-26T12:00:00", "2026-07-01")]
    // 03:00 on the 1st is still the previous month's last game day.
    [InlineData("2026-08-01T03:00:00", "2026-07-01")]
    [InlineData("2026-08-01T04:00:00", "2026-08-01")]
    public void MonthlyWindowsStartOnTheFirst(string now, string expected)
    {
        Assert.Equal(
            DateTime.Parse(expected),
            ShopManager.PeriodStart(PurchaseCountResetType.Month, DateTime.Parse(now)));
    }

    [Fact]
    public async Task TheCounterAccumulatesAcrossPurchasesAndTheCapIsEnforced()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var shop = ApShop(limit: 20);

        // First purchase of the window: the counter starts at zero.
        var history = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 3);
        Assert.Equal(0, history.PurchaseCount);

        history.PurchaseCount += 3;
        await db.SaveChangesAsync();

        // Re-reads the persisted counter rather than starting over.
        history = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 17);
        Assert.Equal(3, history.PurchaseCount);

        // 3 + 18 exceeds the cap of 20 and must be refused.
        var ex = await Assert.ThrowsAsync<WebAPIException>(
            () => ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 18));
        Assert.Equal(WebAPIErrorCode.ShopExceedPurchaseCountLimit, ex.ErrorCode);

        // Landing exactly on the cap is allowed.
        history = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 17);
        history.PurchaseCount += 17;
        await db.SaveChangesAsync();
        Assert.Equal(20, history.PurchaseCount);

        // And one more past it is not.
        await Assert.ThrowsAsync<WebAPIException>(
            () => ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 1));
    }

    [Fact]
    public async Task RollingIntoANewWindowZeroesTheSameRow()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var shop = ApShop(limit: 20);

        var history = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 20);
        history.PurchaseCount += 20;
        await db.SaveChangesAsync();

        // Exhausted for this window.
        await Assert.ThrowsAsync<WebAPIException>(
            () => ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 1));

        // Backdate the stored window to simulate the game day having rolled over. The row is kept and zeroed rather than deleted, so the account's counter stays a stable single row.
        history.PeriodStart = history.PeriodStart.AddDays(-1);
        await db.SaveChangesAsync();

        var rolled = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 20);
        Assert.Equal(0, rolled.PurchaseCount);
        Assert.Single(db.ShopPurchaseHistories);
    }

    [Fact]
    public async Task AZeroLimitMeansUncapped()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        // PurchaseCountLimit of 0 means "no limit" - most shop products are like this, and they must not start failing now that a counter exists.
        var shop = ApShop(limit: 0);

        var history = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 9999);
        history.PurchaseCount += 9999;
        await db.SaveChangesAsync();

        var again = await ShopManager.EnsurePurchasable(db, account, shop.Id, shop, 9999);
        Assert.Equal(9999, again.PurchaseCount);
    }

    [Fact]
    public async Task CountersAreKeptPerShopAndPerAccount()
    {
        using var db = NewContext();
        var first = NewAccount(db, serverId: 1);
        var second = NewAccount(db, serverId: 2);
        var shopA = ApShop(limit: 20, id: 21);
        var shopB = ApShop(limit: 20, id: 22);

        var a = await ShopManager.EnsurePurchasable(db, first, shopA.Id, shopA, 20);
        a.PurchaseCount += 20;
        await db.SaveChangesAsync();

        // Exhausting one shop must not exhaust another, nor the same shop for another account.
        var otherShop = await ShopManager.EnsurePurchasable(db, first, shopB.Id, shopB, 20);
        Assert.Equal(0, otherShop.PurchaseCount);

        var otherAccount = await ShopManager.EnsurePurchasable(db, second, shopA.Id, shopA, 20);
        Assert.Equal(0, otherAccount.PurchaseCount);
    }

    private static SchaleDataContext NewContext()
    {
        // A private file-backed SQLite database per test; EnsureCreated builds the schema from the model, which is also what proves ShopPurchaseHistories is actually registered on the context
        // (it was a new entity and the project has no migrations).
        var path = Path.Combine(Path.GetTempPath(), $"shittim-shoptest-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db, long serverId = 1)
    {
        var account = new AccountDBServer { ServerId = serverId, Nickname = $"Sensei{serverId}" };
        db.Accounts.Add(account);
        db.SaveChanges();
        return account;
    }

    private static ShopExcelT ApShop(long limit, long id = 21) => new()
    {
        Id = id,
        PurchaseCountLimit = limit,
        PurchaseCountResetType = PurchaseCountResetType.Day,
        CategoryType = ShopCategoryType.Ap
    };
}
