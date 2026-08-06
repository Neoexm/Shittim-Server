using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Shittim_Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shittim_Server.Tests;

public class MiniGameMissionTests(ITestOutputHelper output)
{
    [Fact]
    public void ASectionMissionOnlyMovesForTheStageThatNamedIt()
    {
        // 8250016-8250024 are [825, 1000, 2..10] and 8250026-8250034 repeat those same section numbers against stage 2000, so a section number on its own names one mission on the normal stage and one on the hard.
        if (Excel is null) { SkipNote(); return; }

        using var db = NewContext();
        var account = NewAccount(db);

        var progress = new MiniGameMissionService(Excel, BuildMapper()).UpdateMissionProgress(
            db, account, 825, MissionCompleteConditionType.Reset_ClearSpecificSectionShooting,
            amount: 1, parameter: 5, stageId: 1000);

        var mission = Assert.Single(progress, x => x.MissionUniqueId == 8250019);
        Assert.Equal(1, mission.ProgressParameters![5]);
        Assert.True(mission.Complete);

        Assert.DoesNotContain(progress, x => x.MissionUniqueId == 8250029);
    }

    private void SkipNote() => output.WriteLine(
        "No Resources/Dumped found, so the excel-backed assertions did not run. Build and start " +
        "the server once to populate it, then re-run.");

    private static readonly ExcelTableService? Excel = LocateDumps();

    private static ExcelTableService? LocateDumps()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "Shittim-Server", "Shittim-Server.csproj")))
                continue;

            var server = Path.Combine(dir.FullName, "Shittim-Server");
            var dumped = new[]
            {
                Path.Combine(server, "Resources", "Dumped"),
                Path.Combine(server, "bin", "Debug", "net10.0", "Resources", "Dumped"),
                Path.Combine(server, "bin", "Release", "net10.0", "Resources", "Dumped"),
            }.FirstOrDefault(Directory.Exists);

            if (dumped is null) return null;

            ExcelTableService.DumpedDir = dumped;
            return new ExcelTableService();
        }

        return null;
    }

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-minigamemission-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db)
    {
        var account = new AccountDBServer { ServerId = 1, Nickname = "Sensei" };
        db.Accounts.Add(account);
        db.SaveChanges();
        return account;
    }

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
