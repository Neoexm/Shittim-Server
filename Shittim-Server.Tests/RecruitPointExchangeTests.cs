using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The recruitment point exchange rides Shop_BuyGacha3 like every other banner buy, and the only thing separating it from one is the RecruitSellection goods row. Miss that and the coin item has no GachaTicket type, the amount-by-item lookup falls through to 10, and a student purchase comes out a ten-pull.
public class RecruitPointExchangeTests
{
    [Fact]
    public async Task SpendingRecruitPointsGrantsTheChosenStudentInsteadOfTenPulls()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var (goods, coinId, coinAmount, characterId) = ExchangeGoods();
        db.Items.Add(new ItemDBServer { AccountServerId = account.ServerId, UniqueId = coinId, StackCount = coinAmount + 37 });
        db.SaveChanges();

        var result = await Manager().TryBuyStudent(db, account, new ShopBuyGacha3Request
        {
            GoodsId = goods.Id,
            Cost = new ParcelCost
            {
                ParcelInfos = [new ParcelInfo { Key = new ParcelKeyPair { Type = ParcelType.Item, Id = coinId }, Amount = coinAmount }]
            }
        });

        Assert.NotNull(result);
        var (_, acquired, gachaResults) = result.Value;
        var single = Assert.Single(gachaResults);
        Assert.Equal(characterId, single.CharacterId);
        Assert.Contains(db.Characters, x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
        Assert.Equal(37, db.Items.Single(x => x.AccountServerId == account.ServerId && x.UniqueId == coinId).StackCount);
        // the client reads the remaining coin count back from AcquiredItems
        Assert.Contains(acquired, x => x.UniqueId == coinId && x.StackCount == 37);
    }

    [Fact]
    public async Task ExchangingAnOwnedStudentPaysOutStonesNotADuplicateRow()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var (goods, coinId, coinAmount, characterId) = ExchangeGoods();
        db.Characters.Add(new CharacterDBServer { AccountServerId = account.ServerId, UniqueId = characterId, StarGrade = 3 });
        db.Items.Add(new ItemDBServer { AccountServerId = account.ServerId, UniqueId = coinId, StackCount = coinAmount });
        db.SaveChanges();

        var result = await Manager().TryBuyStudent(db, account, new ShopBuyGacha3Request
        {
            GoodsId = goods.Id,
            Cost = new ParcelCost
            {
                ParcelInfos = [new ParcelInfo { Key = new ParcelKeyPair { Type = ParcelType.Item, Id = coinId }, Amount = coinAmount }]
            }
        });

        var single = Assert.Single(result!.Value.Item3);
        Assert.NotNull(single.Stone);
        Assert.Single(db.Characters, x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
    }

    [Fact]
    public async Task ATenPullGoodsIsStillRolledNotExchanged()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var banner = Excels.GetTable<ShopRecruitExcelT>().First(x => x.TenGachaGoodsId != 0);
        var result = await Manager().TryBuyStudent(db, account, new ShopBuyGacha3Request
        {
            ShopUniqueId = banner.Id,
            GoodsId = banner.TenGachaGoodsId
        });

        Assert.Null(result);
    }

    private static (GoodsExcelT, long coinId, long coinAmount, long characterId) ExchangeGoods()
    {
        var shops = Excels.GetTable<ShopExcelT>();
        var goods = Excels.GetTable<GoodsExcelT>();
        var released = Excels.GetTable<CharacterExcelT>().GetReleaseCharacters().Select(x => x.Id).ToHashSet();

        foreach (var banner in Excels.GetTable<ShopRecruitExcelT>().Where(x => x.RecruitSellectionShopId != 0))
        {
            var shop = shops.FirstOrDefault(x => x.Id == banner.RecruitSellectionShopId);
            if (shop == null) continue;

            foreach (var goodsId in shop.GoodsId ?? [])
            {
                var g = goods.FirstOrDefault(x => x.Id == goodsId);
                if (g == null || g.ParcelType == null) continue;

                var slot = g.ParcelType.IndexOf(ParcelType.Character);
                if (slot < 0 || !released.Contains(g.ParcelId[slot])) continue;

                var coinSlot = g.ConsumeParcelType.IndexOf(ParcelType.Item);
                if (coinSlot < 0) continue;

                return (g, g.ConsumeParcelId[coinSlot], g.ConsumeParcelAmount[coinSlot], g.ParcelId[slot]);
            }
        }

        throw new InvalidOperationException("no recruit point exchange goods in the dumped excels");
    }

    private static readonly ExcelTableService Excels = LoadExcels();

    private static ExcelTableService LoadExcels()
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

        return new ExcelTableService();
    }

    private static ShopManager Manager() =>
        new(Excels, new SharedDataCacheService(Excels), new ParcelHandler(Excels, Mapper), Mapper);

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-recruitexchange-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db, long serverId = 1)
    {
        var account = new AccountDBServer { ServerId = serverId, Nickname = $"Sensei{serverId}" };
        db.Accounts.Add(account);
        db.Currencies.Add(new AccountCurrencyDBServer(serverId));
        db.SaveChanges();
        return account;
    }

    private static readonly IMapper Mapper = BuildMapper();

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
