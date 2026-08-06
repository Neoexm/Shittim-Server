using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCommand;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCondition;
using System.Reflection;

namespace Shittim_Server.Services;

public class HexaMapService
{
    private readonly ILogger<HexaMapService> _logger;
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        SerializationBinder = new HexaMapSerializationBinder()
    };

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HexaTileMap> _hexaMapCache = new();
    private static readonly string _resourceDir = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory), 
        "Resources", 
        "Dumped", 
        "HexaMap"
    );

    public HexaMapService(ILogger<HexaMapService> logger)
    {
        _logger = logger;
    }

    public Task<HexaTileMap> LoadState(long stageUniqueId) => LoadState($"strategymap_{stageUniqueId}");

    // The string overload is for event stages: their map name comes off EventContentStageExcel.StrategyMap, and a rerun replays the original event's file, so stage 108013102 plays strategymap_8013102.
    public async Task<HexaTileMap> LoadState(string strategyMap)
    {
        var nameMap = strategyMap.ToLowerInvariant() + ".json";

        if (_hexaMapCache.ContainsKey(nameMap))
            return _hexaMapCache[nameMap];

        var filePath = Path.Combine(_resourceDir, nameMap);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("HexaMap file not found: {FilePath}", filePath);
            return CreateEmptyMap();
        }

        var json = await File.ReadAllTextAsync(filePath);
        var hexaData = JsonConvert.DeserializeObject<HexaTileMap>(json, _jsonSettings);

        if (hexaData != null)
        {
            _hexaMapCache[nameMap] = hexaData;
            _logger.LogDebug("HexaMap: {StrategyMap} loaded!", nameMap);
        }

        return hexaData ?? CreateEmptyMap();
    }

    private HexaTileMap CreateEmptyMap()
    {
        return new HexaTileMap
        {
            LastEntityId = 0,
            IsBig = false,
            HexaTileList = new List<HexaTile>(),
            HexaUnitList = new List<HexaUnit>(),
            HexaStrageyList = new List<Strategy>(),
            Events = new List<HexaEvent>()
        };
    }

    public static Dictionary<long, HexaUnit> AddHexaUnitList(List<HexaUnit>? hexaUnitData)
    {
        var unitInfos = new Dictionary<long, HexaUnit>();

        if (hexaUnitData == null)
            return unitInfos;

        foreach (var hexaUnit in hexaUnitData)
        {
            // Copy the dump verbatim. Official's enemy entries carry exactly EntityId/Id/Location/Rotate on the wire - the dump's zero stats and null HP/dying collections round-trip as-is (zeros and nulls drop in serialization; deployed PLAYER echelons are the ones that get live stats, in DeployConcentratedEchelon).
            // Null/zero enemy stats are NOT a "Now Loading" hang cause - official's working capture sends exactly this shape; that hang is the HexLocation2D z-coordinate drop.
            var unitInfo = new HexaUnit
            {
                EntityId = hexaUnit.EntityId,
                HpInfos = hexaUnit.HpInfos,
                DyingInfos = hexaUnit.DyingInfos,
                BuffInfos = hexaUnit.BuffInfos,
                ActionCount = hexaUnit.ActionCount,
                ActionCountMax = hexaUnit.ActionCountMax,
                Mobility = hexaUnit.Mobility,
                StrategySightRange = hexaUnit.StrategySightRange,
                MovementOrder = hexaUnit.MovementOrder,
                Id = hexaUnit.Id,
                IsPlayer = hexaUnit.IsPlayer,
                Rotate = hexaUnit.Rotate == null ? null : new SimpleVector3
                {
                    x = hexaUnit.Rotate.x,
                    y = hexaUnit.Rotate.y,
                    z = hexaUnit.Rotate.z
                }
            };

            if (hexaUnit.Location != null && (
                hexaUnit.Location.x != 0 ||
                hexaUnit.Location.y != 0 ||
                hexaUnit.Location.z != 0))
            {
                unitInfo.Location = hexaUnit.Location;
            }

            unitInfos.Add(hexaUnit.EntityId, unitInfo);
        }

        return unitInfos;
    }

    public static Dictionary<long, Strategy> AddHexaStrategyList(List<Strategy>? strategiesData)
    {
        var strategyDataInfos = new Dictionary<long, Strategy>();

        if (strategiesData == null)
            return strategyDataInfos;

        foreach (var strategyObject in strategiesData)
        {
            var strategyInfo = new Strategy
            {
                EntityId = strategyObject.EntityId,
                Id = strategyObject.Id,
                CampaignStrategyExcel = strategyObject.CampaignStrategyExcel,
                Rotate = new SimpleVector3
                {
                    x = strategyObject.Rotate?.x ?? 0f,
                    y = strategyObject.Rotate?.y ?? 0f,
                    z = strategyObject.Rotate?.z ?? 0f
                }
            };

            if (strategyObject.Location != null && (
                strategyObject.Location.x != 0 || 
                strategyObject.Location.y != 0 || 
                strategyObject.Location.z != 0))
            {
                strategyInfo.Location = strategyObject.Location;
            }

            strategyDataInfos.Add(strategyObject.EntityId, strategyInfo);
        }

        return strategyDataInfos;
    }

    public static Dictionary<int, HexaTileState> AddHexaTileList(HexaTileMap hexaTileMap)
    {
        // TileMapStates is keyed by the tile's index in HexaTileList, and HexaTileState.Id must be that same index (a HexaTile has no numeric id, only a Location).
        // Padding the front of the map with one blank tile per strategy object shifts every real tile's index/Id by StrategyCount and desyncs the client's hex grid from the tile list.
        var tileDataset = new Dictionary<int, HexaTileState>();

        if (hexaTileMap.HexaTileList == null)
            return tileDataset;

        for (var i = 0; i < hexaTileMap.HexaTileList.Count; i++)
        {
            var tileData = hexaTileMap.HexaTileList[i];
            tileDataset.Add(i, new HexaTileState
            {
                Id = i,
                CanNotMove = tileData.CanNotMove,
                IsFog = tileData.IsFog,
                IsHide = tileData.IsHide
            });
        }

        return tileDataset;
    }

    public static List<HexaUnit> DeployHexaUnitList(List<HexaUnit> hexaUnitData)
    {
        var unitInfos = new List<HexaUnit>();

        foreach (var hexaUnit in hexaUnitData)
        {
            var unitInfo = new HexaUnit
            {
                EntityId = hexaUnit.EntityId,
                DyingInfos = new Dictionary<long, long>(),
                Id = hexaUnit.Id,
                Location = hexaUnit.Location,
                IsPlayer = hexaUnit.IsPlayer
            };

            unitInfos.Add(unitInfo);
        }

        return unitInfos;
    }

    public static HexaDisplayInfo AddHexaDisplayInfo(long entityId, HexLocation destLocation)
    {
        return new HexaDisplayInfo
        {
            Type = HexaDisplayType.MoveUnit,
            EntityId = entityId,
            Location = destLocation
        };
    }

    // The map's stage-clear event, if its conditions are now satisfied.
    // Clearing a stage is data-driven, not "the map is empty": all 321 dumped strategy maps carry exactly one HexaCommandEndBattle gated by one HexaConditionUnitDead naming a single designated boss, never the whole roster. On strategymap_1011104 the boss is 10013 while 10017 and 10018 are still standing when official ends the mission.
    // Requiring an empty map makes the player mop up units official never asks for, and can never be satisfied where a TileHide removes one.
    // Membership is tested against the surviving enemies rather than the unit just killed, so this keeps working once TileHide and UnitDie start removing units too. The returned objects belong to the process-wide map cache.
    public static (HexaEvent Event, List<long> ConditionIds, HexaCommandEndBattle Command)? FindSatisfiedEndBattle(
        HexaTileMap map,
        IReadOnlyDictionary<long, HexaUnit>? survivingEnemies,
        IReadOnlyDictionary<long, List<long>>? alreadyActivated)
    {
        if (map.Events == null)
            return null;

        foreach (var hexaEvent in map.Events)
        {
            var endBattle = hexaEvent.HexaCommands?.OfType<HexaCommandEndBattle>().FirstOrDefault();
            if (endBattle == null)
                continue;

            // A replayed Campaign_TacticResult must not fire the clear twice.
            if (alreadyActivated?.ContainsKey(hexaEvent.EventId) == true)
                continue;

            // No conditions means nothing to satisfy, not "satisfied by default" - this is the guard that stops an eventless or malformed map from clearing itself for free.
            var conditions = hexaEvent.HexaConditions;
            if (conditions == null || conditions.Count == 0)
                continue;

            // Every dump uses And. Or would need each condition's own trigger history to evaluate correctly, so rather than guess it,
            // leave the event unfired and let the caller's fallback end the run.
            if (hexaEvent.MultipleConditionCheckType != MultipleConditionCheckType.And)
                continue;

            // Conservative on purpose: a condition type we do not evaluate (ArriveTile, EveryTurn, ...) is treated as unsatisfied rather than skipped, so a partially-understood event can never fire early.
            if (!conditions.All(c => IsUnitDeadConditionSatisfied(c, survivingEnemies)))
                continue;

            return (hexaEvent, conditions.Select(c => c.ConditionId).ToList(), endBattle);
        }

        return null;
    }

    private static bool IsUnitDeadConditionSatisfied(
        HexaCondition condition,
        IReadOnlyDictionary<long, HexaUnit>? survivingEnemies)
    {
        if (condition is not HexaConditionUnitDead unitDead)
            return false;

        // An empty id list is vacuously true and clears the stage on the first kill.
        if (unitDead.UnitEntityIds == null || unitDead.UnitEntityIds.Count == 0)
            return false;

        return unitDead.UnitEntityIds.All(id => survivingEnemies?.ContainsKey(id) != true);
    }
}

public class HexaMapSerializationBinder : ISerializationBinder
{
    private static readonly string SchaleAssemblyName = "Schale";

    public Type BindToType(string? assemblyName, string typeName)
    {
        if (assemblyName != null && assemblyName.StartsWith("BlueArchive", StringComparison.OrdinalIgnoreCase))
            assemblyName = SchaleAssemblyName;

        var qn = $"{assemblyName}.{typeName}, {assemblyName}";
        var t = Type.GetType(qn);

        if (t == null)
            throw new JsonSerializationException($"Could not resolve type '{qn}'");

        return t;
    }

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = SchaleAssemblyName;
        typeName = serializedType.FullName;
    }
}

public static class HexaMapServiceExtensions
{
    public static void AddHexaMapService(this IServiceCollection services)
    {
        services.AddSingleton<HexaMapService>();
    }
}
