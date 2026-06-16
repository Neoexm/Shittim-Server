using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Core.Crypto;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Core
{
    public sealed record GatewaySessionCrypto(
        string EncryptedKey,
        string SignedKey,
        string EncryptedIV,
        string SignedIV);

    public sealed record GatewayAesCrypto(byte[] Key, byte[] Iv);

    public static class GatewaySessionCryptoBuilder
    {
        private const int MaxAesSessions = 32;
        private static readonly TimeSpan AesSessionTimeout = TimeSpan.FromMinutes(30);
        private static readonly ConcurrentDictionary<string, StoredGatewayAesCrypto> AesSessions = new();

        // Persist the gateway AES sessions across server restarts. A restart otherwise wipes these
        // in-memory sessions; a client that has switched to session-encrypted mode then keeps
        // resuming a session the server no longer knows, so its requests (50001 onward) fail to
        // decrypt and the client hangs at "Unpacking game resources". Reloading on startup lets a
        // resumed session keep working across a restart. (Single-account offline server, so writing
        // the session keys to a local file is acceptable.)
        private static readonly object PersistLock = new();
        private static string PersistPath => Path.Combine(Config.ConfigDirectory, "gateway_aes_sessions.json");

        static GatewaySessionCryptoBuilder()
        {
            LoadPersistedSessions();
        }

        // Most-recent client-generated key/IV from a handshake (GetCryptoKeys/CheckNexon). The client
        // uses this for in-session response decryption (see Build).
        private static volatile GatewayAesCrypto? _lastClientCrypto;

        public static GatewayAesCrypto? GetLastClientCrypto() => _lastClientCrypto;

        // History of recent handshake client cryptos (most-recent last). Used by the in-session
        // response-crypto sweep to try clientKey from GetCryptoKeys vs CheckNexon.
        private static readonly object _clientHistLock = new();
        private static readonly List<GatewayAesCrypto> _clientCryptoHistory = new();

        public static IReadOnlyList<GatewayAesCrypto> GetClientCryptoCandidates()
        {
            lock (_clientHistLock)
            {
                var list = new List<GatewayAesCrypto>(_clientCryptoHistory);
                list.Reverse(); // most-recent first
                return list;
            }
        }

        public static GatewaySessionCrypto Build(string? clientKeyText, string? clientIvText)
        {
            var (clientKey, clientIv) = DecodeClientCrypto(clientKeyText, clientIvText);
            var serverKey = RandomNumberGenerator.GetBytes(16);
            var serverIv = RandomNumberGenerator.GetBytes(16);
            RememberAes(serverKey, serverIv);


            return new GatewaySessionCrypto(
                EncryptAesBase64(Convert.ToBase64String(serverKey), clientKey, clientIv),
                SignBase64(serverKey),
                EncryptAesBase64(Convert.ToBase64String(serverIv), clientKey, clientIv),
                SignBase64(serverIv));
        }

        public static (byte[] Key, byte[] Iv) DecodeClientCrypto(string? keyText, string? ivText)
        {
            if (string.IsNullOrWhiteSpace(keyText) || string.IsNullOrWhiteSpace(ivText))
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Missing client generated crypto material");

            byte[] key, iv;
            try
            {
                key = Convert.FromBase64String(keyText);
                iv = Convert.FromBase64String(ivText);
            }
            catch (FormatException ex)
            {
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Invalid client generated crypto material: {ex.Message}");
            }

            // Two handshakes share this field with different framing:
            //  * Queuing_GetCryptoKeys sends the AES key/IV as raw bytes (16/24/32 + 16) in the clear.
            //  * Account_CheckNexon / Queuing_GetAuthTicket RSA-encrypt the AES key+IV with the gateway
            //    PUBLIC key that ClientMetadataPatchService injects into the client (256 bytes for the
            //    RSA-2048 key pair), so they arrive as RSA ciphertext.
            // RSA-decrypt with the matching gateway private key when the bytes are not already a valid
            // AES key/IV. Only a result of the right length (decrypted under the correct padding) is
            // accepted, so a wrong-padding garbage decrypt is rejected.
            if (!IsValidAesKeyLength(key.Length) && TryRsaDecrypt(key, IsValidAesKeyLength, out var decryptedKey))
                key = decryptedKey;
            if (iv.Length != 16 && TryRsaDecrypt(iv, length => length == 16, out var decryptedIv))
                iv = decryptedIv;

            if (!IsValidAesKeyLength(key.Length) || iv.Length != 16)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Invalid client generated crypto material length");

            return (key, iv);
        }

        private static bool IsValidAesKeyLength(int length) => length is 16 or 24 or 32;

        private static bool TryRsaDecrypt(byte[] payload, Func<int, bool> isValidLength, out byte[] result)
        {
            result = [];

            var privateKey = GetGatewayRsaPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
                return false;

            try
            {
                using var rsa = RSA.Create();
                if (!TryImportRsaPrivateKey(rsa, privateKey))
                    return false;

                foreach (var padding in new[]
                {
                    RSAEncryptionPadding.OaepSHA256,
                    RSAEncryptionPadding.OaepSHA1,
                    RSAEncryptionPadding.Pkcs1,
                    RSAEncryptionPadding.OaepSHA384,
                    RSAEncryptionPadding.OaepSHA512,
                })
                {
                    try
                    {
                        var decrypted = rsa.Decrypt(payload, padding);
                        if (isValidLength(decrypted.Length))
                        {
                            result = decrypted;
                            return true;
                        }
                    }
                    catch (CryptographicException)
                    {
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static string EncryptAesBase64(string text, byte[] key, byte[] iv)
        {
            // MUST stay CBC: this encrypts EncryptedKey/EncryptedIV AND the SqlCipher asset-DB key.
            // The client decrypts these with CBC (proven: assets unpack and login proceeds with CBC);
            // changing it stalls "Unpacking game resources". Only the in-session response mode is swept.
            var encrypted = HybridCryptor.EncryptTextAES(Encoding.UTF8.GetBytes(text), key, iv);
            return Convert.ToBase64String(encrypted);
        }

        public static IReadOnlyList<GatewayAesCrypto> GetAesCandidates()
        {
            PruneAesSessions();

            return AesSessions.Values
                .OrderByDescending(session => session.LastUsedUtc)
                .Select(session => new GatewayAesCrypto(
                    (byte[])session.Key.Clone(),
                    (byte[])session.Iv.Clone()))
                .ToList();
        }

        public static void TouchAes(byte[] key, byte[] iv)
        {
            RememberAes(key, iv);
        }

        private static void RememberAes(byte[] key, byte[] iv)
        {
            if (key.Length is not (16 or 24 or 32) || iv.Length != 16)
                return;

            var id = GetAesSessionId(key, iv);
            var now = DateTime.UtcNow;

            AesSessions.AddOrUpdate(
                id,
                _ => new StoredGatewayAesCrypto
                {
                    Key = (byte[])key.Clone(),
                    Iv = (byte[])iv.Clone(),
                    LastUsedUtc = now
                },
                (_, session) =>
                {
                    session.LastUsedUtc = now;
                    return session;
                });

            PruneAesSessions();
            PersistSessions();
        }

        private static void PruneAesSessions()
        {
            var cutoff = DateTime.UtcNow - AesSessionTimeout;

            foreach (var pair in AesSessions)
            {
                if (pair.Value.LastUsedUtc < cutoff)
                    AesSessions.TryRemove(pair.Key, out _);
            }

            if (AesSessions.Count <= MaxAesSessions)
                return;

            foreach (var pair in AesSessions.OrderByDescending(pair => pair.Value.LastUsedUtc).Skip(MaxAesSessions))
                AesSessions.TryRemove(pair.Key, out _);
        }

        private static string GetAesSessionId(byte[] key, byte[] iv)
        {
            var material = new byte[key.Length + iv.Length];
            Buffer.BlockCopy(key, 0, material, 0, key.Length);
            Buffer.BlockCopy(iv, 0, material, key.Length, iv.Length);

            return Convert.ToHexString(SHA256.HashData(material));
        }

        private static string SignBase64(byte[] data)
        {
            var privateKey = GetGatewayRsaPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
                return "";

            try
            {
                using var rsa = RSA.Create();
                if (!TryImportRsaPrivateKey(rsa, privateKey))
                    return "";

                return Convert.ToBase64String(rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
            catch
            {
                return "";
            }
        }

        private static string GetGatewayRsaPrivateKey()
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

        private static bool TryImportRsaPrivateKey(RSA rsa, string privateKey)
        {
            privateKey = privateKey.Trim();

            try
            {
                if (privateKey.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    rsa.ImportFromPem(privateKey);
                    return true;
                }

                var keyBytes = Convert.FromBase64String(privateKey);

                try
                {
                    rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                    return true;
                }
                catch (CryptographicException)
                {
                }

                rsa.ImportRSAPrivateKey(keyBytes, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            return File.Exists(basePath) ? basePath : Path.GetFullPath(path);
        }

        private sealed record PersistedGatewaySession(string Key, string Iv, long LastUsedTicks);

        private static void PersistSessions()
        {
            try
            {
                lock (PersistLock)
                {
                    var snapshot = AesSessions.Values
                        .Select(s => new PersistedGatewaySession(
                            Convert.ToBase64String(s.Key),
                            Convert.ToBase64String(s.Iv),
                            s.LastUsedUtc.Ticks))
                        .ToList();

                    var dir = Config.ConfigDirectory;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(PersistPath, System.Text.Json.JsonSerializer.Serialize(snapshot));
                }
            }
            catch
            {
                // Persistence is best-effort; never let it break a live handshake.
            }
        }

        private static void LoadPersistedSessions()
        {
            try
            {
                if (!File.Exists(PersistPath))
                    return;

                var snapshot = System.Text.Json.JsonSerializer
                    .Deserialize<List<PersistedGatewaySession>>(File.ReadAllText(PersistPath));
                if (snapshot == null)
                    return;

                // Refresh LastUsedUtc to "now" so a reloaded session survives the post-restart resume
                // window (the client resumes immediately on launch). Keep the newest MaxAesSessions.
                var now = DateTime.UtcNow;
                foreach (var entry in snapshot
                    .OrderByDescending(e => e.LastUsedTicks)
                    .Take(MaxAesSessions))
                {
                    byte[] key, iv;
                    try
                    {
                        key = Convert.FromBase64String(entry.Key);
                        iv = Convert.FromBase64String(entry.Iv);
                    }
                    catch
                    {
                        continue;
                    }

                    if (key.Length is not (16 or 24 or 32) || iv.Length != 16)
                        continue;

                    AesSessions[GetAesSessionId(key, iv)] = new StoredGatewayAesCrypto
                    {
                        Key = key,
                        Iv = iv,
                        LastUsedUtc = now
                    };
                }
            }
            catch
            {
                // Corrupt/missing persistence -> start empty.
            }
        }

        private sealed class StoredGatewayAesCrypto
        {
            public required byte[] Key { get; init; }
            public required byte[] Iv { get; init; }
            public DateTime LastUsedUtc { get; set; }
        }
    }
}
