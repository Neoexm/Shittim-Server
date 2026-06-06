using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientNexonPlatformPatchService : IHostedService
    {
        private static readonly BinaryPatchDefinition[] PatchDefinitions =
        [
            new(
                "ias-full-live-url",
                Encoding.ASCII.GetBytes("https://public.api.nexon.com/ias/live/public"),
                Encoding.ASCII.GetBytes("http://127.0.0.1:5000/ias/live/public/xxxxxx")
            ),
            new(
                "ias-live-host",
                Encoding.ASCII.GetBytes("public.api.nexon.com/ias/live/public"),
                Encoding.ASCII.GetBytes("127.0.0.1:5000/ias/live/public/xxxxx")
            ),
            new(
                "ias-http-scheme-builder",
                Convert.FromHexString("48C745F70800000048B868747470733A2F2F488945E7C645EF00"),
                Convert.FromHexString("48C745F70700000048B8687474703A2F2F00488945E7C645EF00")
            )
        ];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ILogger<ClientNexonPlatformPatchService> logger;
        private NexonPlatformPatchState patchState;
        private string modulePath;

        public ClientNexonPlatformPatchService(ILogger<ClientNexonPlatformPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var config = Config.Instance.ServerConfiguration;

            if (!config.AutoPatchClientNexonPlatform)
                return Task.CompletedTask;

            try
            {
                modulePath = GetModulePath();
                if (string.IsNullOrWhiteSpace(modulePath))
                {
                    logger.LogWarning("Client NexonPlatformModules auto-patch is enabled, but no DLL path was configured");
                    return Task.CompletedTask;
                }

                if (!File.Exists(modulePath))
                {
                    logger.LogWarning("Client NexonPlatformModules file not found: {ModulePath}", modulePath);
                    return Task.CompletedTask;
                }

                patchState = PatchModule(modulePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to patch client NexonPlatformModules DLL");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                RestoreModule();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore client NexonPlatformModules DLL");
            }

            return Task.CompletedTask;
        }

        private NexonPlatformPatchState PatchModule(string path)
        {
            var statePath = GetStatePath(path);
            var existingState = LoadState(statePath);
            var bytes = File.ReadAllBytes(path);
            var patches = new List<NexonPlatformAppliedPatch>();
            var changed = false;

            foreach (var definition in PatchDefinitions)
            {
                if (definition.Original.Length != definition.Patched.Length)
                    throw new InvalidOperationException($"Patch {definition.Name} has mismatched byte lengths");

                var offset = IndexOf(bytes, definition.Original);
                if (offset >= 0)
                {
                    patches.Add(new NexonPlatformAppliedPatch
                    {
                        Name = definition.Name,
                        Offset = offset,
                        Original = Convert.ToBase64String(definition.Original),
                        Patched = Convert.ToBase64String(definition.Patched)
                    });

                    Array.Copy(definition.Patched, 0, bytes, offset, definition.Patched.Length);
                    changed = true;
                    continue;
                }

                offset = IndexOf(bytes, definition.Patched);
                if (offset >= 0)
                {
                    patches.Add(new NexonPlatformAppliedPatch
                    {
                        Name = definition.Name,
                        Offset = offset,
                        Original = Convert.ToBase64String(definition.Original),
                        Patched = Convert.ToBase64String(definition.Patched)
                    });
                    continue;
                }

                var existingPatch = existingState?.Patches.FirstOrDefault(x => x.Name == definition.Name);
                if (existingPatch != null)
                {
                    patches.Add(existingPatch);
                    continue;
                }

                logger.LogWarning("Could not find NexonPlatformModules patch target {PatchName}: {ModulePath}", definition.Name, path);
            }

            if (patches.Count == 0)
                return null;

            var state = new NexonPlatformPatchState
            {
                ModulePath = path,
                Patches = patches
            };

            if (changed)
            {
                File.WriteAllBytes(path, bytes);
                logger.LogInformation("Patched client NexonPlatformModules DLL: {ModulePath}", path);
            }
            else
            {
                logger.LogInformation("Client NexonPlatformModules DLL already patched: {ModulePath}", path);
            }

            SaveState(statePath, state);
            return state;
        }

        private void RestoreModule()
        {
            var path = modulePath ?? patchState?.ModulePath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var statePath = GetStatePath(path);
            var state = patchState ?? LoadState(statePath);
            if (state == null)
                return;

            if (!File.Exists(path))
            {
                logger.LogWarning("Client NexonPlatformModules DLL disappeared before restore: {ModulePath}", path);
                return;
            }

            var bytes = File.ReadAllBytes(path);

            foreach (var patch in state.Patches)
            {
                var original = Convert.FromBase64String(patch.Original);
                var patched = Convert.FromBase64String(patch.Patched);

                if (patch.Offset < 0 || patch.Offset + patched.Length > bytes.Length)
                {
                    logger.LogWarning("Client NexonPlatformModules patch offset is invalid. Leaving it unchanged: {ModulePath}", path);
                    return;
                }

                if (!bytes.AsSpan((int)patch.Offset, patched.Length).SequenceEqual(patched))
                {
                    logger.LogWarning("Client NexonPlatformModules DLL did not match the active patch state. Leaving it unchanged: {ModulePath}", path);
                    return;
                }

                Array.Copy(original, 0, bytes, patch.Offset, original.Length);
            }

            File.WriteAllBytes(path, bytes);

            if (File.Exists(statePath))
                File.Delete(statePath);

            logger.LogInformation("Restored client NexonPlatformModules DLL: {ModulePath}", path);
        }

        private static int IndexOf(byte[] bytes, byte[] pattern)
        {
            return bytes.AsSpan().IndexOf(pattern);
        }

        private static string GetStatePath(string path)
        {
            return $"{path}.shittim_patch.json";
        }

        private static NexonPlatformPatchState LoadState(string statePath)
        {
            if (!File.Exists(statePath))
                return null;

            return JsonSerializer.Deserialize<NexonPlatformPatchState>(File.ReadAllText(statePath));
        }

        private static void SaveState(string statePath, NexonPlatformPatchState state)
        {
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        private static string GetModulePath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_NEXON_PLATFORM_PATH");
            if (string.IsNullOrWhiteSpace(configuredPath))
                configuredPath = Config.Instance.ServerConfiguration.ClientNexonPlatformPath;

            if (!string.IsNullOrWhiteSpace(configuredPath))
                return ResolvePath(configuredPath);

            var candidates = new[]
            {
                @"F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive_Data\Plugins\x86_64\NexonPlatformModules.dll",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "BlueArchive_Data", "Plugins", "x86_64", "NexonPlatformModules.dll")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            return File.Exists(basePath) ? basePath : Path.GetFullPath(path);
        }

        private sealed record BinaryPatchDefinition(string Name, byte[] Original, byte[] Patched);

        private sealed class NexonPlatformPatchState
        {
            public string ModulePath { get; set; } = "";
            public List<NexonPlatformAppliedPatch> Patches { get; set; } = [];
        }

        private sealed class NexonPlatformAppliedPatch
        {
            public string Name { get; set; } = "";
            public int Offset { get; set; }
            public string Original { get; set; } = "";
            public string Patched { get; set; } = "";
        }
    }
}
