using System.Text.Json;

// The one shared piece of the world raid. Every Shittim install runs its own game server, so this is what makes the raid "server-wide": each install polls the manifest here (which season, when, how much HP), posts the damage its player dealt, and reads back the pooled remaining HP everyone produced together. The admin surface is what the raid console edits. Everything lives in a couple of json files next to the exe.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") == null)
    app.Urls.Add("http://0.0.0.0:5200");

var store = new RaidStore(AppContext.BaseDirectory);
store.Load();

// the shared secret the raid console sends as X-Admin-Token. Env var wins; otherwise one is minted on first boot and kept next to the exe.
var adminToken = Environment.GetEnvironmentVariable("SHITTIM_COORDINATOR_TOKEN");
if (string.IsNullOrWhiteSpace(adminToken))
{
    var tokenPath = Path.Combine(AppContext.BaseDirectory, "admin_token.txt");
    if (File.Exists(tokenPath))
        adminToken = File.ReadAllText(tokenPath).Trim();
    if (string.IsNullOrWhiteSpace(adminToken))
    {
        adminToken = Guid.NewGuid().ToString("N");
        File.WriteAllText(tokenPath, adminToken);
    }
}
Console.WriteLine($"admin token: {adminToken}");

bool Authorized(HttpRequest request) => request.Headers.TryGetValue("X-Admin-Token", out var token) && token == adminToken;

app.MapGet("/", () => Results.Text("shittim world raid coordinator - game servers talk to /worldraid, humans want the raid console"));

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ShittimCoordinator" }));

// empty body means no raid - the game servers treat whitespace as null
app.MapGet("/worldraid/manifest", () => store.Manifest == null ? Results.Text("") : Results.Json(store.Manifest));

app.MapGet("/worldraid/state", () => store.State == null ? Results.Text("") : Results.Json(store.Snapshot()));

// 409 tells the game server to drop the contribution instead of retrying it forever: the season rolled, or the group was never declared here
app.MapPost("/worldraid/contribute", (Contribution contribution) =>
{
    if (!store.Contribute(contribution))
        return Results.Conflict();
    return Results.Json(store.Snapshot());
});

app.MapGet("/admin/overview", (HttpRequest request) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    return Results.Json(new { manifest = store.Manifest, state = store.Snapshot(), servers = store.KnownServerCount() });
});

app.MapPut("/admin/manifest", (HttpRequest request, WorldRaidManifest manifest) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    store.SetManifest(manifest);
    return Results.Json(new { manifest = store.Manifest, state = store.Snapshot() });
});

app.MapDelete("/admin/manifest", (HttpRequest request) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    store.SetManifest(null);
    return Results.Ok();
});

app.MapPut("/admin/state", (HttpRequest request, WorldRaidWorldState state) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    store.ReplaceState(state);
    return Results.Json(store.Snapshot());
});

app.MapPost("/admin/reseed", (HttpRequest request) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    store.Reseed();
    return Results.Json(store.Snapshot());
});

app.Run();

class Contribution
{
    public string serverId { get; set; } = "";
    public long seasonId { get; set; }
    public long groupId { get; set; }
    public long damage { get; set; }
}

class RaidStore
{
    private readonly object gate = new();
    private readonly string manifestPath;
    private readonly string statePath;
    private readonly string contributorsPath;
    private Dictionary<long, HashSet<string>> contributors = new();

    public WorldRaidManifest? Manifest { get; private set; }
    public WorldRaidWorldState? State { get; private set; }

    public RaidStore(string dir)
    {
        manifestPath = Path.Combine(dir, "manifest.json");
        statePath = Path.Combine(dir, "state.json");
        contributorsPath = Path.Combine(dir, "contributors.json");
    }

    public void Load()
    {
        lock (gate)
        {
            Manifest = ReadFile<WorldRaidManifest>(manifestPath);
            State = ReadFile<WorldRaidWorldState>(statePath);
            contributors = ReadFile<Dictionary<long, HashSet<string>>>(contributorsPath) ?? new();
        }
    }

    public void SetManifest(WorldRaidManifest? manifest)
    {
        lock (gate)
        {
            var seasonChanged = manifest?.seasonId != Manifest?.seasonId;
            Manifest = manifest;
            if (manifest == null)
            {
                State = null;
                contributors = new();
                File.Delete(manifestPath);
                File.Delete(statePath);
                File.Delete(contributorsPath);
                return;
            }

            WriteFile(manifestPath, manifest);
            if (seasonChanged || State == null)
            {
                ReseedLocked();
            }
            else
            {
                // same season edited in place: bosses added later in the month get their pool now, running pools stay untouched
                foreach (var boss in manifest.bosses.Where(b => !State.bosses.ContainsKey(b.groupId)))
                    State.bosses[boss.groupId] = new WorldRaidBossState { remainingHP = boss.totalHP };
                WriteFile(statePath, State);
            }
        }
    }

    public void Reseed()
    {
        lock (gate)
            ReseedLocked();
    }

    // no excel tables on this side - the console supplies every boss HP through the manifest
    private void ReseedLocked()
    {
        if (Manifest == null)
            return;
        State = new WorldRaidWorldState { seasonId = Manifest.seasonId };
        foreach (var boss in Manifest.bosses)
            State.bosses[boss.groupId] = new WorldRaidBossState { remainingHP = boss.totalHP };
        contributors = new();
        WriteFile(statePath, State);
        WriteFile(contributorsPath, contributors);
    }

    public void ReplaceState(WorldRaidWorldState state)
    {
        lock (gate)
        {
            State = state;
            foreach (var (groupId, boss) in state.bosses)
                if (contributors.TryGetValue(groupId, out var set) && boss.participants < set.Count)
                    boss.participants = set.Count;
            WriteFile(statePath, State);
        }
    }

    public bool Contribute(Contribution contribution)
    {
        lock (gate)
        {
            if (Manifest == null || State == null || State.seasonId != contribution.seasonId)
                return false;
            if (!State.bosses.TryGetValue(contribution.groupId, out var boss))
                return false;

            if (contribution.damage > 0)
                boss.remainingHP = Math.Max(0, boss.remainingHP - contribution.damage);

            if (!contributors.TryGetValue(contribution.groupId, out var set))
                contributors[contribution.groupId] = set = new HashSet<string>();
            if (set.Add(contribution.serverId))
                WriteFile(contributorsPath, contributors);
            boss.participants = set.Count;

            WriteFile(statePath, State);
            return true;
        }
    }

    public int KnownServerCount()
    {
        lock (gate)
            return contributors.Values.SelectMany(x => x).Distinct().Count();
    }

    // copy under the lock so a contribution arriving mid-serialization cannot corrupt the response
    public WorldRaidWorldState? Snapshot()
    {
        lock (gate)
        {
            if (State == null)
                return null;
            var copy = new WorldRaidWorldState { seasonId = State.seasonId };
            foreach (var (groupId, boss) in State.bosses)
                copy.bosses[groupId] = new WorldRaidBossState { remainingHP = boss.remainingHP, participants = boss.participants };
            return copy;
        }
    }

    private static T? ReadFile<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteFile<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}

// wire shape shared with the coordinator and its admin gui - lowercase names are the json contract
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
