using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Controllers.Api;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class CampaignMapMoveWireTests
{
    private static JObject Wire(CampaignMapMoveResponse response) =>
        JObject.Parse(JsonConvert.SerializeObject(response, GatewayController.OfficialPacketJsonSettings));

    private static HexaUnit Echelon(long entityId, int x, int y, int z, int movementOrder) => new()
    {
        EntityId = entityId,
        Id = entityId,
        IsPlayer = true,
        ActionCount = 1,
        ActionCountMax = 1,
        Mobility = 1,
        StrategySightRange = 1,
        MovementOrder = movementOrder,
        Location = new HexLocation2D { x = x, y = y, z = z },
        HpInfos = new Dictionary<long, long> { [252643612] = 9647 },
        DyingInfos = new Dictionary<long, long>(),
    };

    /// <summary>Official's two-echelon force after deploy: orders 1 and 2, both with an action left.</summary>
    private static CampaignMainStageSaveDB DeployedForce() => new()
    {
        AccountServerId = 1,
        StageUniqueId = 1161101,
        CampaignState = CampaignState.PlayerPhase,
        CurrentTurn = 1,
        EchelonInfos = new Dictionary<long, HexaUnit>
        {
            [1] = Echelon(1, -2, 2, 0, movementOrder: 1),
            [2] = Echelon(2, -1, -1, 2, movementOrder: 2),
        },
    };

    /// <summary>Echelon 1 stepping to {-1,2,-1}, as the manager leaves it: order 3, no action left.</summary>
    private static CampaignMainStageSaveDB AfterEchelonOneMoves()
    {
        var save = DeployedForce();
        save.EchelonInfos![1].Location = new HexLocation2D { x = -1, y = 2, z = -1 };
        save.EchelonInfos[1].MovementOrder = 3;
        save.EchelonInfos[1].ActionCount = 0;
        save.DisplayInfos = new List<HexaDisplayInfo>
        {
            new()
            {
                Type = HexaDisplayType.MoveUnit,
                EntityId = 1,
                Location = new HexLocation { x = -1, y = 2, z = -1 },
            },
        };
        return save;
    }

    private static HexaUnit PreMove() => new()
    {
        Location = new HexLocation2D { x = -2, y = 2, z = 0 },
        MovementOrder = 1,
        ActionCount = 1,
    };

    private static JObject MoveResponse() => Wire(new CampaignMapMoveResponse
    {
        EchelonEntityId = 1,
        SaveDataDB = ConcentrateCampaignManager.ShapeForWire(
            ConcentrateCampaignManager.RewindMovedEchelonForWire(AfterEchelonOneMoves(), 1, PreMove())),
    });

    [Fact]
    public void TheResponseNamesTheEchelonThatMoved()
    {
        Assert.Equal(1, (long)MoveResponse()["EchelonEntityId"]!);
    }

    [Fact]
    public void DisplayInfosCarriesTheStepAndNothingElse()
    {
        var display = (JArray)MoveResponse()["SaveDataDB"]!["DisplayInfos"]!;

        Assert.Single(display);
        Assert.Equal((long)HexaDisplayType.MoveUnit, (long)display[0]["Type"]!);
        Assert.Equal(1, (long)display[0]["EntityId"]!);
        Assert.Equal(-1, (int)display[0]["Location"]!["x"]!);
        Assert.Equal(2, (int)display[0]["Location"]!["y"]!);
        Assert.Equal(-1, (int)display[0]["Location"]!["z"]!);
    }

    [Fact]
    public void TheMoverIsReportedWhereItStoodBeforeTheStep()
    {
        var mover = MoveResponse()["SaveDataDB"]!["EchelonInfos"]!["1"]!;

        // Official answers echelon 1's move to {-1,2,-1} with it still at {-2,2,0}; y=0 is dropped by
        // DefaultValueHandling.Ignore, which is why z is read rather than asserted absent.
        Assert.Equal(-2, (int)mover["Location"]!["x"]!);
        Assert.Equal(2, (int)mover["Location"]!["y"]!);
        Assert.Equal(1, (long)mover["MovementOrder"]!);
        Assert.Equal(1, (long)mover["ActionCount"]!);
    }

    [Fact]
    public void RewindingLeavesTheRestOfTheForceAlone()
    {
        var other = MoveResponse()["SaveDataDB"]!["EchelonInfos"]!["2"]!;

        Assert.Equal(2, (long)other["MovementOrder"]!);
        Assert.Equal(1, (long)other["ActionCount"]!);
        Assert.Equal(2, (int)other["Location"]!["z"]!);
    }

    [Fact]
    public void RewindingIsWireOnlyAndDoesNotUndoTheSavedMove()
    {
        // The mapped wire copy shares HexaUnit references with the tracked entity, so a rewind that
        // wrote through them would roll the move back out of the database.
        var save = AfterEchelonOneMoves();
        var moved = save.EchelonInfos![1];

        ConcentrateCampaignManager.RewindMovedEchelonForWire(save, 1, PreMove());

        Assert.Equal(-1, moved.Location!.x);
        Assert.Equal(3, moved.MovementOrder);
        Assert.Equal(0, moved.ActionCount);
    }

    [Fact]
    public void AnExhaustedEchelonDropsActionCountFromTheWire()
    {
        // ActionCount 0 is the client's "this unit has already moved this turn". It travels as an
        // absent key, which is how official reports a spent echelon.
        var json = Wire(new CampaignMapMoveResponse
        {
            EchelonEntityId = 1,
            SaveDataDB = ConcentrateCampaignManager.ShapeForWire(AfterEchelonOneMoves()),
        });

        var mover = json["SaveDataDB"]!["EchelonInfos"]!["1"]!;

        Assert.Null(mover["ActionCount"]);
        Assert.Equal(3, (long)mover["MovementOrder"]!);
        Assert.Equal(-1, (int)mover["Location"]!["x"]!);
    }
}
