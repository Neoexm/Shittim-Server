using System.Text.Json;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientGameAssemblyIasPatchService : IHostedService
    {
        private static readonly BinaryPatchDefinition[] PatchDefinitions =
        [
            new(
                "GameAuth.FetchPrimaryLinkResult.HasPrimaryLink.alwaysTrue",
                Hex("55 56 57 48 83 EC 70 48 8D 6C 24 70 48 C7 45 F0 FE FF FF FF 48 89 CE 80 3D 4E 64 4D 03 00 75 43"),
                0,
                Hex("55 56 57 48 83"),
                Hex("B0 01 C3 90 90"))
        ];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ILogger<ClientGameAssemblyIasPatchService> logger;
        private GameAssemblyPatchState patchState;
        private string gameAssemblyPath;

        public ClientGameAssemblyIasPatchService(ILogger<ClientGameAssemblyIasPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                gameAssemblyPath = GetGameAssemblyPath();
                if (!IsEnabled())
                {
                    RestoreGameAssembly();
                    RestoreOrphanedGameAssemblyPatch();
                    logger.LogInformation("GameAssembly IAS auth patch disabled");
                    return Task.CompletedTask;
                }

                if (string.IsNullOrWhiteSpace(gameAssemblyPath))
                {
                    logger.LogWarning("GameAssembly IAS auto-patch is enabled, but no GameAssembly.dll path was configured");
                    return Task.CompletedTask;
                }

                if (!File.Exists(gameAssemblyPath))
                {
                    logger.LogWarning("GameAssembly.dll not found: {GameAssemblyPath}", gameAssemblyPath);
                    return Task.CompletedTask;
                }

                patchState = PatchGameAssembly(gameAssemblyPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to patch GameAssembly IAS auth handling");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                RestoreGameAssembly();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore GameAssembly IAS auth handling");
            }

            return Task.CompletedTask;
        }

        private GameAssemblyPatchState PatchGameAssembly(string path)
        {
            var statePath = GetStatePath(path);
            var existingState = LoadState(statePath);
            var data = File.ReadAllBytes(path);

            var activePatches = new List<GameAssemblyPatchEntry>();
            var writePlans = new List<GameAssemblyPatchEntry>();

            foreach (var definition in PatchDefinitions)
            {
                var entry = ResolveExistingPatch(data, existingState, definition);
                if (entry == null)
                    entry = ResolvePatchBySignature(data, definition);

                if (entry == null)
                    continue;

                activePatches.Add(entry);

                var current = Slice(data, entry.Offset, definition.Original.Length);
                if (current.SequenceEqual(Convert.FromBase64String(entry.Patched)))
                    continue;

                writePlans.Add(entry);
            }

            if (writePlans.Count == 0)
            {
                if (activePatches.Count > 0)
                {
                    var state = CreateState(path, activePatches);
                    SaveState(statePath, state);
                    logger.LogInformation("GameAssembly IAS auth handling already patched: {GameAssemblyPath}", path);
                    return state;
                }

                logger.LogWarning("No GameAssembly IAS auth patch signatures matched: {GameAssemblyPath}", path);
                return null;
            }

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            foreach (var plan in writePlans)
            {
                var patched = Convert.FromBase64String(plan.Patched);
                stream.Position = plan.Offset;
                stream.Write(patched, 0, patched.Length);
            }

            stream.Flush(true);

            var patchState = CreateState(path, activePatches);
            SaveState(statePath, patchState);

            logger.LogInformation("Patched GameAssembly IAS auth handling: {GameAssemblyPath}", path);
            return patchState;
        }

        private GameAssemblyPatchEntry ResolveExistingPatch(byte[] data, GameAssemblyPatchState state, BinaryPatchDefinition definition)
        {
            var entry = state?.Patches.FirstOrDefault(x => x.Name == definition.Name);
            if (entry == null || entry.Offset < 0 || entry.Offset + definition.Original.Length > data.LongLength)
                return null;

            var current = Slice(data, entry.Offset, definition.Original.Length);
            var original = Convert.FromBase64String(entry.Original);
            var patched = Convert.FromBase64String(entry.Patched);

            if (current.SequenceEqual(original) || current.SequenceEqual(patched))
                return entry;

            logger.LogWarning("Saved GameAssembly IAS patch state no longer matches {PatchName}; searching signature again", definition.Name);
            return null;
        }

        private GameAssemblyPatchEntry ResolvePatchBySignature(byte[] data, BinaryPatchDefinition definition)
        {
            var matches = FindAll(data, definition.Signature);
            if (matches.Count != 1)
            {
                logger.LogWarning("GameAssembly IAS patch signature {PatchName} matched {MatchCount} locations", definition.Name, matches.Count);
                return null;
            }

            var offset = matches[0] + definition.PatchOffset;
            var current = Slice(data, offset, definition.Original.Length);

            if (!current.SequenceEqual(definition.Original) && !current.SequenceEqual(definition.Patched))
            {
                logger.LogWarning("GameAssembly IAS patch target bytes did not match {PatchName}", definition.Name);
                return null;
            }

            return new GameAssemblyPatchEntry
            {
                Name = definition.Name,
                Offset = offset,
                Original = Convert.ToBase64String(definition.Original),
                Patched = Convert.ToBase64String(definition.Patched)
            };
        }

        private void RestoreGameAssembly()
        {
            var path = gameAssemblyPath ?? patchState?.GameAssemblyPath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var statePath = GetStatePath(path);
            var state = patchState ?? LoadState(statePath);
            if (state == null)
                return;

            if (!File.Exists(path))
            {
                logger.LogWarning("GameAssembly.dll disappeared before restore: {GameAssemblyPath}", path);
                return;
            }

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var restored = true;

            foreach (var entry in state.Patches)
            {
                var original = Convert.FromBase64String(entry.Original);
                var patched = Convert.FromBase64String(entry.Patched);

                if (entry.Offset < 0 || entry.Offset + patched.Length > stream.Length)
                {
                    restored = false;
                    logger.LogWarning("GameAssembly IAS patch state has an invalid offset for {PatchName}", entry.Name);
                    continue;
                }

                stream.Position = entry.Offset;
                var current = new byte[patched.Length];
                var read = stream.Read(current, 0, current.Length);

                if (read != current.Length)
                {
                    restored = false;
                    logger.LogWarning("Could not read GameAssembly IAS patch bytes for {PatchName}", entry.Name);
                    continue;
                }

                if (current.SequenceEqual(original))
                    continue;

                if (!current.SequenceEqual(patched))
                {
                    restored = false;
                    logger.LogWarning("GameAssembly IAS patch bytes no longer match active state for {PatchName}; leaving unchanged", entry.Name);
                    continue;
                }

                stream.Position = entry.Offset;
                stream.Write(original, 0, original.Length);
            }

            stream.Flush(true);

            if (!restored)
                return;

            if (File.Exists(statePath))
                File.Delete(statePath);

            logger.LogInformation("Restored GameAssembly IAS auth handling: {GameAssemblyPath}", path);
        }

        private void RestoreOrphanedGameAssemblyPatch()
        {
            var path = gameAssemblyPath ?? patchState?.GameAssemblyPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var data = File.ReadAllBytes(path);
            var patches = new List<GameAssemblyPatchEntry>();

            foreach (var definition in PatchDefinitions)
            {
                var patchedSignature = BuildPatchedSignature(definition);
                var matches = FindAll(data, patchedSignature);
                if (matches.Count == 0)
                    continue;

                if (matches.Count != 1)
                {
                    logger.LogWarning("Orphaned GameAssembly IAS patch signature {PatchName} matched {MatchCount} locations", definition.Name, matches.Count);
                    continue;
                }

                patches.Add(new GameAssemblyPatchEntry
                {
                    Name = definition.Name,
                    Offset = matches[0] + definition.PatchOffset,
                    Original = Convert.ToBase64String(definition.Original),
                    Patched = Convert.ToBase64String(definition.Patched)
                });
            }

            if (patches.Count == 0)
                return;

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            foreach (var patch in patches)
            {
                var original = Convert.FromBase64String(patch.Original);
                stream.Position = patch.Offset;
                stream.Write(original, 0, original.Length);
            }

            stream.Flush(true);
            logger.LogInformation("Restored orphaned GameAssembly IAS auth handling: {GameAssemblyPath}", path);
        }

        private static GameAssemblyPatchState CreateState(string path, List<GameAssemblyPatchEntry> patches)
        {
            return new GameAssemblyPatchState
            {
                GameAssemblyPath = path,
                Patches = patches
            };
        }

        private static List<long> FindAll(byte[] data, byte[] pattern)
        {
            var matches = new List<long>();
            var offset = 0;

            while (offset <= data.Length - pattern.Length)
            {
                var index = data.AsSpan(offset).IndexOf(pattern);
                if (index < 0)
                    break;

                matches.Add(offset + index);
                offset += index + 1;
            }

            return matches;
        }

        private static byte[] Slice(byte[] data, long offset, int length)
        {
            var buffer = new byte[length];
            Array.Copy(data, offset, buffer, 0, length);
            return buffer;
        }

        private static byte[] BuildPatchedSignature(BinaryPatchDefinition definition)
        {
            var signature = definition.Signature.ToArray();
            Array.Copy(definition.Patched, 0, signature, definition.PatchOffset, definition.Patched.Length);
            return signature;
        }

        private static bool IsEnabled()
        {
            var value = Environment.GetEnvironmentVariable("SHITTIM_AUTO_PATCH_GAMEASSEMBLY_IAS");
            return bool.TryParse(value, out var enabled)
                ? enabled
                : Config.Instance.ServerConfiguration.AutoPatchClientGameAssemblyIas;
        }

        private static string GetGameAssemblyPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_GAMEASSEMBLY_PATH");
            if (string.IsNullOrWhiteSpace(configuredPath))
                configuredPath = Config.Instance.ServerConfiguration.ClientGameAssemblyPath;

            if (!string.IsNullOrWhiteSpace(configuredPath))
                return ResolvePath(configuredPath);

            // Locate the game across any Steam library on the machine, not just F:\
            // or Program Files.
            var located = SteamGameLocator.FindGameFile("GameAssembly.dll");
            if (!string.IsNullOrWhiteSpace(located))
                return located;

            var candidates = new[]
            {
                @"F:\SteamLibrary\steamapps\common\BlueArchive\GameAssembly.dll",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "GameAssembly.dll")
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

        private static string GetStatePath(string path)
        {
            return $"{path}.shittim_ias_patch.json";
        }

        private static GameAssemblyPatchState LoadState(string statePath)
        {
            if (!File.Exists(statePath))
                return null;

            return JsonSerializer.Deserialize<GameAssemblyPatchState>(File.ReadAllText(statePath));
        }

        private static void SaveState(string statePath, GameAssemblyPatchState state)
        {
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        private static byte[] Hex(string hex)
        {
            return hex
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Convert.ToByte(x, 16))
                .ToArray();
        }

        private sealed record BinaryPatchDefinition(string Name, byte[] Signature, int PatchOffset, byte[] Original, byte[] Patched);

        private sealed class GameAssemblyPatchState
        {
            public string GameAssemblyPath { get; set; } = "";
            public List<GameAssemblyPatchEntry> Patches { get; set; } = [];
        }

        private sealed class GameAssemblyPatchEntry
        {
            public string Name { get; set; } = "";
            public long Offset { get; set; }
            public string Original { get; set; } = "";
            public string Patched { get; set; } = "";
        }
    }
}
