using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The decision pass mirrors the client's MX.GameLogic.Service.CampaignService, decompiled from GameAssembly.dll: DecideAIDestination / DecideGuard / DecidePursuit / ApplyAIMovementAndClearAIDestination / HasEncounter.
public class CampaignEnemyPhaseTests
{
    private static HexaTileMap Line(int length)
    {
        // tiles (0,0,0) .. (length-1, 0, -(length-1)), a straight corridor
        var map = new HexaTileMap { HexaTileList = new List<HexaTile>() };
        for (var i = 0; i < length; i++)
            map.HexaTileList.Add(new HexaTile { Location = new HexLocation { x = i, z = -i } });
        return map;
    }

    private static HexaUnit Unit(long entityId, long id, int x, int z, bool isPlayer, int movementOrder = 0) => new()
    {
        EntityId = entityId,
        Id = id,
        IsPlayer = isPlayer,
        MovementOrder = movementOrder,
        Location = new HexLocation2D { x = x, z = z },
    };

    private static CampaignMainStageSaveDBServer Save(params HexaUnit[] units) => new()
    {
        EchelonInfos = units.Where(u => u.IsPlayer).ToDictionary(u => u.EntityId, u => u),
        EnemyInfos = units.Where(u => !u.IsPlayer).ToDictionary(u => u.EntityId, u => u),
        DisplayInfos = new List<HexaDisplayInfo>(),
        StrategyObjects = new Dictionary<long, Strategy>(),
    };

    private static List<CampaignUnitExcelT> Excels(params (long Id, StrategyAIType Ai, int Range)[] rows) =>
        rows.Select(r => new CampaignUnitExcelT { Id = r.Id, AIMoveType = r.Ai, MoveRange = r.Range }).ToList();

    private static readonly List<CampaignStrategyObjectExcelT> NoStrategies = new();

    [Fact]
    public void AGuardNextToAnEchelonWalksOntoIt()
    {
        var save = Save(Unit(1, 0, 0, 0, isPlayer: true), Unit(10001, 900, 1, -1, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(Line(4), save, long.MinValue,
            Excels((900, StrategyAIType.Guard, 1)), NoStrategies);

        var move = Assert.Single(save.DisplayInfos);
        Assert.Equal(HexaDisplayType.MoveUnit, move.Type);
        Assert.Equal(10001, move.EntityId);
        Assert.Equal(0, move.Location.x);
        Assert.Equal(0, move.Location.z);
        // deciding is not moving - EnemyInfos holds the old tile until the second EndTurn applies it
        Assert.Equal(1, save.EnemyInfos[10001].Location!.x);
    }

    [Fact]
    public void AGuardOutOfReachStaysPut()
    {
        var save = Save(Unit(1, 0, 0, 0, isPlayer: true), Unit(10001, 900, 2, -2, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(Line(4), save, long.MinValue,
            Excels((900, StrategyAIType.Guard, 1)), NoStrategies);

        Assert.Empty(save.DisplayInfos);
    }

    [Fact]
    public void APursuerOutOfReachClosesOneStepAlongTheRoute()
    {
        var save = Save(Unit(1, 0, 0, 0, isPlayer: true), Unit(10001, 900, 3, -3, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(Line(4), save, long.MinValue,
            Excels((900, StrategyAIType.Pursuit, 1)), NoStrategies);

        var move = Assert.Single(save.DisplayInfos);
        Assert.Equal(2, move.Location.x);
        Assert.Equal(-2, move.Location.z);
    }

    [Fact]
    public void APursuerBlockedByAFellowEnemyStaysPut()
    {
        // the guard at distance 2 will not act, and the pursuer behind it has nowhere to step
        var save = Save(
            Unit(1, 0, 3, -3, isPlayer: true),
            Unit(10001, 900, 1, -1, isPlayer: false),
            Unit(10002, 901, 0, 0, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(Line(4), save, long.MinValue,
            Excels((900, StrategyAIType.Guard, 1), (901, StrategyAIType.Pursuit, 1)), NoStrategies);

        Assert.Empty(save.DisplayInfos);
    }

    [Fact]
    public void ACanNotMoveTileRoutesThePursuitAround()
    {
        var map = Line(3);
        map.HexaTileList!.Add(new HexaTile { Location = new HexLocation { x = 2, y = -1, z = -1 } });
        map.HexaTileList.Add(new HexaTile { Location = new HexLocation { x = 1, y = -1, z = 0 } });
        map.HexaTileList[1].CanNotMove = true;

        var save = Save(Unit(1, 0, 0, 0, isPlayer: true), Unit(10001, 900, 2, -2, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(map, save, long.MinValue,
            Excels((900, StrategyAIType.Pursuit, 1)), NoStrategies);

        // the straight line through (1,0,-1) is closed, so the detour's first step is (2,-1,-1)
        var move = Assert.Single(save.DisplayInfos);
        Assert.Equal(2, move.Location.x);
        Assert.Equal(-1, move.Location.y);
        Assert.Equal(-1, move.Location.z);
    }

    [Fact]
    public void TheFirstEngagingEnemyStopsThePass()
    {
        var save = Save(
            Unit(1, 0, 1, -1, isPlayer: true),
            Unit(10001, 900, 0, 0, isPlayer: false),
            Unit(10002, 900, 2, -2, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(Line(4), save, long.MinValue,
            Excels((900, StrategyAIType.Guard, 1)), NoStrategies);

        var move = Assert.Single(save.DisplayInfos);
        Assert.Equal(10001, move.EntityId);
    }

    [Fact]
    public void ThePassResumesAfterTheEngagedId()
    {
        var save = Save(
            Unit(1, 0, 1, -1, isPlayer: true),
            Unit(10001, 900, 0, 0, isPlayer: false),
            Unit(10002, 900, 2, -2, isPlayer: false));

        ConcentrateCampaignManager.DecideEnemyMoves(Line(4), save, 10001,
            Excels((900, StrategyAIType.Guard, 1)), NoStrategies);

        var move = Assert.Single(save.DisplayInfos);
        Assert.Equal(10002, move.EntityId);
    }

    [Fact]
    public void ApplyRelocatesEnemiesAndConsumesTheEntries()
    {
        var save = Save(Unit(10001, 900, 3, -3, isPlayer: false));
        save.DisplayInfos.Add(new HexaDisplayInfo { Type = HexaDisplayType.MoveUnit, EntityId = 10001, Location = new HexLocation { x = 2, z = -2 } });

        ConcentrateCampaignManager.ApplyEnemyMoves(save);

        Assert.Equal(2, save.EnemyInfos[10001].Location!.x);
        Assert.Equal(-2, save.EnemyInfos[10001].Location!.z);
        Assert.Empty(save.DisplayInfos);
    }

    [Fact]
    public void ApplyUpToTheEngagedIdLeavesLaterMovesPending()
    {
        var save = Save(Unit(10001, 900, 3, -3, isPlayer: false), Unit(10002, 900, 4, -4, isPlayer: false));
        save.DisplayInfos.Add(new HexaDisplayInfo { Type = HexaDisplayType.MoveUnit, EntityId = 10001, Location = new HexLocation { x = 2, z = -2 } });
        save.DisplayInfos.Add(new HexaDisplayInfo { Type = HexaDisplayType.MoveUnit, EntityId = 10002, Location = new HexLocation { x = 3, z = -3 } });

        ConcentrateCampaignManager.ApplyEnemyMoves(save, 10001);

        Assert.Equal(2, save.EnemyInfos[10001].Location!.x);
        Assert.Equal(4, save.EnemyInfos[10002].Location!.x);
        Assert.Equal(10002, Assert.Single(save.DisplayInfos).EntityId);
    }

    [Fact]
    public void APendingWalkOntoAnEchelonReadsAsAnEncounter()
    {
        var save = Save(Unit(1, 0, 0, 0, isPlayer: true), Unit(10001, 900, 1, -1, isPlayer: false));

        Assert.False(ConcentrateCampaignManager.HasEncounter(save));

        save.DisplayInfos.Add(new HexaDisplayInfo { Type = HexaDisplayType.MoveUnit, EntityId = 10001, Location = new HexLocation() });
        Assert.True(ConcentrateCampaignManager.HasEncounter(save));

        ConcentrateCampaignManager.ApplyEnemyMoves(save);
        Assert.True(ConcentrateCampaignManager.HasEncounter(save));
    }
}
