using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientMetadataPatchService : IHostedService
    {
        private const int ChunkLength = 150;

        private static readonly MetadataChunk[] MetadataChunks =
        [
            new(0x145D428, 0),
            new(0xFA6650, 1),
            new(0x145D368, 2)
        ];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ILogger<ClientMetadataPatchService> logger;
        private MetadataPatchState patchState;
        private string metadataPath;

        public ClientMetadataPatchService(ILogger<ClientMetadataPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var config = Config.Instance.ServerConfiguration;

            if (!config.EnableGateway || !config.AutoPatchClientMetadata)
                return Task.CompletedTask;

            try
            {
                metadataPath = GetMetadataPath();
                if (string.IsNullOrWhiteSpace(metadataPath))
                {
                    logger.LogWarning("Client metadata auto-patch is enabled, but no metadata path was configured");
                    return Task.CompletedTask;
                }

                if (!File.Exists(metadataPath))
                {
                    logger.LogWarning("Client metadata file not found: {MetadataPath}", metadataPath);
                    return Task.CompletedTask;
                }

                var publicKey = GetGatewayPublicKey();
                if (string.IsNullOrWhiteSpace(publicKey))
                {
                    logger.LogWarning("Client metadata auto-patch is enabled, but no gateway public key was found");
                    return Task.CompletedTask;
                }

                var targetChunks = BuildTargetChunks(publicKey);
                patchState = PatchMetadata(metadataPath, targetChunks);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to patch client metadata");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                RestoreMetadata();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore client metadata");
            }

            return Task.CompletedTask;
        }

        private MetadataPatchState PatchMetadata(string path, byte[][] targetChunks)
        {
            var statePath = GetStatePath(path);
            var existingState = LoadState(statePath);

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var currentChunks = ReadChunks(stream);

            if (ChunksEqual(currentChunks, targetChunks))
            {
                var state = existingState ?? CreateStateFromBackup(path, targetChunks);
                if (state == null)
                {
                    logger.LogWarning("Client metadata is already patched, but no original metadata state was found. It cannot be restored automatically on shutdown.");
                    return null;
                }

                SaveState(statePath, state);
                logger.LogInformation("Client metadata already patched: {MetadataPath}", path);
                return state;
            }

            var originalChunks = currentChunks;
            if (existingState != null)
            {
                var previousPatchedChunks = GetPatchedChunks(existingState);
                var previousOriginalChunks = GetOriginalChunks(existingState);

                if (ChunksEqual(currentChunks, previousPatchedChunks) || ChunksEqual(currentChunks, previousOriginalChunks))
                    originalChunks = previousOriginalChunks;
            }

            var patchState = CreateState(path, originalChunks, targetChunks);

            WriteChunks(stream, targetChunks);
            SaveState(statePath, patchState);

            logger.LogInformation("Patched client metadata: {MetadataPath}", path);
            return patchState;
        }

        private void RestoreMetadata()
        {
            var path = metadataPath ?? patchState?.MetadataPath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var statePath = GetStatePath(path);
            var state = patchState ?? LoadState(statePath);
            if (state == null)
                return;

            if (!File.Exists(path))
            {
                logger.LogWarning("Client metadata file disappeared before restore: {MetadataPath}", path);
                return;
            }

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var currentChunks = ReadChunks(stream);
            var patchedChunks = GetPatchedChunks(state);

            if (!ChunksEqual(currentChunks, patchedChunks))
            {
                logger.LogWarning("Client metadata did not match the active patch state. Leaving it unchanged: {MetadataPath}", path);
                return;
            }

            WriteChunks(stream, GetOriginalChunks(state));

            if (File.Exists(statePath))
                File.Delete(statePath);

            logger.LogInformation("Restored client metadata: {MetadataPath}", path);
        }

        private static byte[][] BuildTargetChunks(string publicKey)
        {
            var normalized = publicKey
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .TrimEnd('\n');

            var publicKeyBytes = Encoding.ASCII.GetBytes(normalized);
            if (publicKeyBytes.Length != ChunkLength * MetadataChunks.Length)
                throw new InvalidOperationException($"Gateway public key must be {ChunkLength * MetadataChunks.Length} ASCII bytes after newline normalization, got {publicKeyBytes.Length}");

            return MetadataChunks
                .Select(chunk => publicKeyBytes.Skip(chunk.PublicKeyChunkIndex * ChunkLength).Take(ChunkLength).ToArray())
                .ToArray();
        }

        private static byte[][] ReadChunks(FileStream stream)
        {
            var chunks = new byte[MetadataChunks.Length][];

            for (var i = 0; i < MetadataChunks.Length; i++)
            {
                chunks[i] = new byte[ChunkLength];
                stream.Position = MetadataChunks[i].Offset;

                var read = stream.Read(chunks[i], 0, ChunkLength);
                if (read != ChunkLength)
                    throw new IOException($"Could not read metadata patch chunk at 0x{MetadataChunks[i].Offset:X}");
            }

            return chunks;
        }

        private static void WriteChunks(FileStream stream, byte[][] chunks)
        {
            if (chunks.Length != MetadataChunks.Length)
                throw new InvalidOperationException("Invalid metadata patch chunk count");

            for (var i = 0; i < MetadataChunks.Length; i++)
            {
                if (chunks[i].Length != ChunkLength)
                    throw new InvalidOperationException("Invalid metadata patch chunk length");

                stream.Position = MetadataChunks[i].Offset;
                stream.Write(chunks[i], 0, chunks[i].Length);
            }

            stream.Flush(true);
        }

        private static MetadataPatchState CreateStateFromBackup(string path, byte[][] targetChunks)
        {
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            var backups = Directory
                .EnumerateFiles(directory, $"{fileName}.bak*")
                .OrderByDescending(File.GetLastWriteTimeUtc);

            foreach (var backup in backups)
            {
                using var stream = File.OpenRead(backup);
                var originalChunks = ReadChunks(stream);

                if (!ChunksEqual(originalChunks, targetChunks))
                    return CreateState(path, originalChunks, targetChunks);
            }

            return null;
        }

        private static MetadataPatchState CreateState(string path, byte[][] originalChunks, byte[][] patchedChunks)
        {
            return new MetadataPatchState
            {
                MetadataPath = path,
                Chunks = MetadataChunks
                    .Select((chunk, index) => new MetadataPatchChunk
                    {
                        Offset = chunk.Offset,
                        Original = Convert.ToBase64String(originalChunks[index]),
                        Patched = Convert.ToBase64String(patchedChunks[index])
                    })
                    .ToList()
            };
        }

        private static byte[][] GetOriginalChunks(MetadataPatchState state)
        {
            return GetStateChunks(state, chunk => chunk.Original);
        }

        private static byte[][] GetPatchedChunks(MetadataPatchState state)
        {
            return GetStateChunks(state, chunk => chunk.Patched);
        }

        private static byte[][] GetStateChunks(MetadataPatchState state, Func<MetadataPatchChunk, string> selector)
        {
            return MetadataChunks
                .Select(chunk =>
                {
                    var stateChunk = state.Chunks.First(x => x.Offset == chunk.Offset);
                    return Convert.FromBase64String(selector(stateChunk));
                })
                .ToArray();
        }

        private static bool ChunksEqual(byte[][] left, byte[][] right)
        {
            return left.Length == right.Length && left.Zip(right).All(pair => pair.First.SequenceEqual(pair.Second));
        }

        private static string GetStatePath(string path)
        {
            return $"{path}.shittim_patch.json";
        }

        private static MetadataPatchState LoadState(string statePath)
        {
            if (!File.Exists(statePath))
                return null;

            return JsonSerializer.Deserialize<MetadataPatchState>(File.ReadAllText(statePath));
        }

        private static void SaveState(string statePath, MetadataPatchState state)
        {
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        private static string GetMetadataPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_METADATA_PATH");
            if (string.IsNullOrWhiteSpace(configuredPath))
                configuredPath = Config.Instance.ServerConfiguration.ClientMetadataPath;

            if (!string.IsNullOrWhiteSpace(configuredPath))
                return ResolvePath(configuredPath);

            var candidates = new[]
            {
                @"F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive_Data\il2cpp_data\Metadata\global-metadata.dat",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "BlueArchive_Data", "il2cpp_data", "Metadata", "global-metadata.dat")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string GetGatewayPublicKey()
        {
            var publicKey = Environment.GetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PUBLIC_KEY");
            if (!string.IsNullOrWhiteSpace(publicKey))
                return publicKey;

            if (!string.IsNullOrWhiteSpace(Config.Instance.ServerConfiguration.GatewayRsaPublicKeyPem))
                return Config.Instance.ServerConfiguration.GatewayRsaPublicKeyPem;

            var publicKeyPath = Environment.GetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PUBLIC_KEY_PATH");
            if (string.IsNullOrWhiteSpace(publicKeyPath))
                publicKeyPath = Config.Instance.ServerConfiguration.GatewayRsaPublicKeyPath;

            var publicKeyCandidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(publicKeyPath))
                publicKeyCandidates.Add(ResolvePath(publicKeyPath));

            var privateKeyPath = Config.Instance.ServerConfiguration.GatewayRsaPrivateKeyPath;
            if (!string.IsNullOrWhiteSpace(privateKeyPath))
            {
                var privateKeyDirectory = Path.GetDirectoryName(ResolvePath(privateKeyPath));
                if (!string.IsNullOrWhiteSpace(privateKeyDirectory))
                    publicKeyCandidates.Add(Path.Combine(privateKeyDirectory, "GatewayPublicKey.pem"));
            }

            publicKeyCandidates.Add(Path.Combine(Config.ConfigDirectory, "GatewayPublicKey.pem"));

            foreach (var candidate in publicKeyCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
            }

            var privateKey = GetGatewayPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
                return "";

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
            return rsa.ExportSubjectPublicKeyInfoPem();
        }

        private static string GetGatewayPrivateKey()
        {
            var privateKey = Environment.GetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PRIVATE_KEY");
            if (!string.IsNullOrWhiteSpace(privateKey))
                return privateKey;

            var privateKeyPath = Environment.GetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PRIVATE_KEY_PATH");
            if (string.IsNullOrWhiteSpace(privateKeyPath))
                privateKeyPath = Config.Instance.ServerConfiguration.GatewayRsaPrivateKeyPath;

            if (!string.IsNullOrWhiteSpace(privateKeyPath))
            {
                privateKeyPath = ResolvePath(privateKeyPath);
                if (File.Exists(privateKeyPath))
                    return File.ReadAllText(privateKeyPath);
            }

            var defaultPrivateKeyPath = Path.Combine(Config.ConfigDirectory, "GatewayPrivateKey.pem");
            if (File.Exists(defaultPrivateKeyPath))
                return File.ReadAllText(defaultPrivateKeyPath);

            return Config.Instance.ServerConfiguration.GatewayRsaPrivateKeyPem;
        }

        private static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            return File.Exists(basePath) ? basePath : Path.GetFullPath(path);
        }

        private sealed record MetadataChunk(long Offset, int PublicKeyChunkIndex);

        private sealed class MetadataPatchState
        {
            public string MetadataPath { get; set; } = "";
            public List<MetadataPatchChunk> Chunks { get; set; } = [];
        }

        private sealed class MetadataPatchChunk
        {
            public long Offset { get; set; }
            public string Original { get; set; } = "";
            public string Patched { get; set; } = "";
        }
    }
}
