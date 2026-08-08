using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;

namespace Shittim_Server.Services;

// Keeps this install in step with the world raid coordinator: pulls the manifest (what raid, when), pushes the damage queued up locally, and pulls back the pooled remaining HP everyone else's damage produced. Runs only when a coordinator url is configured; without one WorldRaidService just serves its cached files.
public class WorldRaidSyncService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly ILogger<WorldRaidSyncService> logger;
    private readonly ExcelTableService excel;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private bool offline;

    public WorldRaidSyncService(ILogger<WorldRaidSyncService> logger, ExcelTableService excel)
    {
        this.logger = logger;
        this.excel = excel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = Config.Instance.ServerConfiguration.WorldRaidCoordinatorUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
            return;

        logger.LogInformation("World raid coordinator: {Url}", url);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnce(url);
                if (offline)
                {
                    offline = false;
                    logger.LogInformation("World raid coordinator is reachable again");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // one line the first time, then quiet - a coordinator that is down for the night would otherwise fill the log at every poll
                if (!offline)
                {
                    offline = true;
                    logger.LogWarning("World raid coordinator unreachable, running from cache: {Error}", ex.Message);
                }
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SyncOnce(string url)
    {
        var manifestJson = await http.GetStringAsync($"{url}/worldraid/manifest");
        var manifest = string.IsNullOrWhiteSpace(manifestJson) ? null : JsonSerializer.Deserialize<WorldRaidManifest>(manifestJson);

        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.minServerVersion) &&
            Version.TryParse(manifest.minServerVersion, out var min) && typeof(WorldRaidSyncService).Assembly.GetName().Version < min)
        {
            logger.LogWarning("World raid season {SeasonId} needs server {Min}, this is {Ours} - update to take part", manifest.seasonId, manifest.minServerVersion, typeof(WorldRaidSyncService).Assembly.GetName().Version);
            return;
        }

        var changed = JsonSerializer.Serialize(manifest) != JsonSerializer.Serialize(WorldRaidService.Manifest);
        if (changed)
        {
            WorldRaidService.SetManifest(manifest, excel);
            logger.LogInformation(manifest == null
                ? "World raid: coordinator reports no active raid, shipped season dates go back"
                : $"World raid: season {manifest.seasonId} ({manifest.name}) scheduled {manifest.open} .. {manifest.close}");
            // the raid dates only reach the player through the client's own ExcelDB, same lever as the event schedule
            try { EventScheduleService.Apply(excel); }
            catch (Exception ex) { logger.LogWarning(ex, "World raid dates could not be written into the client ExcelDB"); }
        }

        if (manifest == null)
            return;

        var flushed = false;
        foreach (var (groupId, damage) in WorldRaidService.PendingSnapshot())
        {
            var body = JsonSerializer.Serialize(new { serverId = WorldRaidService.ServerId, seasonId = manifest.seasonId, groupId, damage });
            var answer = await http.PostAsync($"{url}/worldraid/contribute", new StringContent(body, Encoding.UTF8, "application/json"));
            if (answer.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // the coordinator rejected this group outright (season rolled, or a boss it never declared) - retrying cannot succeed
                WorldRaidService.ConfirmFlushed(groupId, damage);
                continue;
            }
            answer.EnsureSuccessStatusCode();
            WorldRaidService.ConfirmFlushed(groupId, damage);
            var acked = JsonSerializer.Deserialize<WorldRaidWorldState>(await answer.Content.ReadAsStringAsync());
            if (acked != null)
                WorldRaidService.ApplyRemoteState(acked);
            flushed = true;
        }

        if (!flushed)
        {
            var state = JsonSerializer.Deserialize<WorldRaidWorldState>(await http.GetStringAsync($"{url}/worldraid/state"));
            if (state != null)
                WorldRaidService.ApplyRemoteState(state);
        }
    }
}
