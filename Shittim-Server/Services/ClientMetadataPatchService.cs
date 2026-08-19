using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientMetadataPatchService : IHostedService
    {
        // The key is three separate string literals in the metadata rather than one run, and the split lands wherever the build's literal packing put it: 272/272/255 for the RSA-4096 key, and 150/150/150 for the RSA-2048 one it replaced.
        private static readonly int[] ChunkLengths = [272, 272, 255];
        private const int ChunkCount = 3;

        // The official Nexon gateway RSA-4096 public key, exactly as it is embedded inside the client's global-metadata.dat.
        // The client RSA-encrypts the 50001 gateway handshake with this key, so swapping in the private server's own key is what lets the gateway decode the handshake. Ours has to be 4096-bit as well: the chunks are fixed-size holes in fieldAndParameterDefaultValueData, and a PEM of any other length cannot go into them without shifting the section and every offset past it.
        //
        // The three byte runs are located by content (an AOB scan) rather than by fixed file offsets: the offsets move every time the client is rebuilt, and build 439170 shifted all three by +0x49B0.
        // Writing at a stale offset corrupts whatever IL2CPP metadata now lives there and hangs the client at "Unpacking game resources". A scan also fails safe: a chunk that cannot be found means no write at all.
        private const string OfficialGatewayPublicKeyPem =
            "-----BEGIN PUBLIC KEY-----\n" +
            "MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAz1oODtVINPQsTIbFDiL2\n" +
            "IIj3YqDGxZ1WqJWfJrpo7ZqdnSIMXaVqVyM07Zguc9wczPrluDNowheP8NXe8uTR\n" +
            "prO7jpF9bOcrrit9Xyxf57NA5rIMqOj6/XgWzzptvvt3oUrOQJRTvEjusQCX79Ds\n" +
            "fhhbPRfdPzZUOPYa49MM385Z05ndNiTB9/yhxP5+5zm7TS3f6/o1Ntw9yXoRYmgR\n" +
            "8+JYGL6TpqtZHkqPTp6kIJrnsLyT1wRteh3oQKJaze0CM0DUUzFiqieRE8c8JAoV\n" +
            "jk/yRQUqVMoKbdpINYlWhF+GFE/ccSpqQymepHxfR0alspynqJwJgODCeHtwuno5\n" +
            "HEOwdbA8NanLVnE9qlvdeNkbODdt/XTHKOaXx+SUMHezUY0rEXp8WMim0i9IQTGa\n" +
            "YWeW25RjXIZswKdg0gLRJXYZXUgF3nVcqr0hi4kv1pgrFPsjI7yIThxLY0UfomkR\n" +
            "oe6sSBBtQUDI85s+1pZRcl8xygxOxMfFSk5RB3EntFmcf/AKaLYQbXN9RxgrvqRS\n" +
            "9koBBEnnrIN0zXDBioZUDOWVBeBZwAydpPGKu2mkqu3BI/Al92noEHc6fmkG5+Qm\n" +
            "b237xSh1DFbbQO6lxg4ABdsqgvZskYS+7BcQPjs1z2zftrnesFkqD7BhbqXUBuDY\n" +
            "D80B8Be5I483qSSy8DsW458CAwEAAQ==\n" +
            "-----END PUBLIC KEY-----";

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
            try
            {
                metadataPath = GetMetadataPath();

                if (!IsEnabled())
                {
                    // Turning the patch off is how someone goes back to the official client, so it has to mean putting the key back rather than merely not patching again. A global-metadata.dat left carrying our gateway key hangs the client on "Unpacking game resources" with nothing on screen to say why, and the usual guess is to verify the files through Steam.
                    RestoreMetadata();
                    logger.LogInformation("Client metadata auto-patch disabled");
                    return Task.CompletedTask;
                }

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

                var officialChunks = BuildKeyChunks(OfficialGatewayPublicKeyPem, "official gateway public key");
                var targetChunks = BuildKeyChunks(publicKey, "gateway public key");
                patchState = PatchMetadata(metadataPath, officialChunks, targetChunks);
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

        private MetadataPatchState PatchMetadata(string path, byte[][] officialChunks, byte[][] targetChunks)
        {
            var statePath = GetStatePath(path);
            var fileBytes = File.ReadAllBytes(path);

            // Resolve where each key chunk currently lives by scanning for its bytes: a chunk is either still the official key (needs patching) or already ours (patched on an earlier run).
            // If neither turns up, abort without writing rather than corrupt the file.
            var offsets = new long[ChunkCount];
            var alreadyPatched = true;
            for (var i = 0; i < ChunkCount; i++)
            {
                if (TryLocateUnique(fileBytes, officialChunks[i], out var officialOffset))
                {
                    offsets[i] = officialOffset;
                    alreadyPatched = false;
                }
                else if (TryLocateUnique(fileBytes, targetChunks[i], out var patchedOffset))
                {
                    offsets[i] = patchedOffset;
                }
                else
                {
                    logger.LogWarning(
                        "Gateway public key chunk {Index} was not found in client metadata. The client build " +
                        "may have changed how the key is stored; leaving {MetadataPath} unchanged to avoid " +
                        "corrupting it.", i, path);
                    return null;
                }
            }

            if (alreadyPatched)
            {
                // The official key is a known constant, so the original bytes stay recoverable regardless of any stale sidecar left by an older client build.
                var state = CreateState(offsets, officialChunks, targetChunks);
                SaveState(statePath, state);
                logger.LogInformation("Client metadata already patched: {MetadataPath}", path);
                return state;
            }

            using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                WriteChunks(stream, offsets, targetChunks);
            }

            var patchState = CreateState(offsets, officialChunks, targetChunks);
            SaveState(statePath, patchState);

            logger.LogInformation("Patched client metadata gateway key at 0x{O0:X}, 0x{O1:X}, 0x{O2:X}: {MetadataPath}",
                offsets[0], offsets[1], offsets[2], path);
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

            var offsets = state.Chunks.Select(c => c.Offset).ToArray();
            var patchedChunks = state.Chunks.Select(c => Convert.FromBase64String(c.Patched)).ToArray();
            var originalChunks = state.Chunks.Select(c => Convert.FromBase64String(c.Original)).ToArray();

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var currentChunks = ReadChunks(stream, offsets);

            if (!ChunksEqual(currentChunks, patchedChunks))
            {
                logger.LogWarning("Client metadata did not match the active patch state. Leaving it unchanged: {MetadataPath}", path);
                return;
            }

            WriteChunks(stream, offsets, originalChunks);

            if (File.Exists(statePath))
                File.Delete(statePath);

            logger.LogInformation("Restored client metadata: {MetadataPath}", path);
        }

        // Splits a PEM public key at the lengths the client stores its own in. Newlines are normalized to '\n' and any trailing newline trimmed so the byte layout is identical to the metadata's embedded copy.
        private static byte[][] BuildKeyChunks(string publicKey, string label)
        {
            var normalized = publicKey
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .TrimEnd('\n');

            var publicKeyBytes = Encoding.ASCII.GetBytes(normalized);
            if (publicKeyBytes.Length != ChunkLengths.Sum())
                throw new InvalidOperationException($"The {label} must be {ChunkLengths.Sum()} ASCII bytes after newline normalization, got {publicKeyBytes.Length}");

            var chunks = new byte[ChunkCount][];
            var at = 0;
            for (var i = 0; i < ChunkCount; i++)
            {
                chunks[i] = publicKeyBytes.Skip(at).Take(ChunkLengths[i]).ToArray();
                at += ChunkLengths[i];
            }

            return chunks;
        }

        // Finds the single occurrence of pattern in data. Returns false if absent or ambiguous (found more than once) so the caller can fail safe instead of patching the wrong location.
        private static bool TryLocateUnique(byte[] data, byte[] pattern, out long offset)
        {
            offset = -1;
            var span = data.AsSpan();
            var start = 0;
            while (start <= data.Length - pattern.Length)
            {
                var index = span[start..].IndexOf(pattern);
                if (index < 0)
                    break;

                var absolute = start + index;
                if (offset >= 0)
                {
                    // More than one match: cannot safely choose one.
                    offset = -1;
                    return false;
                }

                offset = absolute;
                start = absolute + 1;
            }

            return offset >= 0;
        }

        private static byte[][] ReadChunks(FileStream stream, long[] offsets)
        {
            var chunks = new byte[offsets.Length][];

            for (var i = 0; i < offsets.Length; i++)
            {
                chunks[i] = new byte[ChunkLengths[i]];
                stream.Position = offsets[i];

                var read = stream.Read(chunks[i], 0, ChunkLengths[i]);
                if (read != ChunkLengths[i])
                    throw new IOException($"Could not read metadata patch chunk at 0x{offsets[i]:X}");
            }

            return chunks;
        }

        private static void WriteChunks(FileStream stream, long[] offsets, byte[][] chunks)
        {
            if (chunks.Length != offsets.Length)
                throw new InvalidOperationException("Invalid metadata patch chunk count");

            for (var i = 0; i < offsets.Length; i++)
            {
                if (chunks[i].Length != ChunkLengths[i])
                    throw new InvalidOperationException("Invalid metadata patch chunk length");

                stream.Position = offsets[i];
                stream.Write(chunks[i], 0, chunks[i].Length);
            }

            stream.Flush(true);
        }

        private static MetadataPatchState CreateState(long[] offsets, byte[][] originalChunks, byte[][] patchedChunks)
        {
            return new MetadataPatchState
            {
                MetadataPath = "",
                Chunks = Enumerable.Range(0, offsets.Length)
                    .Select(index => new MetadataPatchChunk
                    {
                        Offset = offsets[index],
                        Original = Convert.ToBase64String(originalChunks[index]),
                        Patched = Convert.ToBase64String(patchedChunks[index])
                    })
                    .ToList()
            };
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

        private void SaveState(string statePath, MetadataPatchState state)
        {
            state.MetadataPath = metadataPath ?? state.MetadataPath;
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        private static bool IsEnabled()
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_AUTO_PATCH_METADATA");
            if (!string.IsNullOrWhiteSpace(env) && bool.TryParse(env, out var enabled))
                return enabled;

            var config = Config.Instance.ServerConfiguration;
            return config.EnableGateway && config.AutoPatchClientMetadata;
        }

        internal static string GetMetadataPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_METADATA_PATH");
            if (string.IsNullOrWhiteSpace(configuredPath))
                configuredPath = Config.Instance.ServerConfiguration.ClientMetadataPath;

            if (!string.IsNullOrWhiteSpace(configuredPath))
                return ResolvePath(configuredPath);

            // Any Steam library can hold the install.
            return SteamGameLocator.FindGameFile(Path.Combine("BlueArchive_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
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
