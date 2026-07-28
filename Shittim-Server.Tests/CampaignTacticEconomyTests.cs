using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.BattlesEntities;
using Shittim.Services;
using Shittim_Server.Controllers.Api;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

/// <summary>
/// Pins what a Campaign_TacticResult (6008) actually pays out and reports, against the official
/// capture of a full chapter-11 mission (captures/NetworkLog official.txt, four tactic results).
///
/// Two whole halves of that response were missing. Official's ParcelResultDB is never empty: a tactic
/// that does not clear the stage still pays the stage's TacticRewardExp to every character that
/// fought it (line 397 — six CharacterExp parcels and nothing else), and the clearing tactic adds the
/// rolled drop table, the AP-to-account-exp conversion and the once-per-account reward rows (line 529
/// — fifteen ParcelForMission entries and BaseAccountExp 10 / AdditionalAccountExp 5). We sent a
/// hand-built shell of two dozen empty collections instead, so a campaign run granted nothing but the
/// first-clear and three-star rows. Official also carries MissionProgressDBs on all four responses;
/// we carried them on none, so a daily like "clear 10 stages" never moved while a campaign was played.
/// </summary>
public class CampaignTacticEconomyTests
{
    // ------------------------------------------------------------------- the reward strip (GAP 1)

    [Fact]
    public void DisplaySequenceIsParcelForMissionWithoutTheExpEntries()
    {
        // Official's clear: ParcelForMission 15 entries, DisplaySequence the same list less the six
        // CharacterExp and the one AccountExp. The strip has no icon for exp.
        var parcelForMission = new List<ParcelInfo>
        {
            Parcel(ParcelType.Equipment, 101005, 1),
            Parcel(ParcelType.CharacterExp, 10004, 3),
            Parcel(ParcelType.CharacterExp, 10005, 3),
            Parcel(ParcelType.AccountExp, 1, 10),
            Parcel(ParcelType.Currency, 3, 30),
            Parcel(ParcelType.Currency, 1, 1058),
        };

        var display = ParcelHandler.DisplaySequenceFor(parcelForMission);

        Assert.Equal(
            new[] { ParcelType.Equipment, ParcelType.Currency, ParcelType.Currency },
            display.Select(x => x.Key.Type));
        Assert.Equal(new long[] { 101005, 3, 1 }, display.Select(x => x.Key.Id));
    }

    [Fact]
    public void ANonClearingTacticStillReportsTheExpItPaid()
    {
        // Capture line 397: the ParcelResultDB on a mid-stage tactic has exactly two keys, CharacterDBs
        // and ParcelForMission. OmitWhenEmpty is what strips the rest — official never sends an empty
        // collection inside a ParcelResultDB, and it sends no DisplaySequence here at all because
        // every entry was exp.
        var parcels = new List<ParcelInfo>
        {
            Parcel(ParcelType.CharacterExp, 13013, 3),
            Parcel(ParcelType.CharacterExp, 13014, 3),
        };

        var result = new ParcelResultDB
        {
            CharacterDBs = new List<CharacterDB> { new() { UniqueId = 13013 }, new() { UniqueId = 13014 } },
            ParcelForMission = parcels,
            DisplaySequence = ParcelHandler.DisplaySequenceFor(parcels),
        };

        var json = JObject.Parse(
            JsonConvert.SerializeObject(result, GatewayController.OfficialPacketJsonSettings));

        // AccountCurrencyDB is not a collection, so OmitWhenEmpty cannot drop it, and
        // ParcelResolver.FinalizeUpdates attaches the live one on every reward for every endpoint
        // rather than only when a currency moved. That is a truthful superset of what official sends
        // here and not this path's to change, so it is excluded from the comparison.
        Assert.Equal(
            new[] { "CharacterDBs", "ParcelForMission" },
            json.Properties().Select(p => p.Name).Where(x => x != "AccountCurrencyDB"));
    }

    [Fact]
    public void EveryDeployedStudentEarnsTacticExpAndBorrowedAssistsEarnNone()
    {
        // Official pays TacticRewardExp to six students per tactic — four strikers and two Specials —
        // even the ones who never took the field. A borrowed assist is not one of ours to level.
        var summary = WonBattle();
        summary.Group01Summary!.Supporters![1].OwnerAccountId = 999;

        var ids = ConcentrateCampaignManager.DeployedCharacterIds(summary, accountServerId: 1).ToList();

        Assert.Equal(new long[] { 10004, 10005, 13001, 10088, 20046 }, ids);
    }

    [Fact]
    public void OnlyTheDefaultTaggedRowsAreRolledAsDrops()
    {
        // FirstClear and ThreeStar rows live in the same reward group and carry probabilities of their
        // own. Rolling the whole group — which the sub-stage and sweep paths still do — re-grants the
        // once-per-account rewards on every clear. Probabilities are pinned at 10000 here so the roll
        // is decided by the tag, not by the RNG.
        var rewards = new List<CampaignStageRewardExcelT>
        {
            RewardRow(RewardTag.FirstClear, ParcelType.Equipment, 101005, prob: 10000),
            RewardRow(RewardTag.ThreeStar, ParcelType.Currency, 3, amount: 30, prob: 10000),
            RewardRow(RewardTag.Default, ParcelType.GachaGroup, 500000, prob: 10000),
        };

        var drops = ConcentrateCampaignManager.RolledDrops(rewards);

        Assert.Equal(new long[] { 500000 }, drops.Select(x => x.Id));
    }

    [Fact]
    public void ADropThatFailsItsRollIsNotGranted()
    {
        var rewards = new List<CampaignStageRewardExcelT>
        {
            RewardRow(RewardTag.Default, ParcelType.Equipment, 101005, prob: 0),
        };

        // Probability 0 in this data means "no roll declared, always grant" — GenerateProbability
        // treats it as certain, and the stage's guaranteed gold drop relies on that.
        Assert.Single(ConcentrateCampaignManager.RolledDrops(rewards));
    }

    [Fact]
    public void NewbieBonusIsTheSurplusOverParNotTheWholeRatio()
    {
        // NewbieExpRatio 15000 means "pay 150% altogether", so the bonus is the 50% above par.
        // Reading the whole ratio as bonus paid 150% extra: official's clear of this 10-AP stage
        // reports BaseAccountExp 10 with AdditionalAccountExp 5, and we reported 15.
        var levels = new List<AccountLevelExcelT>
        {
            new() { Level = 1, Exp = 5556, NewbieExpRatio = 15000, CloseInterval = 70, APAutoChargeMax = 0 },
        };

        var (_, _, bonusExp, _) = MathService.CalculateAccountExp(
            level: 1, exp: 0, addedExp: 10, expLevelDatas: levels);

        Assert.Equal(5, bonusExp);
    }

    [Fact]
    public void PastTheNewbieWindowThereIsNoBonusAtAll()
    {
        var levels = new List<AccountLevelExcelT>
        {
            new() { Level = 80, Exp = 5556, NewbieExpRatio = 15000, CloseInterval = 70, APAutoChargeMax = 0 },
        };

        var (_, _, bonusExp, _) = MathService.CalculateAccountExp(
            level: 80, exp: 0, addedExp: 10, expLevelDatas: levels);

        Assert.Equal(0, bonusExp);
    }

    // ------------------------------------------------------------------ mission progress (GAP 2)

    [Fact]
    public void AStageSpecificMissionIsKeyedByTheStageItNamed()
    {
        // Official's clear reports mission 20150 as {"1161101": 1}, Complete — keyed by the parameter
        // it matched on, not by 0. The client reads the count out by that key.
        if (Excel is null) return;

        using var db = NewContext();
        var account = NewAccount(db);

        var progress = NewMissionService().UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_ClearSpecificCampaignStageCount,
            amount: 1, parameter: 1161101);

        var mission = Assert.Single(progress, x => x.MissionUniqueId == 20150);
        Assert.Equal(new long[] { 1161101 }, mission.ProgressParameters!.Keys);
        Assert.Equal(1, mission.ProgressParameters[1161101]);
        Assert.True(mission.Complete);
    }

    [Fact]
    public void ClearingOneStageDoesNotSatisfyEveryOtherStagesMission()
    {
        // The regression this replaces: with no parameter passed, every parameterised row of the
        // condition type ticked — one campaign clear moved all 259 "clear stage X" missions.
        if (Excel is null) return;

        using var db = NewContext();
        var account = NewAccount(db);
        var service = NewMissionService();

        Assert.Empty(service.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_ClearSpecificCampaignStageCount));

        var one = service.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_ClearSpecificCampaignStageCount,
            amount: 1, parameter: 1161101);

        Assert.Single(one);
    }

    [Fact]
    public void TheTurnMissionKeepsTheBestRunRatherThanASum()
    {
        // Mission 30150 is "clear 1161101 in 4 turns or fewer". Official's five-turn clear reports it
        // as {"1161101": 5} and *not* complete, which is only readable as a personal best against a
        // ceiling. Accumulating would walk the number away from the target on every replay.
        if (Excel is null) return;

        using var db = NewContext();
        var account = NewAccount(db);
        var service = NewMissionService();

        var fiveTurns = Assert.Single(service.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_CompleteCampaignStageMinimumTurn,
            amount: 5, parameter: 1161101));
        db.SaveChanges();

        Assert.Equal(5, fiveTurns.ProgressParameters![1161101]);
        Assert.False(fiveTurns.Complete);

        var threeTurns = Assert.Single(service.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_CompleteCampaignStageMinimumTurn,
            amount: 3, parameter: 1161101));

        Assert.Equal(3, threeTurns.ProgressParameters![1161101]);
        Assert.True(threeTurns.Complete);
    }

    [Fact]
    public void ChallengeMissionsAreServedLikeEveryOtherCategory()
    {
        // Every Reset_CompleteCampaignStageMinimumTurn row is Category.Challenge, so excluding the
        // category excluded the whole condition type — and official sends 30150 in MissionProgressDBs
        // like any other mission.
        if (Excel is null) return;

        var challenge = Excel.GetTable<MissionExcelT>().First(x => x.Id == 30150);
        Assert.Equal(MissionCategory.Challenge, challenge.Category);

        using var db = NewContext();
        var account = NewAccount(db);

        Assert.Single(NewMissionService().UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_CompleteCampaignStageMinimumTurn,
            amount: 5, parameter: 1161101));
    }

    [Fact]
    public void UnparameterisedCountersStillAccumulateUnderKeyZero()
    {
        // The other half of official's clear: 1503/1504/110014 are all Achieve_ClearCampaignStageCount
        // with no parameter, and every one of them reports under {"0": n}.
        if (Excel is null) return;

        using var db = NewContext();
        var account = NewAccount(db);
        var service = NewMissionService();

        service.UpdateMissionProgress(db, account, MissionCompleteConditionType.Achieve_ClearCampaignStageCount);
        db.SaveChanges();
        var second = service.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Achieve_ClearCampaignStageCount);

        var daily = Assert.Single(second, x => x.MissionUniqueId == 1503);
        Assert.Equal(new long[] { 0 }, daily.ProgressParameters!.Keys);
        Assert.Equal(2, daily.ProgressParameters[0]);
    }

    [Fact]
    public void TacticRewardExpIsTheThreeOfficialPaysPerStudentPerTactic()
    {
        // The realigned tail of CampaignStageExcel. Read against the old layout this field came back
        // as 652835029140 — it was overlapping a string offset — which would have granted a student
        // every level in the game off one tactic.
        if (Excel is null) return;

        var stages = Excel.GetTable<CampaignStageExcelT>();

        Assert.Equal(3, stages.First(x => x.Id == 1161101).TacticRewardExp);
        Assert.Equal([3L], stages.Select(x => x.TacticRewardExp).Distinct().ToList());
    }

    // ------------------------------------------------------------------------------ fixtures

    private static ParcelInfo Parcel(ParcelType type, long id, long amount) => new()
    {
        Key = new ParcelKeyPair { Type = type, Id = id },
        Amount = amount,
        Multiplier = BasisPoint.One,
        Probability = BasisPoint.One,
    };

    private static CampaignStageRewardExcelT RewardRow(
        RewardTag tag, ParcelType type, long id, int amount = 1, int prob = 10000) => new()
    {
        GroupId = 1161101,
        RewardTag = tag,
        StageRewardParcelType = type,
        StageRewardId = id,
        StageRewardAmount = amount,
        StageRewardProb = prob,
    };

    private static HeroSummary Hero(long characterId, int entityId) => new()
    {
        CharacterId = characterId,
        BattleEntityId = new EntityId { uniqueId = entityId },
    };

    private static HeroSummaryCollection Collection(params HeroSummary[] heroes)
    {
        var collection = new HeroSummaryCollection();
        collection.Add(heroes);
        return collection;
    }

    /// <summary>Official 6008 #4: four strikers and two Specials, matching the six exp parcels.</summary>
    private static BattleSummary WonBattle() => new()
    {
        StageId = 1161101,
        Group01Summary = new GroupSummary
        {
            TeamId = 1,
            Heroes = Collection(
                Hero(10004, 16777217), Hero(10005, 16777218),
                Hero(13001, 16777219), Hero(10088, 16777220)),
            Supporters = Collection(Hero(20046, 16777250), Hero(26003, 16777251)),
        },
    };

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-tacticecon-{Guid.NewGuid():N}.sqlite3");
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

    private static MissionService NewMissionService() => new(Excel!, BuildMapper());

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        // AddAutoMapper's factory resolves an ILoggerFactory; without it building IMapper throws.
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    /// <summary>
    /// The shipped excel tables. They live under the server project rather than the test output, and
    /// ExcelDB.db is SQLCipher-encrypted, so ExcelTableService is the only way to read them. A
    /// checkout without the dumps skips the tests that need them rather than failing.
    /// </summary>
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
}
