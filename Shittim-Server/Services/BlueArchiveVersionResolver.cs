using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.IO;

namespace BlueArchiveAPI.Services
{
    public class VersionIdCache
    {
        public string? VersionId { get; set; }
        public string? CdnBaseUrl { get; set; }
        public string? SourceBuildVersion { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    public class BlueArchiveVersionResolver
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private const string CacheFilePath = "./config/ba_version_cache.json";

        // PureAPK constants
        private const string PureApkVersionUrl = "https://api.pureapk.com/m/v3/cms/app_version?hl=en-US&package_name=com.nexon.bluearchive";
        
        // Nexon Patch API constants
        private const string NexonPatchApiUrl = "https://api-pub.nexon.com/patch/v1.1/version-check";
        private const string MarketGameId = "com.nexon.bluearchive";
        private const string MarketCode = "playstore";

        public BlueArchiveVersionResolver(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetGlobalAndroidVersionAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Fetching Global Android version from PureAPK...");

            using var request = new HttpRequestMessage(HttpMethod.Get, PureApkVersionUrl);
            request.Headers.Add("x-cv", "3172501");
            request.Headers.Add("x-sv", "29");
            request.Headers.Add("x-abis", "arm64-v8a,armeabi-v7a,armeabi");
            request.Headers.Add("x-gp", "1");

            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(ct);

                // Extract version using Regex: X.Y.Z
                var match = Regex.Match(body, @"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)");
                
                if (match.Success)
                {
                    var version = match.Value;
                    _logger.LogInformation("Found Global Android version: {Version}", version);
                    return version;
                }

                throw new Exception("Could not find a valid version string in PureAPK response.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch version from PureAPK.");
                throw new Exception("VersionLookupException: Failed to fetch version from PureAPK.", ex);
            }
        }

        public async Task<string> GetResourcePathAsync(CancellationToken ct = default)
        {
            var currBuildVersion = await GetGlobalAndroidVersionAsync(ct);
            var parts = currBuildVersion.Split('.');
            var currBuildNumber = parts[^1];

            _logger.LogInformation("Checking Nexon Patch API for version {Version} (Build {Build})...", currBuildVersion, currBuildNumber);

            var payload = new
            {
                market_game_id = MarketGameId,
                market_code = MarketCode,
                curr_build_version = currBuildVersion,
                curr_build_number = currBuildNumber
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.PostAsync(NexonPatchApiUrl, content, ct);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error))
                {
                    var type = error.GetProperty("type").GetString();
                    var code = error.GetProperty("code").GetInt32();
                    var message = error.GetProperty("message").GetString();
                    
                    _logger.LogError("Nexon Patch API returned error: {Type} ({Code}) - {Message}", type, code, message);
                    throw new Exception($"PatchVersionCheckException: {message} ({code})");
                }

                if (root.TryGetProperty("patch", out var patch) && patch.TryGetProperty("resource_path", out var resourcePath))
                {
                    var path = resourcePath.GetString();
                    if (string.IsNullOrEmpty(path))
                    {
                        throw new Exception("Nexon Patch API returned empty resource_path.");
                    }
                    _logger.LogInformation("Received resource path: {ResourcePath}", path);
                    return path;
                }

                throw new Exception("Nexon Patch API response did not contain 'patch.resource_path'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get resource path from Nexon Patch API.");
                throw;
            }
        }

        public static (string VersionId, string CdnBaseUrl) ParseVersionIdFromResourcePath(string resourcePath)
        {
            if (!Uri.TryCreate(resourcePath, UriKind.Absolute, out _))
            {
                throw new ArgumentException($"Invalid resource path URI: {resourcePath}");
            }

            // Pattern: .../com.nexon.bluearchive/<VersionId>/resource-data.json
            var match = Regex.Match(resourcePath, @"com\.nexon\.bluearchive/([^/]+)/resource-data\.json", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new Exception($"Could not extract VersionId from resource path: {resourcePath}");
            }

            var versionId = match.Groups[1].Value;
            
            // CdnBaseUrl is resourcePath without /resource-data.json
            var baseUrl = resourcePath.Substring(0, resourcePath.LastIndexOf("/resource-data.json", StringComparison.Ordinal));

            return (versionId, baseUrl);
        }

        public async Task<(string VersionId, string CdnBaseUrl)> GetOrUpdateVersionIdAsync(
            string? overrideVersionId = null,
            string? overrideCdnBaseUrl = null,
            bool forceRefresh = false,
            CancellationToken ct = default)
        {
            // 1. Check for manual overrides
            if (!string.IsNullOrWhiteSpace(overrideVersionId) && !string.IsNullOrWhiteSpace(overrideCdnBaseUrl))
            {
                _logger.LogWarning("Using manual override for VersionId: {VersionId} and CdnBaseUrl: {CdnBaseUrl}", overrideVersionId, overrideCdnBaseUrl);
                return (overrideVersionId, overrideCdnBaseUrl);
            }

            // 2. Try to load from cache
            VersionIdCache? cache = null;
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    var json = await File.ReadAllTextAsync(CacheFilePath, ct);
                    cache = JsonSerializer.Deserialize<VersionIdCache>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load version cache.");
            }

            if (!forceRefresh && cache != null && 
                !string.IsNullOrEmpty(cache.VersionId) && 
                !string.IsNullOrEmpty(cache.CdnBaseUrl) &&
                (DateTime.UtcNow - cache.LastUpdatedUtc).TotalHours < 12)
            {
                _logger.LogInformation("Using cached VersionId: {VersionId} (Last updated: {LastUpdated})", cache.VersionId, cache.LastUpdatedUtc);
                return (cache.VersionId, cache.CdnBaseUrl);
            }

            // 3. Fetch fresh data
            try
            {
                var resourcePath = await GetResourcePathAsync(ct);
                var (versionId, cdnBaseUrl) = ParseVersionIdFromResourcePath(resourcePath);

                // 4. Save to cache
                var newCache = new VersionIdCache
                {
                    VersionId = versionId,
                    CdnBaseUrl = cdnBaseUrl,
                    SourceBuildVersion = await GetGlobalAndroidVersionAsync(ct), // Re-fetching might be redundant but safe, or we can cache it from GetResourcePathAsync if we refactor. 
                    // Actually GetResourcePathAsync calls GetGlobalAndroidVersionAsync internally. 
                    // To avoid double call, we could refactor, but for now let's just store what we have.
                    // Or better, just store the versionId and cdnBaseUrl.
                    LastUpdatedUtc = DateTime.UtcNow
                };

                try
                {
                    var dir = Path.GetDirectoryName(CacheFilePath);
                    if (dir != null && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(CacheFilePath, JsonSerializer.Serialize(newCache, options), ct);
                    _logger.LogInformation("Updated version cache.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save version cache.");
                }

                return (versionId, cdnBaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update version info from remote APIs.");

                // Fallback to cache if available, even if expired
                if (cache != null && !string.IsNullOrEmpty(cache.VersionId) && !string.IsNullOrEmpty(cache.CdnBaseUrl))
                {
                    _logger.LogWarning("Falling back to expired cache: {VersionId}", cache.VersionId);
                    return (cache.VersionId, cache.CdnBaseUrl);
                }

                throw;
            }
        }
    }
}
