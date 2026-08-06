using Newtonsoft.Json;
using Schale.FlatData;
using Schale.MX.Campaign;

namespace Shittim_Server.Services;

public enum TBGTileType
{
    None = 0,
    Start = 1,
    Movable = 2,
    UnMovable = 3
}

public enum TBGHexaObjectSpawnRule
{
    Nothing = 0,
    ObjectId = 1,
    ObjectType = 2
}

public class TBGHexaTileData
{
    public string? ResourcePath { get; set; }
    public TBGTileType TileType { get; set; }
    public HexLocation Location { get; set; }
}

public class TBGHexaSpawnData
{
    public TBGHexaObjectSpawnRule SpawnRule { get; set; }
    public HexLocation Location { get; set; }
    public List<long>? UniqueIds { get; set; }
    public List<TBGObjectType>? ObjectTypes { get; set; }
    public long ShuffleGroupId { get; set; }
}

public class TBGHexaMapData
{
    public List<TBGHexaTileData> Tiles { get; set; } = [];
    public List<TBGHexaSpawnData> Spawns { get; set; } = [];
}

public class TableBoardMapService
{
    private readonly ILogger<TableBoardMapService> _logger;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TBGHexaMapData> _mapCache = new();
    private static readonly string _resourceDir = Path.Join(
        Path.GetDirectoryName(AppContext.BaseDirectory),
        "Resources",
        "Dumped",
        "TableBoardMapData"
    );

    public TableBoardMapService(ILogger<TableBoardMapService> logger)
    {
        _logger = logger;
    }

    // MinigameTBGThemaExcel.ThemaMap spells the map "TBGMap_828011" while the bundle ships it as tbgmap_828011.json
    public TBGHexaMapData? Load(string themaMap)
    {
        var key = themaMap.ToLowerInvariant();
        if (_mapCache.TryGetValue(key, out var cached))
            return cached;

        var filePath = Path.Combine(_resourceDir, key + ".json");
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("TableBoard map file not found: {FilePath}", filePath);
            return null;
        }

        var map = JsonConvert.DeserializeObject<TBGHexaMapData>(File.ReadAllText(filePath));
        if (map != null)
        {
            _mapCache[key] = map;
            _logger.LogDebug("TableBoard map {ThemaMap} loaded, {TileCount} tiles / {SpawnCount} spawns", themaMap, map.Tiles.Count, map.Spawns.Count);
        }

        return map;
    }
}
