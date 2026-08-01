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
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Managers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// Every reward group carries its FirstClear and ThreeStar rows at 100% next to the drops, so any path that rolls the whole group pays the once-per-account pyroxene on every replay - which is exactly what sweeps and sub-stage clears did.
public class CampaignRewardGatingTests
{
    [Fact]
    public async Task OnlyTheFirstClearPaysTheFirstClearRows()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var stageId = FirstNormalStage();

        var (_, _, firstClear, _) = await Manager().CampaignSubStageResult(db, account, Result(account, OneStarClear(stageId)));
        Assert.NotEmpty(firstClear);

        var (_, _, reclear, _) = await Manager().CampaignSubStageResult(db, account, Result(account, OneStarClear(stageId)));
        Assert.Empty(reclear);
    }

    [Fact]
    public async Task TheThreeStarPyroxeneWaitsForTheRunThatCompletesTheStars()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var stageId = FirstNormalStage();

        var (_, _, _, shortOfStars) = await Manager().CampaignSubStageResult(db, account, Result(account, OneStarClear(stageId)));
        Assert.Empty(shortOfStars);

        var (_, _, _, completed) = await Manager().CampaignSubStageResult(db, account, Result(account, ThreeStarClear(stageId)));
        var gem = Assert.Single(completed);
        Assert.Equal(ParcelType.Currency, gem.Key.Type);
        Assert.Equal((long)CurrencyTypes.GemBonus, gem.Key.Id);

        var (_, _, _, again) = await Manager().CampaignSubStageResult(db, account, Result(account, ThreeStarClear(stageId)));
        Assert.Empty(again);
    }

    [Fact]
    public async Task AReclearGrantsNoPyroxeneThroughTheDropRoll()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var stageId = FirstNormalStage();

        await Manager().CampaignSubStageResult(db, account, Result(account, ThreeStarClear(stageId)));

        var (_, result, _, _) = await Manager().CampaignSubStageResult(db, account, Result(account, ThreeStarClear(stageId)));
        Assert.DoesNotContain(result.ParcelForMission!, x => x.Key.Type == ParcelType.Currency && x.Key.Id == (long)CurrencyTypes.GemBonus);
    }

    [Fact]
    public void SweepDropsAreRolledToConcreteItemsBeforeDisplay()
    {
        // the group's GachaGroup rows are IsDisplayed=false; sent raw they render as blank cells in the client's sweep result
        var stage = Excels.GetTable<CampaignStageExcelT>().First(x => x.Id == FirstNormalStage());
        var rewards = Excels.GetTable<CampaignStageRewardExcelT>().GetAllRewardsByGroupId(stage.CampaignStageRewardId);

        var expanded = ConcentrateCampaignManager.RolledDrops(rewards).GenerateGachaGroup(Excels.GetTable<GachaElementExcelT>());

        Assert.NotEmpty(expanded);
        Assert.DoesNotContain(expanded, x => x.Type == ParcelType.GachaGroup);
    }

    private static CampaignSubStageResultRequest Result(AccountDBServer account, BattleSummary summary) => new()
    {
        SessionKey = new SessionKey { AccountServerId = account.ServerId, MxToken = "test" },
        Summary = summary
    };

    private static BattleSummary OneStarClear(long stageId) => new()
    {
        StageId = stageId,
        IsAbort = false,
        EndType = BattleEndType.Clear,
        Group01Summary = new GroupSummary { TeamId = 1, Heroes = new HeroSummaryCollection() },
    };

    // every enemy down inside the two-minute window with no survivor lost: all three of CalcStrategySkipStarGoals' goals at once
    private static BattleSummary ThreeStarClear(long stageId) => new()
    {
        StageId = stageId,
        IsAbort = false,
        EndType = BattleEndType.Clear,
        Group01Summary = new GroupSummary { TeamId = 1, Heroes = Collection(new HeroSummary { CharacterId = 10004, DeadFrame = -1 }) },
        Group02Summary = new GroupSummary { TeamId = 2, Heroes = Collection(new HeroSummary { CharacterId = 20001, DeadFrame = 300 }) },
    };

    private static HeroSummaryCollection Collection(params HeroSummary[] heroes)
    {
        var collection = new HeroSummaryCollection();
        collection.Add(heroes);
        return collection;
    }

    private static long FirstNormalStage() =>
        Excels.GetTable<CampaignChapterExcelT>().First(x => x.NormalCampaignStageId is { Count: > 0 }).NormalCampaignStageId[0];

    private static CampaignManager Manager() => new(Excels, new ParcelHandler(Excels, Mapper), Mapper);

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

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-rewardgating-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db)
    {
        var account = new AccountDBServer { ServerId = 1, Nickname = "Sensei" };
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
