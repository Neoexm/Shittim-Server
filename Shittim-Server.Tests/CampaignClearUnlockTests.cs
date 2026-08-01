using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Managers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The client's CampaignService filters histories on IsClearedEver before comparing StageUniqueIds against a stage's prerequisites - it is the only field a history row unlocks anything with.
// The sub-stage and strategy-skip clear paths built their rows through the ctor, which never set it, so hard tabs, extra stages and the next chapter stayed locked no matter how much was cleared.
public class CampaignClearUnlockTests
{
    [Fact]
    public async Task ASubStageClearCountsForUnlocks()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var stageId = FirstNormalStage();

        var (history, _) = await Manager().CampaignSubStageResult(db, account, new CampaignSubStageResultRequest
        {
            SessionKey = new SessionKey { AccountServerId = account.ServerId, MxToken = "test" },
            Summary = ClearedBattle(stageId)
        });

        Assert.True(history.IsClearedEver);
        Assert.True(db.CampaignStageHistories.Single(x => x.StageUniqueId == stageId).IsClearedEver);
    }

    [Fact]
    public async Task AReclearBackfillsARowMissingTheFlag()
    {
        using var db = NewContext();
        var account = NewAccount(db);
        var stageId = FirstNormalStage();

        // a row the old ctor wrote: cleared, rewarded, but not counted
        db.CampaignStageHistories.Add(new CampaignStageHistoryDBServer
        {
            AccountServerId = account.ServerId,
            StageUniqueId = stageId,
            Star1Flag = true,
            FirstClearRewardReceive = DateTime.UtcNow,
            IsClearedEver = false
        });
        db.SaveChanges();

        var (history, _) = await Manager().CampaignSubStageResult(db, account, new CampaignSubStageResultRequest
        {
            SessionKey = new SessionKey { AccountServerId = account.ServerId, MxToken = "test" },
            Summary = ClearedBattle(stageId)
        });

        Assert.True(history.IsClearedEver);
    }

    private static BattleSummary ClearedBattle(long stageId) => new()
    {
        StageId = stageId,
        IsAbort = false,
        EndType = BattleEndType.Clear,
        Group01Summary = new GroupSummary { TeamId = 1, Heroes = new HeroSummaryCollection() },
    };

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
        var path = Path.Combine(Path.GetTempPath(), $"shittim-clearunlock-{Guid.NewGuid():N}.sqlite3");
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
