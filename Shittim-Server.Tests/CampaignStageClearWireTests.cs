using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.FlatData;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Controllers.Api;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

/// <summary>
/// Pins the stage clear on Campaign_TacticResult (6008) against the official capture of a full
/// chapter-11 mission (captures/NetworkLog official.txt, four tactic results, the fourth clearing).
///
/// Killing the last enemy on the hex map *is* the stage clear — there is no protocol for it, and
/// after that response official's client sends Campaign_List and goes back to the lobby. The one
/// and only signal is a single EndBattle entry in the response's DisplayInfos. We sent an empty
/// DisplayInfos on every tactic result, so the boss died, the map emptied, and the mission never
/// ended: the player was left on the cleared map with End Turn as the only thing to press.
///
/// The contract, read off line 529 of the capture:
///
///  * DisplayInfos holds exactly one entry, and that entry has exactly three keys — Type (1,
///    EndBattle), Parameter (1) and StageRewardInfo. EntityId, UniqueId and Location stay at their
///    defaults and are dropped by DefaultValueHandling.Ignore.
///  * StageRewardInfo carries six keys in order: FirstClearReward, ThreeStarReward,
///    StrategyObjectRewards, ParcelResultDB, EventContentBonusReward, CampaignStageHistoryDB.
///  * The nested reward fields are byte-identical to the response's own top-level copies.
///  * EnemyInfos, which every earlier response carries, is absent once the map is empty.
///  * The three non-clearing results carry no DisplayInfos at all.
/// </summary>
public class CampaignStageClearWireTests
{
    private static JObject Wire(CampaignTacticResultResponse response) =>
        JObject.Parse(JsonConvert.SerializeObject(response, GatewayController.OfficialPacketJsonSettings));

    private static List<ParcelInfo> Reward(ParcelType type, long id, long amount) =>
    [
        new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = type, Id = id },
            Amount = amount,
            Multiplier = BasisPoint.One,
            Probability = BasisPoint.One,
        },
    ];

    /// <summary>Official's chapter-11 clear: history stamped, both reward sets paid out.</summary>
    private static StrategyClearRewardInfo ClearReward() => new()
    {
        FirstClearReward = Reward(ParcelType.Equipment, 101005, 1),
        ThreeStarReward = Reward(ParcelType.Currency, 3, 30),
        StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
        EventContentBonusReward = new List<ParcelInfo>(),
        ParcelResultDB = new ParcelResultDB(),
        CampaignStageHistoryDB = new CampaignStageHistoryDB
        {
            ChapterUniqueId = 116,
            StageUniqueId = 1161101,
            TacticClearCountWithRankSRecord = 4,
            ClearTurnRecord = 5,
            Star1Flag = true,
            Star2Flag = true,
            Star3Flag = true,
        },
    };

    /// <summary>The save as the manager leaves it on the clearing tactic: map empty, run closed.</summary>
    private static CampaignMainStageSaveDB ClearedSave() => new()
    {
        StageUniqueId = 1161101,
        CurrentTurn = 5,
        EnemyClearCount = 4,
        TacticRankSCount = 4,
        LastEnemyEntityId = 10032,
        EnemyInfos = new Dictionary<long, HexaUnit>(),
        DisplayInfos = new List<HexaDisplayInfo>(),
    };

    private static CampaignTacticResultResponse ClearResponse()
    {
        var clearReward = ClearReward();

        return new CampaignTacticResultResponse
        {
            TacticRank = 3,
            CampaignStageHistoryDB = clearReward.CampaignStageHistoryDB,
            LevelUpCharacterDBs = new(),
            FirstClearReward = clearReward.FirstClearReward,
            ThreeStarReward = clearReward.ThreeStarReward,
            StrategyObjectRewards = clearReward.StrategyObjectRewards,
            ParcelResultDB = clearReward.ParcelResultDB,
            SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
                ConcentrateCampaignManager.ShapeForWire(ClearedSave()), clearReward),
        };
    }

    [Fact]
    public void ClearingTheMapEndsTheMission()
    {
        var display = (JArray)Wire(ClearResponse())["SaveDataDB"]!["DisplayInfos"]!;

        Assert.Single(display);
        Assert.Equal((long)HexaDisplayType.EndBattle, (long)display[0]["Type"]!);
    }

    [Fact]
    public void TheEndBattleEntryCarriesExactlyWhatOfficialSends()
    {
        var entry = (JObject)Wire(ClearResponse())["SaveDataDB"]!["DisplayInfos"]![0]!;

        // Official's entry is {"Type": 1, "Parameter": 1, "StageRewardInfo": {...}} and nothing else.
        Assert.Equal(
            new[] { "Type", "Parameter", "StageRewardInfo" },
            entry.Properties().Select(p => p.Name));
        Assert.Equal(1, (long)entry["Parameter"]!);
    }

    [Fact]
    public void StageRewardInfoCarriesTheSixOfficialKeysInOrder()
    {
        var info = (JObject)Wire(ClearResponse())["SaveDataDB"]!["DisplayInfos"]![0]!["StageRewardInfo"]!;

        // ClearReward/ExpReward/TotalReward/EventContentReward are declared on the type but never
        // sent; leaving them null is what keeps them off the wire.
        Assert.Equal(
            new[]
            {
                "FirstClearReward",
                "ThreeStarReward",
                "StrategyObjectRewards",
                "ParcelResultDB",
                "EventContentBonusReward",
                "CampaignStageHistoryDB",
            },
            info.Properties().Select(p => p.Name));
    }

    [Fact]
    public void TheNestedRewardsAreTheSameBytesAsTheTopLevelOnes()
    {
        var json = Wire(ClearResponse());
        var info = json["SaveDataDB"]!["DisplayInfos"]![0]!["StageRewardInfo"]!;

        foreach (var field in new[] { "FirstClearReward", "ThreeStarReward", "StrategyObjectRewards", "ParcelResultDB" })
            Assert.True(JToken.DeepEquals(json[field], info[field]), field);

        Assert.True(JToken.DeepEquals(json["CampaignStageHistoryDB"], info["CampaignStageHistoryDB"]));
    }

    [Fact]
    public void TheRewardsTheStageAdvertisesActuallyRideOnTheResponse()
    {
        // Both fields went out as empty lists on every response before this — no handler anywhere
        // computed a campaign clear reward, so a stage paid nothing for a first clear or three stars.
        var json = Wire(ClearResponse());

        Assert.Equal(101005, (long)json["FirstClearReward"]![0]!["Key"]!["Id"]!);
        Assert.Equal(30, (long)json["ThreeStarReward"]![0]!["Amount"]!);
        Assert.Equal(10000, (long)json["ThreeStarReward"]![0]!["Probability"]!["rawValue"]!);
    }

    [Fact]
    public void AnEmptiedMapDropsEnemyInfosLikeOfficial()
    {
        // EnemyInfos rides on every response from map entry onwards and empties exactly once, on the
        // clear. Official drops the key rather than sending {}.
        var save = Wire(ClearResponse())["SaveDataDB"]!;

        Assert.Null(save["EnemyInfos"]);
        Assert.Equal(4, (long)save["EnemyClearCount"]!);
        Assert.Equal(4, (long)save["TacticRankSCount"]!);
    }

    [Fact]
    public void EnemiesStillOnTheMapAreStillReported()
    {
        var save = ClearedSave();
        save.EnemyInfos = new Dictionary<long, HexaUnit> { [10032] = new() { EntityId = 10032 } };

        var json = JObject.Parse(JsonConvert.SerializeObject(
            ConcentrateCampaignManager.ShapeForWire(save), GatewayController.OfficialPacketJsonSettings));

        Assert.NotNull(json["EnemyInfos"]);
        Assert.Equal(10032, (long)json["EnemyInfos"]!["10032"]!["EntityId"]!);
    }

    [Fact]
    public void AWonBattleThatDoesNotFireTheEndBattleEventEndsNothing()
    {
        // Three of official's four tactic results are wins that do not clear the stage, and none of
        // them carries DisplayInfos. Attaching EndBattle to those would end the mission early.
        var save = ClearedSave();
        save.EnemyInfos = new Dictionary<long, HexaUnit> { [10032] = new() { EntityId = 10032 } };

        var json = Wire(new CampaignTacticResultResponse
        {
            TacticRank = 3,
            SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
                ConcentrateCampaignManager.ShapeForWire(save), clearReward: null),
        });

        Assert.Null(json["SaveDataDB"]!["DisplayInfos"]);
    }

    [Fact]
    public void TheClearingResponseCarriesTheFiredEventInTheActivationHistory()
    {
        // Official's history goes {"0":[0],"1":[0],"3":[0]} on the last EndTurn (capture line 517) to
        // {"0":[0],"1":[0],"3":[0],"2":[0]} on the clearing TacticResult (line 529) — event 2 being
        // EndBattle_Win. Appending it is what stops a replayed 6008 firing the clear twice.
        var save = ClearedSave();
        save.ActivatedHexaEventsAndConditions = new Dictionary<long, List<long>>
        {
            [0] = [0],
            [1] = [0],
            [3] = [0],
            [2] = [0],
        };

        var json = Wire(new CampaignTacticResultResponse
        {
            TacticRank = 3,
            SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
                ConcentrateCampaignManager.ShapeForWire(save), ClearReward()),
        });

        var history = (JObject)json["SaveDataDB"]!["ActivatedHexaEventsAndConditions"]!;

        Assert.Equal(new[] { "0", "1", "3", "2" }, history.Properties().Select(p => p.Name));
        Assert.Equal(new long[] { 0 }, history["2"]!.Values<long>());
    }

    [Fact]
    public void AClearWithEnemiesStillOnTheMapStillReportsThem()
    {
        // The clear is the map's boss-death event, so a stage can and usually does end with mobs
        // alive — on 321 of 321 dumped maps the boss is a strict subset of the roster. EnemyInfos and
        // the EndBattle entry are independent: reporting survivors must not suppress the clear.
        var save = ClearedSave();
        save.EnemyInfos = new Dictionary<long, HexaUnit> { [10027] = new() { EntityId = 10027 } };

        var json = Wire(new CampaignTacticResultResponse
        {
            TacticRank = 3,
            SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
                ConcentrateCampaignManager.ShapeForWire(save), ClearReward()),
        });

        Assert.NotNull(json["SaveDataDB"]!["EnemyInfos"]!["10027"]);
        Assert.Single((JArray)json["SaveDataDB"]!["DisplayInfos"]!);
        Assert.Equal(1, (long)json["SaveDataDB"]!["DisplayInfos"]![0]!["Parameter"]!);
    }

    [Fact]
    public void ParameterComesFromTheMapsEndBattleTypeRatherThanAConstant()
    {
        // No official sample of a Lose exists — none of the 321 dumps carries one — so this pins the
        // plumbing, not the capture: whatever HexaCommandEndBattle.EndBattleType says reaches the wire.
        var json = Wire(new CampaignTacticResultResponse
        {
            SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
                ConcentrateCampaignManager.ShapeForWire(ClearedSave()), ClearReward(), CampaignEndBattle.Lose),
        });

        Assert.Equal((long)CampaignEndBattle.Lose, (long)json["SaveDataDB"]!["DisplayInfos"]![0]!["Parameter"]!);
    }

    [Fact]
    public void ANoneEndBattleTypeStillReportsAWin()
    {
        // None serializes as 0, which DefaultValueHandling.Ignore drops entirely — and an absent
        // Parameter reads as a loss, so a victory would show the retreat screen. Coerce instead.
        var json = Wire(new CampaignTacticResultResponse
        {
            SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
                ConcentrateCampaignManager.ShapeForWire(ClearedSave()), ClearReward(), CampaignEndBattle.None),
        });

        var entry = json["SaveDataDB"]!["DisplayInfos"]![0]!;

        Assert.NotNull(entry["Parameter"]);
        Assert.Equal(1, (long)entry["Parameter"]!);
    }
}
