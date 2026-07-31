using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Controllers.Api;
using Xunit;

namespace Shittim_Server.Tests;

public class ContentSaveGetWireTests
{
    private static JObject Wire(ContentSaveGetResponse response) =>
        JObject.Parse(JsonConvert.SerializeObject(response, GatewayController.OfficialPacketJsonSettings));

    private static CampaignMainStageSaveDB OpenRun()
    {
        var save = new CampaignMainStageSaveDB
        {
            AccountServerId = 1,
            StageUniqueId = 1111102,
            CampaignState = CampaignState.PlayerPhase,
            CurrentTurn = 2,
            EnemyInfos = new Dictionary<long, HexaUnit>
            {
                [10040] = new() { EntityId = 10040, Id = 111110201, Location = new HexLocation2D { x = 2, y = -1, z = -1 } },
            },
        };

        // CampaignMainStageSaveDB hides ContentType with a `new static` property, so the only way to set it is through the base member - which is exactly the one AutoMapper writes when the server entity is mapped onto the wire model.
        ((ContentSaveDB)save).ContentType = ContentType.CampaignMainStage;

        return save;
    }

    [Fact]
    public void AnOpenRunPutsHasValidDataOnTheWire()
    {
        // true survives DefaultValueHandling.Ignore; a false here vanishes from the wire entirely.
        var json = Wire(new ContentSaveGetResponse { HasValidData = true, ContentSaveDB = OpenRun() });

        Assert.True((bool)json["HasValidData"]!);
    }

    [Fact]
    public void NoOpenRunLeavesTheResponseBareLikeOfficialsLoginAnswer()
    {
        // The only ContentSave_Get in the official capture is at login with no run in progress, and it comes back as nothing but the header.
        var json = Wire(new ContentSaveGetResponse());

        Assert.Null(json["HasValidData"]);
        Assert.Null(json["ContentSaveDB"]);
    }

    [Fact]
    public void TheSaveTravelsUnderTheKeyTheClientParses()
    {
        var json = Wire(new ContentSaveGetResponse { HasValidData = true, ContentSaveDB = OpenRun() });

        Assert.NotNull(json["ContentSaveDB"]);
        Assert.Equal(1111102, (long)json["ContentSaveDB"]!["StageUniqueId"]!);
    }

    [Fact]
    public void TheSaveCarriesContentTypeSoTheClientCanPickTheConcreteType()
    {
        // ContentSaveDBService.TryParseContentSaveDB hands the body to ContentSaveDBFactory, which needs ContentType to know it is looking at a campaign run.
        // CampaignMainStageSaveDB shadows the base member with a `new static` property, so that it reaches the wire at all is worth holding still - the response types it through the abstract ContentSaveDB base.
        var json = Wire(new ContentSaveGetResponse { HasValidData = true, ContentSaveDB = OpenRun() });

        Assert.Equal((long)ContentType.CampaignMainStage, (long)json["ContentSaveDB"]!["ContentType"]!);
    }

    [Fact]
    public void TheSaveKeepsTheMapTheClientHasToRebuildTheBattleFrom()
    {
        var json = Wire(new ContentSaveGetResponse { HasValidData = true, ContentSaveDB = OpenRun() });

        var enemy = json["ContentSaveDB"]!["EnemyInfos"]!["10040"]!;
        Assert.Equal(111110201, (long)enemy["Id"]!);
        Assert.Equal(-1, (int)enemy["Location"]!["z"]!);
    }
}
