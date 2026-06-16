using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientInfaceConfigPatchService : IHostedService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ILogger<ClientInfaceConfigPatchService> logger;
        private InfaceConfigPatchState patchState;
        private string configPath;

        public ClientInfaceConfigPatchService(ILogger<ClientInfaceConfigPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            configPath = GetConfigPath();

            if (!IsEnabled())
            {
                RestoreOrphanedConfig();
                logger.LogInformation("Client inface config patch disabled");
                return Task.CompletedTask;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    logger.LogWarning("Client inface config auto-patch is enabled, but no nxinface config path was configured. " +
                        "Set ClientInfaceConfigPath in server config or set SHITTIM_CLIENT_INFACE_CONFIG_PATH env var.");
                    return Task.CompletedTask;
                }

                logger.LogInformation("Client inface config path: {ConfigPath}", configPath);

                if (!File.Exists(configPath))
                {
                    logger.LogWarning("Client inface config file not found: {ConfigPath}", configPath);
                    return Task.CompletedTask;
                }

                var pluginDirectory = GetPluginDirectory(configPath);
                if (string.IsNullOrWhiteSpace(pluginDirectory))
                {
                    logger.LogWarning("Client inface config auto-patch is enabled, but no plugin directory was configured");
                    return Task.CompletedTask;
                }

                logger.LogInformation("Client inface config target plugin directory: {PluginDirectory}", pluginDirectory);

                if (!Directory.Exists(pluginDirectory))
                    logger.LogWarning("Client plugin directory does not exist yet: {PluginDirectory}", pluginDirectory);

                patchState = PatchConfig(configPath, pluginDirectory);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to patch client inface config");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                RestoreConfig();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore client inface config");
            }

            return Task.CompletedTask;
        }

        private InfaceConfigPatchState PatchConfig(string path, string pluginDirectory)
        {
            var targetDirectory = NormaliseDirectory(pluginDirectory);
            var statePath = GetStatePath(path);
            var existingState = LoadState(statePath);
            var currentRaw = File.ReadAllText(path);
            var currentJson = DecodeConfig(currentRaw);
            var currentDirs = GetNxiDirs(currentJson);

            var alreadyPatched = currentDirs.Count == 1 &&
                                 string.Equals(NormaliseDirectory(currentDirs[0]), targetDirectory, StringComparison.OrdinalIgnoreCase);

            if (alreadyPatched)
            {
                var state = existingState;
                if (state == null)
                {
                    logger.LogWarning("Client inface config is already patched to {PluginDirectory}, but no original state was found. It cannot be restored automatically on shutdown.", targetDirectory);
                    return null;
                }

                patchState = state;
                logger.LogInformation("Client inface config already patched NXI_DIRS: {PluginDirectory}", targetDirectory);
                return state;
            }

            var originalRaw = currentRaw;
            if (existingState != null)
            {
                if (string.Equals(currentRaw, existingState.PatchedRaw, StringComparison.Ordinal))
                    originalRaw = existingState.OriginalRaw;
                else if (string.Equals(currentRaw, existingState.OriginalRaw, StringComparison.Ordinal))
                    originalRaw = existingState.OriginalRaw;
            }

            currentJson["NXI_DIRS"] = new JsonArray(targetDirectory);
            var patchedRaw = EncodeConfig(currentJson);

            File.WriteAllText(path, patchedRaw, Encoding.UTF8);

            var stateToSave = new InfaceConfigPatchState
            {
                ConfigPath = path,
                OriginalRaw = originalRaw,
                PatchedRaw = patchedRaw,
                PluginDirectory = targetDirectory
            };

            SaveState(statePath, stateToSave);

            var from = currentDirs.Count == 0 ? "(missing)" : string.Join(", ", currentDirs);
            logger.LogInformation("Patched client inface config NXI_DIRS: {OriginalDirectories} -> {PluginDirectory}", from, targetDirectory);

            return stateToSave;
        }

        private void RestoreConfig()
        {
            var path = configPath ?? patchState?.ConfigPath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var statePath = GetStatePath(path);
            var state = patchState ?? LoadState(statePath);
            if (state == null)
                return;

            if (!File.Exists(path))
            {
                logger.LogWarning("Client inface config disappeared before restore: {ConfigPath}", path);
                return;
            }

            var currentRaw = File.ReadAllText(path);
            if (!string.Equals(currentRaw, state.PatchedRaw, StringComparison.Ordinal))
            {
                logger.LogWarning("Client inface config did not match the active patch state. Leaving it unchanged: {ConfigPath}", path);
                return;
            }

            File.WriteAllText(path, state.OriginalRaw, Encoding.UTF8);
            TryDelete(statePath);

            logger.LogInformation("Restored client inface config: {ConfigPath}", path);
        }

        private void RestoreOrphanedConfig()
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                return;

            var statePath = GetStatePath(configPath);
            var state = LoadState(statePath);
            if (state == null)
                return;

            var currentRaw = File.ReadAllText(configPath);
            if (!string.Equals(currentRaw, state.PatchedRaw, StringComparison.Ordinal))
                return;

            File.WriteAllText(configPath, state.OriginalRaw, Encoding.UTF8);
            TryDelete(statePath);

            logger.LogInformation("Restored orphaned client inface config: {ConfigPath}", configPath);
        }

        private static JsonObject DecodeConfig(string raw)
        {
            var base64 = raw.Replace("&", "").Trim();
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("nxinface config JSON was empty");
        }

        private static string EncodeConfig(JsonObject json)
        {
            var compactJson = json.ToJsonString();
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(compactJson));
            return string.Join("&", base64.ToCharArray());
        }

        private static List<string> GetNxiDirs(JsonObject json)
        {
            var result = new List<string>();

            if (json["NXI_DIRS"] is not JsonArray array)
                return result;

            foreach (var item in array)
            {
                var value = item?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add(value);
            }

            return result;
        }

        private static bool IsEnabled()
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_AUTO_PATCH_INFACE_CONFIG");
            if (!string.IsNullOrWhiteSpace(env) && bool.TryParse(env, out var enabled))
                return enabled;

            return Config.Instance.ServerConfiguration.AutoPatchClientInfaceConfig;
        }

        private static string GetConfigPath()
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_INFACE_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            var configuredPath = Config.Instance.ServerConfiguration.ClientInfaceConfigPath;
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return configuredPath;

            var candidates = new[]
            {
                @"F:\SteamLibrary\steamapps\common\BlueArchive\nxinface.enconfig.json",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "nxinface.enconfig.json")
            };

            return candidates.FirstOrDefault(File.Exists) ?? "";
        }

        private static string GetPluginDirectory(string path)
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_PLUGIN_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            var configuredPath = Config.Instance.ServerConfiguration.ClientPluginDirectory;
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return configuredPath;

            var infacePath = Config.Instance.ServerConfiguration.ClientInfacePath;
            if (!string.IsNullOrWhiteSpace(infacePath))
                return Path.GetDirectoryName(infacePath) ?? "";

            var gameRoot = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(gameRoot))
                return "";

            return Path.Combine(gameRoot, "BlueArchive_Data", "Plugins", "x86_64");
        }

        private static string NormaliseDirectory(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
                fullPath += Path.DirectorySeparatorChar;

            return fullPath;
        }

        private static string GetStatePath(string path)
            => path + ".shittim_inface_config_patch.json";

        private static InfaceConfigPatchState LoadState(string statePath)
        {
            if (!File.Exists(statePath))
                return null;

            try
            {
                return JsonSerializer.Deserialize<InfaceConfigPatchState>(File.ReadAllText(statePath));
            }
            catch
            {
                return null;
            }
        }

        private static void SaveState(string statePath, InfaceConfigPatchState state)
        {
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private sealed class InfaceConfigPatchState
        {
            public string ConfigPath { get; set; } = "";
            public string OriginalRaw { get; set; } = "";
            public string PatchedRaw { get; set; } = "";
            public string PluginDirectory { get; set; } = "";
        }
    }
}
