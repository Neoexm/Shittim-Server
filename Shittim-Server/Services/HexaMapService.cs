using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
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

    private static readonly Dictionary<long, HexaTileMap> _hexaMapCache = new();
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

    public async Task<HexaTileMap> LoadState(long stageUniqueId)
    {
        if (_hexaMapCache.ContainsKey(stageUniqueId))
            return _hexaMapCache[stageUniqueId];

        var nameMap = $"strategymap_{stageUniqueId}.json";
        var filePath = Path.Combine(_resourceDir, nameMap);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("HexaMap file not found: {FilePath}", filePath);
            return CreateEmptyMap(stageUniqueId);
        }

        var json = await File.ReadAllTextAsync(filePath);
        var hexaData = JsonConvert.DeserializeObject<HexaTileMap>(json, _jsonSettings);

        if (hexaData != null)
        {
            _hexaMapCache[stageUniqueId] = hexaData;
            _logger.LogDebug("HexaMap: {StrategyMap} loaded!", nameMap);
        }

        return hexaData ?? CreateEmptyMap(stageUniqueId);
    }

    private HexaTileMap CreateEmptyMap(long stageUniqueId)
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

    public static Dictionary<long, HexaUnit> AddHexaUnitList(List<HexaUnit> hexaUnitData)
    {
        var unitInfos = new Dictionary<long, HexaUnit>();

        foreach (var hexaUnit in hexaUnitData)
        {
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
                Id = hexaUnit.Id,
                Rotate = new SimpleVector3
                {
                    x = hexaUnit.Rotate?.x != 0 ? hexaUnit.Rotate.x : 0.20f,
                    y = hexaUnit.Rotate?.y != 0 ? hexaUnit.Rotate.y : 0.20f,
                    z = hexaUnit.Rotate?.z != 0 ? hexaUnit.Rotate.z : 0.20f
                },
                AIDestination = hexaUnit.AIDestination,
                IsActionComplete = hexaUnit.IsActionComplete,
                IsPlayer = hexaUnit.IsPlayer,
                MovementOrder = hexaUnit.MovementOrder,
                RewardParcelInfosWithDropTacticEntityType = hexaUnit.RewardParcelInfosWithDropTacticEntityType,
                SkillCardHand = hexaUnit.SkillCardHand,
                PlayAnimation = hexaUnit.PlayAnimation
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

    public static Dictionary<long, Strategy> AddHexaStrategyList(List<Strategy> strategiesData)
    {
        var strategyDataInfos = new Dictionary<long, Strategy>();

        foreach (var strategyObject in strategiesData)
        {
            var strategyInfo = new Strategy
            {
                EntityId = strategyObject.EntityId,
                Id = strategyObject.Id,
                CampaignStrategyExcel = strategyObject.CampaignStrategyExcel,
                Rotate = new SimpleVector3
                {
                    x = strategyObject.Rotate?.x != 0 ? strategyObject.Rotate.x : 0.20f,
                    y = strategyObject.Rotate?.y != 0 ? strategyObject.Rotate.y : 0.20f,
                    z = strategyObject.Rotate?.z != 0 ? strategyObject.Rotate.z : 0.20f
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
        var tileDataset = new Dictionary<int, HexaTileState>();
        var i = 0;

        if (hexaTileMap.HexaStrageyList != null)
        {
            foreach (var _ in hexaTileMap.HexaStrageyList)
            {
                tileDataset.Add(i, new HexaTileState());
                i++;
            }
        }

        if (hexaTileMap.HexaTileList != null)
        {
            foreach (var tileData in hexaTileMap.HexaTileList)
            {
                var tileSet = new HexaTileState
                {
                    Id = i,
                    CanNotMove = tileData.CanNotMove,
                    IsFog = tileData.IsFog,
                    IsHide = tileData.IsHide
                };
                tileDataset.Add(i, tileSet);
                i++;
            }
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
                DyingInfos = hexaUnit.DyingInfos,
                Id = hexaUnit.Id,
                Rotate = new SimpleVector3
                {
                    x = hexaUnit.Rotate?.x != 0 ? hexaUnit.Rotate.x : 0.20f,
                    y = hexaUnit.Rotate?.y != 0 ? hexaUnit.Rotate.y : 0.20f,
                    z = hexaUnit.Rotate?.z != 0 ? hexaUnit.Rotate.z : 0.20f
                },
                IsPlayer = hexaUnit.IsPlayer
            };

            if (hexaUnit.Location != null && (
                hexaUnit.Location.x != 0 || 
                hexaUnit.Location.y != 0 || 
                hexaUnit.Location.z != 0))
            {
                unitInfo.Location = hexaUnit.Location;
            }

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
}

public class HexaMapSerializationBinder : ISerializationBinder
{
    private static readonly string SchaleAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

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
