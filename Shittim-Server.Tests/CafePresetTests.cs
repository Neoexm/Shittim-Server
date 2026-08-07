using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class CafePresetTests
{
    [Fact]
    public async Task ApplyingATemplateBringsItsOwnFloorWallpaperAndBackground()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var cafe = NewCafe(db, account);

        var elements = Excels.GetTable<FurnitureTemplateElementExcelT>().Where(x => x.FurnitureTemplateId == 1).ToList();

        await Manager().CafeApplyTemplate(db, account, new CafeApplyTemplateRequest { CafeDBId = cafe.CafeDBId, TemplateId = 1 });

        var deployed = db.Furnitures.Where(x => x.AccountServerId == account.ServerId && x.ItemDeploySequence != 0).ToList();
        Assert.Equal(elements.Count, deployed.Count);
        Assert.All(deployed, x => Assert.Equal(cafe.CafeDBId, x.CafeDBId));

        var furnById = Excels.GetTable<FurnitureExcelT>().ToDictionary(x => x.Id);
        foreach (var sub in new[] { FurnitureSubCategory.Floor, FurnitureSubCategory.Wallpaper, FurnitureSubCategory.Background })
            Assert.Contains(deployed, x => furnById[x.UniqueId].SubCategory == sub);
    }

    [Fact]
    public async Task ApplyingATemplateConsumesAnOwnedStackWithoutDuplicatingTheRow()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var cafe = NewCafe(db, account);

        var background = Excels.GetTable<FurnitureTemplateElementExcelT>()
            .Where(x => x.FurnitureTemplateId == 1)
            .First(x => Excels.GetTable<FurnitureExcelT>().First(f => f.Id == x.FurnitureId).SubCategory == FurnitureSubCategory.Background);

        db.Furnitures.Add(new FurnitureDBServer { AccountServerId = account.ServerId, UniqueId = background.FurnitureId, Location = FurnitureLocation.Inventory, StackCount = 1, ItemDeploySequence = 0 });
        db.SaveChanges();

        await Manager().CafeApplyTemplate(db, account, new CafeApplyTemplateRequest { CafeDBId = cafe.CafeDBId, TemplateId = 1 });

        var rows = db.Furnitures.Where(x => x.AccountServerId == account.ServerId && x.UniqueId == background.FurnitureId).ToList();
        Assert.Single(rows);
        Assert.Equal(cafe.CafeDBId, rows[0].CafeDBId);
        Assert.Empty(db.Furnitures.Where(x => x.AccountServerId == account.ServerId && x.ItemDeploySequence != 0 && x.CafeDBId == 0));
    }

    [Fact]
    public async Task RemoveAllLeavesTheInteriorsAlone()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var cafe = NewCafe(db, account);

        var furniture = Excels.GetTable<FurnitureExcelT>();
        var floorCovering = furniture.First(x => x.SubCategory == FurnitureSubCategory.Floor);
        var chair = furniture.First(x => x.SubCategory == FurnitureSubCategory.Chair);

        db.Furnitures.Add(new FurnitureDBServer { AccountServerId = account.ServerId, CafeDBId = cafe.CafeDBId, UniqueId = floorCovering.Id, Location = FurnitureLocation.Floor, StackCount = 1, ItemDeploySequence = 11 });
        db.Furnitures.Add(new FurnitureDBServer { AccountServerId = account.ServerId, CafeDBId = cafe.CafeDBId, UniqueId = chair.Id, Location = FurnitureLocation.Floor, StackCount = 1, ItemDeploySequence = 12 });
        db.SaveChanges();

        await Manager().CafeRemoveAllFurniture(db, account, new CafeRemoveAllFurnitureRequest { CafeDBId = cafe.CafeDBId });

        Assert.Contains(db.Furnitures, x => x.UniqueId == floorCovering.Id && x.ItemDeploySequence != 0);
        Assert.Contains(db.Furnitures, x => x.UniqueId == chair.Id && x.ItemDeploySequence == 0);
    }

    [Fact]
    public async Task LoginRepairReturnsOrphanedDeployedRowsToInventory()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var cafe = NewCafe(db, account);

        var chair = Excels.GetTable<FurnitureExcelT>().First(x => x.SubCategory == FurnitureSubCategory.Chair);
        db.Furnitures.Add(new FurnitureDBServer { AccountServerId = account.ServerId, CafeDBId = 0, UniqueId = chair.Id, Location = FurnitureLocation.Floor, StackCount = 1, ItemDeploySequence = 21 });
        db.Furnitures.Add(new FurnitureDBServer { AccountServerId = account.ServerId, CafeDBId = 0, UniqueId = chair.Id, Location = FurnitureLocation.Floor, StackCount = 1, ItemDeploySequence = 22 });
        db.SaveChanges();

        await Manager().RepairFurniture(db, account, [cafe]);

        Assert.Empty(db.Furnitures.Where(x => x.AccountServerId == account.ServerId && x.ItemDeploySequence != 0 && x.UniqueId == chair.Id));
        var stack = db.Furnitures.Single(x => x.AccountServerId == account.ServerId && x.UniqueId == chair.Id);
        Assert.Equal(2, stack.StackCount);
        Assert.Equal(FurnitureLocation.Inventory, stack.Location);
    }

    [Fact]
    public async Task LoginRepairReseedsACafeMissingItsInteriors()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var cafe = NewCafe(db, account);

        await Manager().RepairFurniture(db, account, [cafe]);

        var furnById = Excels.GetTable<FurnitureExcelT>().ToDictionary(x => x.Id);
        var deployed = db.Furnitures.Where(x => x.AccountServerId == account.ServerId && x.CafeDBId == cafe.CafeDBId && x.ItemDeploySequence != 0).ToList();
        foreach (var sub in new[] { FurnitureSubCategory.Floor, FurnitureSubCategory.Wallpaper, FurnitureSubCategory.Background })
            Assert.Contains(deployed, x => furnById[x.UniqueId].SubCategory == sub);

        // a second pass must not stack more interiors on top
        await Manager().RepairFurniture(db, account, [cafe]);
        Assert.Equal(deployed.Count, db.Furnitures.Count(x => x.AccountServerId == account.ServerId && x.CafeDBId == cafe.CafeDBId && x.ItemDeploySequence != 0));
    }

    [Fact]
    public async Task VisitorsRollOnceAndHoldUntilTheNextReset()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var cafe = NewCafe(db, account);

        foreach (var id in Excels.GetTable<CharacterExcelT>().GetReleaseCharacters().Take(8).Select(x => x.Id))
            db.Characters.Add(new CharacterDBServer { AccountServerId = account.ServerId, UniqueId = id });
        db.SaveChanges();

        var manager = Manager();
        await manager.RefreshVisitors(db, account, [cafe]);

        Assert.NotEmpty(cafe.CafeVisitCharacterDBs);
        Assert.All(cafe.CafeVisitCharacterDBs.Values, v => Assert.NotEqual(0, v.ServerId));
        var rolledAt = cafe.VisitUpdateTime;
        var keys = cafe.CafeVisitCharacterDBs.Keys.OrderBy(x => x).ToList();

        await manager.RefreshVisitors(db, account, [cafe]);
        Assert.Equal(rolledAt, cafe.VisitUpdateTime);
        Assert.Equal(keys, cafe.CafeVisitCharacterDBs.Keys.OrderBy(x => x).ToList());

        cafe.VisitUpdateTime = rolledAt.AddDays(-2);
        await manager.RefreshVisitors(db, account, [cafe]);
        Assert.True(cafe.VisitUpdateTime > rolledAt.AddDays(-1));
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

    private static CafeManager Manager() =>
        new(Excels, new ParcelHandler(Excels, Mapper), new ConsumeHandler(Excels, Mapper), Mapper);

    private static CafeDBServer NewCafe(SchaleDataContext db, AccountDBServer account)
    {
        var cafe = new CafeDBServer(account, 1);
        db.AddCafes(account.ServerId, cafe);
        db.SaveChanges();
        return cafe;
    }

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-cafepresettest-{Guid.NewGuid():N}.sqlite3");
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
