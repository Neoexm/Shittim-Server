using System.Text.Json;
using System.Text.Json.Serialization;
using BlueArchiveAPI.Configuration;

namespace BlueArchiveAPI.Services
{
    public class ServerInfoData
    {
        [JsonPropertyName("Mapping")]
        public ServerInfoMapping? Mapping { get; set; }

        [JsonPropertyName("CurrentVersion")]
        public long CurrentVersion { get; set; }

        [JsonPropertyName("MinimumVersion")]
        public long MinimumVersion { get; set; }
    }

    public class ServerInfoMapping
    {
        [JsonPropertyName("Resources")]
        public ResourcesMapping? Resources { get; set; }
    }

    public class ResourcesMapping
    {
        [JsonPropertyName("AddressablesCatalogUrlRoot")]
        public string? AddressablesCatalogUrlRoot { get; set; }
    }

    public class VersionFetcherService
    {
        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static VersionFetcherService()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "UnityPlayer/2022.3.21f1 (UnityWebRequest/1.0, libcurl/8.5.0-DEV)");
        }

        /// <summary>
        /// Fetches the latest version info from Nexon's CDN
        /// </summary>
        public static async Task<string?> FetchLatestVersionId(string serverInfoUrl)
        {
            try
            {
                Console.WriteLine($"Fetching latest version from: {serverInfoUrl}");
                
                var json = await httpClient.GetStringAsync(serverInfoUrl);
                var serverInfo = JsonSerializer.Deserialize<ServerInfoData>(json);

                if (serverInfo?.Mapping?.Resources?.AddressablesCatalogUrlRoot != null)
                {
                    var catalogUrl = serverInfo.Mapping.Resources.AddressablesCatalogUrlRoot;
                    
                    // Extract version ID from URL
                    // Format: https://ba.dn.nexoncdn.co.kr/com.nexon.bluearchive/{VERSION_ID}/
                    var uri = new Uri(catalogUrl);
                    var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    
                    if (segments.Length >= 2)
                    {
                        var versionId = segments[1]; // Should be the version ID
                        Console.WriteLine($"✓ Detected latest version: {versionId}");
                        Console.WriteLine($"  Game Version: {serverInfo.CurrentVersion}");
                        Console.WriteLine($"  Min Version: {serverInfo.MinimumVersion}");
                        return versionId;
                    }
                }

                Console.WriteLine("✗ Failed to extract version ID from server info");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"✗ Failed to fetch version from server: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error parsing version info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if resources need updating and returns the latest version if available
        /// </summary>
        public static async Task<(bool needsUpdate, string? latestVersion)> CheckForUpdates(
            string currentVersion, 
            string serverInfoUrl)
        {
            var latestVersion = await FetchLatestVersionId(serverInfoUrl);
            
            if (latestVersion == null)
            {
                Console.WriteLine("⚠ Could not determine latest version, using configured version");
                return (false, currentVersion);
            }

            if (latestVersion != currentVersion)
            {
                Console.WriteLine($"⚠ Version mismatch detected!");
                Console.WriteLine($"  Current: {currentVersion}");
                Console.WriteLine($"  Latest:  {latestVersion}");
                return (true, latestVersion);
            }

            Console.WriteLine($"✓ Version is up to date: {currentVersion}");
            return (false, currentVersion);
        }

        /// <summary>
        /// Updates the config file with the new version ID
        /// </summary>
        public static void UpdateConfigVersion(string newVersionId)
        {
            try
            {
                var configPath = "appsettings.json";
                if (!File.Exists(configPath))
                {
                    Console.WriteLine("✗ Config file not found, cannot auto-update version");
                    return;
                }

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Config>(json);
                
                if (config != null)
                {
                    var oldVersion = config.ServerConfiguration.VersionId;
                    config.ServerConfiguration.VersionId = newVersionId;
                    
                    var options = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    
                    File.WriteAllText(configPath, JsonSerializer.Serialize(config, options));
                    Console.WriteLine($"✓ Updated config: {oldVersion} → {newVersionId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to update config file: {ex.Message}");
            }
        }
    }
}
