using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Managers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// Opening a furniture selection box used to record the pick as a character row keyed by the furniture id, and one such row is enough to make the client abort every later login sync.
// These run against the real dumped excels because the whole bug is in how the ticket's recipe chain is (not) consulted.
public class ItemBoxTests
{
    [Fact]
    public async Task AFurniturePickFromASelectBoxLandsAsFurniture()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var (box, choice) = FurnitureSelectBox();
        var ticket = GiveItem(db, account, box.Id, 2);

        var result = await Manager().SelectTicket(db, account, new ItemSelectTicketRequest
        {
            TicketItemServerId = ticket.ServerId,
            SelectItemUniqueId = choice.ParcelId,
            ConsumeCount = 1
        });

        Assert.Contains(db.Furnitures, x => x.AccountServerId == account.ServerId && x.UniqueId == choice.ParcelId);
        Assert.Empty(db.Characters);
        Assert.Equal(1, db.Items.Single(x => x.ServerId == ticket.ServerId).StackCount);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ASelectionOutsideTheBoxsPoolCannotCreateACharacterRow()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var (box, _) = FurnitureSelectBox();
        var ticket = GiveItem(db, account, box.Id, 1);

        // an id no table knows - the old handler would have written it to Characters verbatim
        await Manager().SelectTicket(db, account, new ItemSelectTicketRequest
        {
            TicketItemServerId = ticket.ServerId,
            SelectItemUniqueId = 999999999,
            ConsumeCount = 1
        });

        Assert.Empty(db.Characters);
    }

    [Fact]
    public async Task ARandomFurnitureBoxOpensThroughItemConsume()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var box = AllFurnitureGachaBox();
        var item = GiveItem(db, account, box.Id, 1);

        var (used, result) = await Manager().Consume(db, account, new ItemConsumeRequest
        {
            TargetItemServerId = item.ServerId,
            ConsumeCount = 1
        });

        Assert.NotEmpty(db.Furnitures.Where(x => x.AccountServerId == account.ServerId));
        Assert.Empty(db.Characters);
        Assert.Equal(0, used.StackCount);
        Assert.NotNull(result);
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

    private static ItemManager Manager() => new(Excels, new ParcelHandler(Excels, Mapper));

    private static (ItemExcelT, RecipeSelectionGroupExcelT) FurnitureSelectBox()
    {
        var recipes = Excels.GetTable<RecipeExcelT>();
        var groups = Excels.GetTable<RecipeSelectionGroupExcelT>();

        foreach (var item in Excels.GetTable<ItemExcelT>().Where(x => x.UsingResultParcelType == ParcelType.Recipe))
        {
            var recipe = recipes.FirstOrDefault(x => x.Id == item.UsingResultId);
            if (recipe == null) continue;

            var choice = groups.FirstOrDefault(x => x.RecipeSelectionGroupId == recipe.RecipeSelectionGroupId && x.ParcelType == ParcelType.Furniture);
            if (choice != null) return (item, choice);
        }

        throw new InvalidOperationException("no furniture select box in the dumped excels");
    }

    private static ItemExcelT AllFurnitureGachaBox()
    {
        var elements = Excels.GetTable<GachaElementExcelT>();

        // a group mixing furniture with anything else would make the assertion flake on the roll
        return Excels.GetTable<ItemExcelT>().First(x =>
            x.UsingResultParcelType == ParcelType.GachaGroup &&
            elements.Where(e => e.GachaGroupID == x.UsingResultId).ToList() is { Count: > 0 } pool &&
            pool.All(e => e.ParcelType == ParcelType.Furniture));
    }

    private static ItemDBServer GiveItem(SchaleDataContext db, AccountDBServer account, long uniqueId, long count)
    {
        var item = new ItemDBServer { AccountServerId = account.ServerId, UniqueId = uniqueId, StackCount = count };
        db.Items.Add(item);
        db.SaveChanges();
        return item;
    }

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-itemboxtest-{Guid.NewGuid():N}.sqlite3");
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
