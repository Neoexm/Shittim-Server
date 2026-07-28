using Newtonsoft.Json;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCommand;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCondition;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shittim_Server.Tests;

/// <summary>
/// Pins how a campaign stage is cleared.
///
/// The clear is not "the map is empty" — it is the map's own HexaCommandEndBattle, fired by a
/// HexaConditionUnitDead naming ONE designated boss. All 321 dumped strategy maps carry exactly one
/// such event, and on none of them is that boss the whole enemy roster: strategymap_1011104's boss
/// is 10013 while 10017 and 10018 are still standing, and the reported failure, stage 1111102, is
/// boss 10044 out of 10040-10044.
///
/// A kill-everything rule is therefore a strict subset of the real one — it fires late, or on the
/// 21 maps where a TileHide deletes an enemy for you, never. That is exactly the reported bug: the
/// boss died and the mission carried on.
/// </summary>
public class CampaignHexaEventTests(ITestOutputHelper output)
{
    /// <summary>strategymap_1011104's shape: three enemies, and only 10013 ends the battle.</summary>
    private static HexaTileMap BossMap() => new()
    {
        Events =
        [
            new HexaEvent
            {
                EventName = "StartEvent(Clone)",
                EventId = 0,
                MultipleConditionCheckType = MultipleConditionCheckType.And,
                HexaConditions = [new HexaConditionStartCampaign { ConditionId = 0 }],
                HexaCommands =
                [
                    new HexaCommandUnitSpawn { CommandId = 0, UnitEntityIds = [10013, 10017, 10018] },
                ],
            },
            new HexaEvent
            {
                EventName = "EndBattle_Win",
                EventId = 2,
                MultipleConditionCheckType = MultipleConditionCheckType.And,
                HexaConditions = [new HexaConditionUnitDead { ConditionId = 0, UnitEntityIds = [10013] }],
                HexaCommands = [new HexaCommandEndBattle { CommandId = 0, EndBattleType = CampaignEndBattle.Win }],
            },
        ],
    };

    private static Dictionary<long, HexaUnit> Alive(params long[] entityIds) =>
        entityIds.ToDictionary(id => id, id => new HexaUnit { EntityId = id });

    [Fact]
    public void BossDeathWithSurvivorsFiresEndBattle()
    {
        // The test that would have caught the shipped bug: 10013 is dead, two mobs are still on the
        // map, and official ends the mission anyway.
        var fired = HexaMapService.FindSatisfiedEndBattle(BossMap(), Alive(10017, 10018), null);

        Assert.NotNull(fired);
        Assert.Equal(2, fired!.Value.Event.EventId);
        Assert.Equal([0L], fired.Value.ConditionIds);
        Assert.Equal(CampaignEndBattle.Win, fired.Value.Command.EndBattleType);
    }

    [Fact]
    public void MobDeathWithTheBossAliveFiresNothing()
    {
        // Guards the other direction: clearing two of three enemies must not end the mission early.
        Assert.Null(HexaMapService.FindSatisfiedEndBattle(BossMap(), Alive(10013), null));
    }

    [Fact]
    public void AnEventAlreadyInTheActivationHistoryDoesNotRefire()
    {
        var activated = new Dictionary<long, List<long>> { [0] = [0], [2] = [0] };

        Assert.Null(HexaMapService.FindSatisfiedEndBattle(BossMap(), Alive(10017, 10018), activated));
    }

    [Fact]
    public void AMapWithNoEventsFiresNothing()
    {
        // LoadState hands back an eventless map when a strategymap dump is missing. It must fall
        // through to TacticResult's EnemyInfos-empty fallback, not clear the stage on turn one.
        Assert.Null(HexaMapService.FindSatisfiedEndBattle(new HexaTileMap { Events = [] }, Alive(10013), null));
        Assert.Null(HexaMapService.FindSatisfiedEndBattle(new HexaTileMap(), Alive(10013), null));
    }

    [Fact]
    public void AnEventWithAnUnhandledConditionIsNotFired()
    {
        // And-semantics, conservatively: a condition type the evaluator does not understand counts as
        // unsatisfied rather than being skipped, so a partly-understood event can never fire early.
        var map = BossMap();
        map.Events![1].HexaConditions!.Add(new HexaConditionArriveTile
        {
            ConditionId = 1,
            TileLocation = new HexLocation { x = 3, y = -4, z = 1 },
        });

        Assert.Null(HexaMapService.FindSatisfiedEndBattle(map, Alive(10017, 10018), null));
    }

    [Fact]
    public void AnEmptyConditionListIsNotVacuouslySatisfied()
    {
        var map = BossMap();
        map.Events![1].HexaConditions = [];

        Assert.Null(HexaMapService.FindSatisfiedEndBattle(map, Alive(10013, 10017, 10018), null));

        map.Events[1].HexaConditions = [new HexaConditionUnitDead { ConditionId = 0, UnitEntityIds = [] }];

        Assert.Null(HexaMapService.FindSatisfiedEndBattle(map, Alive(10013, 10017, 10018), null));
    }

    [Fact]
    public void TheDumpedEndBattleTypeSurvivesDeserialization()
    {
        // EndBattleType is a public *field*, not a property. If Json.NET ever stopped populating it
        // the value would be None, DefaultValueHandling.Ignore would drop Parameter off the wire
        // entirely, and the client would read a victory as a retreat. Pin it against a real dump.
        if (DumpDir is null) { SkipNote(); return; }

        var map = LoadDump(Path.Combine(DumpDir, "strategymap_1161101.json"));

        var ending = Assert.Single(map.Events!, HasEndBattle);

        Assert.Equal(CampaignEndBattle.Win, EndBattleOf(ending).EndBattleType);

        // One boss out of the five enemies 10027-10031 — not the roster.
        var boss = Assert.IsType<HexaConditionUnitDead>(Assert.Single(ending.HexaConditions!));
        Assert.Equal([10031L], boss.UnitEntityIds);
    }

    [Fact]
    public void TheRealDumpsNeverGateTheClearOnTheWholeRoster()
    {
        // The survey that supersedes the kill-everything rule, run against the shipped dumps rather
        // than quoted from a report: every campaign map has exactly one EndBattle, gated by exactly
        // one UnitDead, and its boss set is always a *strict* subset of the spawned enemies. If this
        // ever fails on a new dump, the fallback in TacticResult is what keeps that stage finishable.
        if (DumpDir is null) { SkipNote(); return; }

        var maps = Directory.GetFiles(DumpDir, "strategymap_*.json");
        Assert.NotEmpty(maps);

        foreach (var path in maps)
        {
            var map = LoadDump(path);
            var ending = Assert.Single(map.Events!, HasEndBattle);

            var boss = Assert.IsType<HexaConditionUnitDead>(Assert.Single(ending.HexaConditions!));
            var roster = map.HexaUnitList?.Where(u => !u.IsPlayer).Select(u => u.EntityId).ToHashSet() ?? [];

            Assert.NotEmpty(boss.UnitEntityIds!);
            Assert.ProperSubset(roster, boss.UnitEntityIds!.ToHashSet());
        }
    }

    [Fact]
    public void EveryRealDumpClearsOnItsBossAloneAndOnlyOnce()
    {
        // End to end over the shipped data: kill just the boss, leave every other enemy standing, and
        // the evaluator must fire — then must not fire again once the event is in the history.
        if (DumpDir is null) { SkipNote(); return; }

        foreach (var path in Directory.GetFiles(DumpDir, "strategymap_*.json"))
        {
            var map = LoadDump(path);
            var ending = map.Events!.Single(HasEndBattle);
            var boss = ((HexaConditionUnitDead)ending.HexaConditions![0]).UnitEntityIds!;

            var survivors = map.HexaUnitList!
                .Where(u => !u.IsPlayer && !boss.Contains(u.EntityId))
                .ToDictionary(u => u.EntityId, u => u);

            var fired = HexaMapService.FindSatisfiedEndBattle(map, survivors, null);

            Assert.NotNull(fired);
            Assert.Equal(ending.EventId, fired!.Value.Event.EventId);

            var history = new Dictionary<long, List<long>> { [ending.EventId] = fired.Value.ConditionIds };
            Assert.Null(HexaMapService.FindSatisfiedEndBattle(map, survivors, history));
        }
    }

    // ------------------------------------------------------------------------------------- fixtures

    private static bool HasEndBattle(HexaEvent e) =>
        e.HexaCommands?.OfType<HexaCommandEndBattle>().Any() == true;

    private static HexaCommandEndBattle EndBattleOf(HexaEvent e) =>
        e.HexaCommands!.OfType<HexaCommandEndBattle>().Single();

    private static HexaTileMap LoadDump(string path) =>
        JsonConvert.DeserializeObject<HexaTileMap>(File.ReadAllText(path), new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new HexaMapSerializationBinder(),
        })!;

    // Null when the dumps are absent, in which case the dump-backed tests skip. They live in the
    // server's build output rather than the repo, same as the excel dumps.
    private static readonly string? DumpDir = LocateDumps();

    private static string? LocateDumps()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "Shittim-Server", "Shittim-Server.csproj")))
                continue;

            var server = Path.Combine(dir.FullName, "Shittim-Server");

            return new[]
            {
                Path.Combine(server, "Resources", "Dumped", "HexaMap"),
                Path.Combine(server, "bin", "Debug", "net10.0", "Resources", "Dumped", "HexaMap"),
                Path.Combine(server, "bin", "Release", "net10.0", "Resources", "Dumped", "HexaMap"),
            }.FirstOrDefault(Directory.Exists);
        }

        return null;
    }

    private void SkipNote() => output.WriteLine(
        "No Resources/Dumped/HexaMap found, so no real strategy map was checked. The dumps live in " +
        "the server's build output rather than the repo; run the server once, then re-run this test.");
}
