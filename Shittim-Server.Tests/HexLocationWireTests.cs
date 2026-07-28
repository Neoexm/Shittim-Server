using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.MX.Campaign;
using Shittim_Server.Controllers.Api;
using Xunit;

namespace Shittim_Server.Tests;

/// <summary>
/// Pins the hex cube-coordinate contract for strategy-map entities (2026-07-28 official capture).
///
/// HexLocation2D used to type x/y as float and [JsonIgnore] z on both serializers. The strategymap
/// dumps carry full integer cube coordinates (x + y + z = 0), so z was silently zeroed on load and
/// dropped from the wire: every enemy and strategy object in Campaign_EnterMainStage's SaveDataDB
/// sat off-lattice, the client could not build the strategy map, and "Mission Start" on any
/// campaign main stage did nothing (the session log just stops after the 6001 response).
/// </summary>
public class HexLocationWireTests
{
    [Fact]
    public void DumpDeserializationKeepsZ()
    {
        // strategymap_1111102.json, first unit — the exact row that reached the wire without z.
        var loc = JsonConvert.DeserializeObject<HexLocation2D>("""{"x": 2, "y": -1, "z": -1}""")!;

        Assert.Equal(2, loc.x);
        Assert.Equal(-1, loc.y);
        Assert.Equal(-1, loc.z);
    }

    [Fact]
    public void WireCarriesNonZeroZAsInteger()
    {
        var unit = new HexaUnit { EntityId = 10040, Id = 111110201, Location = new HexLocation2D { x = 2, y = -1, z = -1 } };

        var json = JObject.Parse(JsonConvert.SerializeObject(unit, GatewayController.OfficialPacketJsonSettings));

        Assert.Equal(2, (int)json["Location"]!["x"]!);
        Assert.Equal(-1, (int)json["Location"]!["y"]!);
        Assert.Equal(-1, (int)json["Location"]!["z"]!);
        // Official emits integers ("x":2), never floats ("x":2.0).
        Assert.Equal(JTokenType.Integer, json["Location"]!["z"]!.Type);
    }

    [Fact]
    public void WireOmitsZeroComponentsLikeOfficial()
    {
        // Official 10016: "Location":{"x":-2,"y":2} — the z=0 component is dropped by the same
        // default-value handling as every other zero field.
        var unit = new HexaUnit { EntityId = 10016, Id = 101101, Location = new HexLocation2D { x = -2, y = 2, z = 0 } };

        var json = JObject.Parse(JsonConvert.SerializeObject(unit, GatewayController.OfficialPacketJsonSettings));

        Assert.Equal(-2, (int)json["Location"]!["x"]!);
        Assert.Null(json["Location"]!["z"]);
    }

    /// <summary>
    /// Rotate is the one member that must NOT follow the omit-zero rule. The client feeds it to
    /// Newtonsoft's VectorConverter, whose PopulateVector3 reads x/y/z with Extensions.Value&lt;float&gt;()
    /// and throws ArgumentNullException on a missing component — killing the whole
    /// CampaignEnterMainStageResponse deserialization inside SessionTask.Deserialize, which has no
    /// try/catch. The observed symptom was "Mission Start" leaving the client silent on the stage
    /// select screen. Official sends {"x":0.0,"y":240.0,"z":0.0}.
    /// </summary>
    [Fact]
    public void WireCarriesEveryRotateComponentEvenWhenZero()
    {
        var unit = new HexaUnit { EntityId = 10040, Id = 111110201, Rotate = new SimpleVector3 { x = 0f, y = 240f, z = 0f } };

        var json = JObject.Parse(JsonConvert.SerializeObject(unit, GatewayController.OfficialPacketJsonSettings));

        Assert.Equal(0f, (float)json["Rotate"]!["x"]!);
        Assert.Equal(240f, (float)json["Rotate"]!["y"]!);
        Assert.Equal(0f, (float)json["Rotate"]!["z"]!);
    }
}
