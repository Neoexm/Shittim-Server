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
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// a character delivered as a mail attachment went through ParcelResolver.UpdateCharacter, which never set StarGrade, so every 3-star welfare student arrived at 1 star. gacha and the GM paths were unaffected because they build the row themselves.
public class MailCharacterStarTests
{
    [Fact]
    public async Task ACharacterGrantedAsAParcelArrivesAtItsDefaultStarGrade()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var excel = Excels.GetTable<CharacterExcelT>().GetReleaseCharacters().First(x => x.DefaultStarGrade == 3);

        await Handler().BuildParcel(db, account, new ParcelResult(ParcelType.Character, excel.Id, 1));

        Assert.Equal(3, db.Characters.Single(x => x.AccountServerId == account.ServerId && x.UniqueId == excel.Id).StarGrade);
    }

    [Fact]
    public async Task ADuplicateCharacterParcelStillConvertsToEleph()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var excel = Excels.GetTable<CharacterExcelT>().GetReleaseCharacters().First(x => x.DefaultStarGrade == 3);

        await Handler().BuildParcel(db, account, new ParcelResult(ParcelType.Character, excel.Id, 1));
        await Handler().BuildParcel(db, account, new ParcelResult(ParcelType.Character, excel.Id, 1));

        Assert.Single(db.Characters.Where(x => x.AccountServerId == account.ServerId && x.UniqueId == excel.Id));
        Assert.Equal(excel.CharacterPieceItemAmount, db.Items.Single(x => x.AccountServerId == account.ServerId && x.UniqueId == excel.CharacterPieceItemId).StackCount);
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

    private static ParcelHandler Handler() => new(Excels, Mapper);

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-mailstartest-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db)
    {
        var account = new AccountDBServer { ServerId = 1, Nickname = "Sensei1" };
        db.Accounts.Add(account);
        db.Currencies.Add(new AccountCurrencyDBServer(1));
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
