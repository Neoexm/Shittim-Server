using System.Text.Json;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.FlatData;
using Serilog;

namespace Shittim_Server.Services;

// The shared world boss pool. Every Shittim install runs its own game server, so a "server-wide" raid means each of them mirroring one HP pool hosted on the coordinator: the manifest (which season, when, how much HP) and the live remaining HP both come from there, damage dealt locally is queued and flushed back, and the last known answer is cached on disk so a boot without network still has a raid to show. With no coordinator url configured the same files act as a purely local raid, which is also what the tests drive.
public static class WorldRaidService
{
    private static string ManifestPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "worldraid_manifest.json");
    private static string StatePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "worldraid_state.json");
    private static string PendingPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "worldraid_pending.json");
    private static string IdPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "worldraid_id");

    private static readonly object _gate = new();
    private static Dictionary<long, long> _pending = new();
    private static string? _serverId;

    public static WorldRaidManifest? Manifest { get; private set; }
    public static WorldRaidWorldState? State { get; private set; }

    public static bool CoordinatorConfigured => !string.IsNullOrWhiteSpace(Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl);

    // identifies this install to the coordinator so participants counts distinct servers, not requests
    public static string ServerId
    {
        get
        {
            lock (_gate)
            {
                if (_serverId != null)
                    return _serverId;
                var path = Path.GetFullPath(IdPath);
                if (File.Exists(path))
                    _serverId = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(_serverId))
                {
                    _serverId = Guid.NewGuid().ToString("N");
                    File.WriteAllText(path, _serverId);
                }
                return _serverId;
            }
        }
    }

    public static void Load()
    {
        lock (_gate)
        {
            Manifest = ReadFile<WorldRaidManifest>(ManifestPath);
            State = ReadFile<WorldRaidWorldState>(StatePath);
            _pending = ReadFile<Dictionary<long, long>>(PendingPath) ?? new();
            if (Manifest != null)
                Log.Information("World raid: season {SeasonId} ({Name}) manifest cached, window {Open} .. {Close}", Manifest.seasonId, Manifest.name, Manifest.open, Manifest.close);
        }
    }

    public static void SetManifest(WorldRaidManifest? manifest, ExcelTableService excel)
    {
        lock (_gate)
        {
            var seasonChanged = manifest?.seasonId != Manifest?.seasonId;
            Manifest = manifest;
            if (manifest == null)
            {
                File.Delete(Path.GetFullPath(ManifestPath));
                State = null;
                File.Delete(Path.GetFullPath(StatePath));
                _pending = new();
                File.Delete(Path.GetFullPath(PendingPath));
                return;
            }

            WriteFile(ManifestPath, manifest);
            if (seasonChanged || State == null || State.seasonId != manifest.seasonId)
            {
                State = SeedState(manifest, excel);
                WriteFile(StatePath, State);
                _pending = new();
                WriteFile(PendingPath, _pending);
            }
        }
    }

    // The pool a fresh season starts from. Manifest bosses win; anything it doesn't name falls back to the excel row's HP so a bare manifest of just a season id and dates still works.
    private static WorldRaidWorldState SeedState(WorldRaidManifest manifest, ExcelTableService excel)
    {
        var state = new WorldRaidWorldState { seasonId = manifest.seasonId };
        var season = excel.GetTable<WorldRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == manifest.seasonId);
        var groups = excel.GetTable<WorldRaidBossGroupExcelT>();
        var interactiveGroups = excel.GetTable<InteractiveWorldRaidBossGroupExcelT>();

        // interactive seasons (854) spread their groups over per-phase rows, so the season's group list is the union of every phase's
        var seasonGroupIds = season?.OpenRaidBossGroupId ?? excel.GetTable<InteractiveWorldRaidSeasonManageExcelT>()
            .Where(x => x.SeasonId == manifest.seasonId)
            .SelectMany(x => x.OpenRaidBossGroupId)
            .Distinct()
            .ToList();

        foreach (var groupId in seasonGroupIds)
        {
            var declared = manifest.bosses.FirstOrDefault(b => b.groupId == groupId);
            var hp = declared?.totalHP ?? 0;
            // pools ship one column per region and the client draws the world bar as hp over the column for its own, which on the steam build is asia. Seeding from any other column opens the boss part-dead.
            if (hp == 0)
                hp = groups.FirstOrDefault(g => g.WorldRaidBossGroupId == groupId)?.WorldBossHPAsia ?? 0;
            if (hp == 0)
                hp = interactiveGroups.FirstOrDefault(g => g.WorldRaidBossGroupId == groupId)?.WorldBossHPAsia ?? 0;
            state.bosses[groupId] = new WorldRaidBossState { remainingHP = hp };
        }
        foreach (var declared in manifest.bosses.Where(b => !state.bosses.ContainsKey(b.groupId)))
            state.bosses[declared.groupId] = new WorldRaidBossState { remainingHP = declared.totalHP };

        return state;
    }

    public static long? RemainingHP(long groupId)
    {
        lock (_gate)
        {
            if (State == null || !State.bosses.TryGetValue(groupId, out var boss))
                return null;
            return boss.remainingHP;
        }
    }

    public static long Participants(long groupId)
    {
        lock (_gate)
        {
            if (State == null || !State.bosses.TryGetValue(groupId, out var boss))
                return 0;
            return boss.participants;
        }
    }

    // Damage is applied to the cached pool immediately so the local player watches the bar move without waiting a poll cycle, and queued for the coordinator, whose answer later overwrites the optimistic value.
    public static void AddDamage(long groupId, long damage)
    {
        if (damage <= 0)
            return;
        lock (_gate)
        {
            if (Manifest == null || State == null || !State.bosses.TryGetValue(groupId, out var boss))
                return;
            boss.remainingHP = Math.Max(0, boss.remainingHP - damage);
            if (boss.participants == 0)
                boss.participants = 1;
            WriteFile(StatePath, State);

            if (CoordinatorConfigured)
            {
                _pending.TryGetValue(groupId, out var queued);
                _pending[groupId] = queued + damage;
                WriteFile(PendingPath, _pending);
            }
        }
    }

    public static Dictionary<long, long> PendingSnapshot()
    {
        lock (_gate)
            return new Dictionary<long, long>(_pending);
    }

    // called after the coordinator acked a contribution; damage dealt while the POST was in flight stays queued
    public static void ConfirmFlushed(long groupId, long amount)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(groupId, out var queued))
                return;
            if (queued <= amount)
                _pending.Remove(groupId);
            else
                _pending[groupId] = queued - amount;
            WriteFile(PendingPath, _pending);
        }
    }

    public static void ApplyRemoteState(WorldRaidWorldState remote)
    {
        lock (_gate)
        {
            if (Manifest == null || remote.seasonId != Manifest.seasonId)
                return;
            State = remote;
            WriteFile(StatePath, State);
        }
    }

    // Manifest times are utc so the raid flips at the same instant on every install regardless of where the player lives. Everything downstream runs on this machine's wall clock - the DateTime.Now checks, the dates patched into the client's ExcelDB, and the clock the client syncs its own time to - so convert once at this border. ToLocalTime treats the parsed Unspecified value as utc, which is exactly the contract; the kind goes back to Unspecified so the values serialize like the shipped excel dates do.
    public static bool TryLocal(string? value, out DateTime local)
    {
        if (!DateTime.TryParse(value, out var parsed))
        {
            local = default;
            return false;
        }
        local = DateTime.SpecifyKind(parsed.ToLocalTime(), DateTimeKind.Unspecified);
        return true;
    }

    public static string LocalStamp(string value) => TryLocal(value, out var local) ? local.ToString("yyyy-MM-dd HH:mm:ss") : value;

    public static bool SeasonActive(DateTime now)
    {
        lock (_gate)
        {
            if (Manifest == null)
                return false;
            return TryLocal(Manifest.open, out var open) && TryLocal(Manifest.close, out var close) && open <= now && now < close;
        }
    }

    public static (DateTime Spawn, DateTime Eliminate)? BossWindow(long groupId)
    {
        lock (_gate)
        {
            if (Manifest == null)
                return null;
            var declared = Manifest.bosses.FirstOrDefault(b => b.groupId == groupId);
            var spawnText = declared?.spawnTime;
            var eliminateText = declared?.eliminateTime;
            if (string.IsNullOrEmpty(spawnText))
                spawnText = Manifest.open;
            if (string.IsNullOrEmpty(eliminateText))
                eliminateText = Manifest.close;
            if (!TryLocal(spawnText, out var spawn) || !TryLocal(eliminateText, out var eliminate))
                return null;
            return (spawn, eliminate);
        }
    }

    private static T? ReadFile<T>(string relative) where T : class
    {
        try
        {
            var path = Path.GetFullPath(relative);
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read {Path}", relative);
            return null;
        }
    }

    private static void WriteFile<T>(string relative, T value)
    {
        File.WriteAllText(Path.GetFullPath(relative), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}

// wire shape shared with the coordinator and its admin gui - lowercase names are the json contract, datetimes are utc
public class WorldRaidManifest
{
    public long seasonId { get; set; }
    public string name { get; set; } = "";
    public string open { get; set; } = "";
    public string close { get; set; } = "";
    public string exposed { get; set; } = "";
    public string extension { get; set; } = "";
    public string minServerVersion { get; set; } = "";
    public List<WorldRaidManifestBoss> bosses { get; set; } = new();
}

public class WorldRaidManifestBoss
{
    public long groupId { get; set; }
    public long totalHP { get; set; }
    public string spawnTime { get; set; } = "";
    public string eliminateTime { get; set; } = "";
}

public class WorldRaidWorldState
{
    public long seasonId { get; set; }
    public Dictionary<long, WorldRaidBossState> bosses { get; set; } = new();
}

public class WorldRaidBossState
{
    public long remainingHP { get; set; }
    public long participants { get; set; }
}
