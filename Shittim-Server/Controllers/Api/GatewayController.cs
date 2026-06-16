using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Core.Crypto;
using Schale.MX.NetworkProtocol;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shittim_Server.Core;
using Protocol = Schale.MX.NetworkProtocol.Protocol;
using WebAPIErrorCode = Schale.MX.NetworkProtocol.WebAPIErrorCode;

namespace Shittim_Server.Controllers.Api
{
    public class FloatConverter : JsonConverter<float>
    {
        public override void WriteJson(JsonWriter writer, float value, JsonSerializer serializer)
        {
            if (value == Math.Floor(value))
                writer.WriteRawValue(((int)value).ToString());
            else
                writer.WriteValue(value);
        }

        public override float ReadJson(JsonReader reader, Type objectType, float existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return Convert.ToSingle(reader.Value);
        }
    }

    // BA's MX protocol represents time as .NET ticks (longs) — the ResponsePacket.ServerTimeTicks
    // field is emitted as a tick count, so the client deserializes DateTime members as ticks too.
    // Newtonsoft's default ISO-8601 string for DateTime members (e.g. AccountDB.LastConnectTime)
    // therefore fails client-side deserialization -> "A request that cannot be processed has been
    // received." right after Account_Auth (the first response that carries DateTime fields).
    // Emit every DateTime as its tick count to match.
    public class DateTimeTicksConverter : JsonConverter<DateTime>
    {
        public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer)
            => writer.WriteValue(value.Ticks);

        public override DateTime ReadJson(JsonReader reader, Type objectType, DateTime existingValue, bool hasExistingValue, JsonSerializer serializer)
            => reader.Value is null ? default : new DateTime(Convert.ToInt64(reader.Value));
    }
}

namespace Shittim_Server.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class GatewayController : ControllerBase
    {
        private readonly ILogger<GatewayController> _logger;
        private readonly HandlerManager _handlerManager;
        private static readonly byte[] RequestXorKey = { 0xD9 };
        
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new FloatConverter() }
        };

        private static readonly JsonSerializerSettings serverPacketSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            Converters = { new FloatConverter() }
        };

        public GatewayController(ILogger<GatewayController> logger, HandlerManager handlerManager)
        {
            _logger = logger;
            _handlerManager = handlerManager;
        }

        [HttpGet]
        [Route("Queuing/Ping")]
        public IResult Ping() => Results.Ok("Pong");

        [HttpGet("gateway")]
        public IResult GatewayHealthCheck() => Results.Ok();

        [HttpPost("gateway")]
        public async Task GatewayRequest()
        {
            var formFile = Request.Form.Files.GetFile("mx");
            if (formFile is null)
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Expecting an mx file");
                return;
            }

            var responseCrypto = GatewayCryptoContext.None;

            try
            {
                var gatewayPayload = DecodeGatewayPayload(formFile);
                responseCrypto = gatewayPayload.ResponseCrypto;

                var payloadStr = gatewayPayload.Json;
                var jsonNode = JObject.Parse(payloadStr);
                var protocol = ReadProtocol(jsonNode);
                var responseProtocolName = protocol.ToString();
                int? responseProtocolOverride = null;

                if (ShouldTreatAsQueuingGetTicketGL(protocol, jsonNode))
                {
                    protocol = Protocol.Queuing_GetTicket;
                    responseProtocolName = "Queuing_GetTicketGL";
                    responseProtocolOverride = 50001;
                }

                _logger.LogInformation("Protocol: {ProtocolInt} / {Protocol}", (int)protocol, protocol);
                _logger.LogInformation("Request: {Payload}", payloadStr);

                if (protocol == Protocol.None)
                {
                    _logger.LogError("Failed to read protocol from JsonNode, {Payload}", payloadStr);
                    await CreateProtocolErrorResponse("Failed to read protocol", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                var requestType = _handlerManager.GetRequestType(protocol);
                if (requestType == null)
                {
                    _logger.LogError("Protocol {Protocol} doesn't have corresponding type registered", protocol);
                    await CreateProtocolErrorResponse("Failed to handle protocol", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                var payload = (RequestPacket)JsonConvert.DeserializeObject(payloadStr, requestType)!;
                if (payload == null)
                {
                    _logger.LogError("Failed to deserialize payload to type {Type}", requestType.FullName);
                    await CreateProtocolErrorResponse("Malformed request", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                // NOTE: gateway responses are PLAINTEXT. Verified by live-RE of the v1.90.433063 client:
                // HttpGameSession.MoveNext uses aesKey = O6b74.get_Key() only when the session's
                // O4baecbba flag (+0x80) is non-null, and GameSessionManager.O26e18cb leaves it null,
                // so HttpGameMessage.DecryptOrReturnOriginal returns the body as-is (no AES). The
                // post-Auth "A request that cannot be processed" popup was NOT crypto — it was a content
                // problem in the AccountAuthResponse (DateTime ticks vs ISO + missing v433063 fields).

                using var lease = _handlerManager.GetHandlerLease(protocol);
                if (!lease.IsValid)
                {
                    _logger.LogInformation("{Protocol} {Payload}", protocol, payloadStr);
                    _logger.LogError("Protocol {Protocol} is unimplemented and left unhandled", protocol);

                    await CreateProtocolErrorResponse("Protocol not implemented (Server Error)", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                var rsp = await lease.Handler.Handle(payload);

                if (rsp == null)
                {
                    _logger.LogError("Handler returned null for protocol {Protocol}", protocol);
                    await CreateProtocolErrorResponse("Handler error", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                if (rsp.SessionKey == null)
                    rsp.SessionKey = payload.SessionKey;

                var responseJson = JsonConvert.SerializeObject(rsp, jsonSettings);
                if (responseProtocolOverride.HasValue)
                    responseJson = OverridePacketProtocol(responseJson, responseProtocolOverride.Value);

                _logger.LogInformation("Response: {Rsp}", responseJson);

                var serverPacket = new ServerResponsePacket { Protocol = responseProtocolName, Packet = responseJson };
                await CreateProtocolResponse(serverPacket, responseCrypto);
            }
            catch (WebAPIException ex)
            {
                if (!Response.HasStarted)
                {
                    await CreateProtocolErrorResponse(ex.Message, ex.ErrorCode, responseCrypto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing gateway request");
                if (!Response.HasStarted)
                {
                    await CreateProtocolErrorResponse(ex.Message, WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                }
            }
        }

        private GatewayPayload DecodeGatewayPayload(IFormFile formFile)
        {
            using var reader = new BinaryReader(formFile.OpenReadStream());

            if (reader.BaseStream.Length < 14)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Gateway packet is too short");

            var crc = reader.ReadUInt32();
            var typeConversion = reader.ReadInt32();
            var keyLength = reader.ReadByte();
            var ivLength = reader.ReadByte();
            var headerKey = ReadExact(reader, keyLength, "AES key");
            var headerIv = ReadExact(reader, ivLength, "AES IV");
            var rawPayload = ReadExact(reader, (int)(reader.BaseStream.Length - reader.BaseStream.Position), "payload");

            if (rawPayload.Length < 4)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Gateway payload is too short");

            var decodeFailures = new List<string>();
            var payloads = DecodeGatewayPayloadBodies(rawPayload, decodeFailures);
            foreach (var payload in payloads)
            {
                var gatewayPayload = TryBuildGatewayPayload(
                    payload,
                    crc,
                    typeConversion,
                    keyLength,
                    ivLength,
                    headerKey,
                    headerIv,
                    out var failure);

                if (gatewayPayload != null)
                    return gatewayPayload;

                decodeFailures.Add(failure);
            }

            var preview = Convert.ToHexString(rawPayload.AsSpan(0, Math.Min(rawPayload.Length, 32)));
            _logger.LogError(
                "Gateway payload could not be decoded. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, KeyLength: {KeyLength}, IvLength: {IvLength}, RawFirstBytes: {FirstBytes}, Attempts: {Attempts}",
                crc,
                typeConversion,
                keyLength,
                ivLength,
                preview,
                string.Join("; ", decodeFailures));

            throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Gateway payload could not be decoded. First bytes: {preview}");
        }

        private static Protocol ReadProtocol(JObject jsonNode)
        {
            var protocolNode = jsonNode["Protocol"] ?? jsonNode["protocol"];
            if (protocolNode == null)
                return Protocol.None;

            if (protocolNode.Type == JTokenType.Integer)
                return (Protocol)protocolNode.Value<int>();

            return Enum.TryParse<Protocol>(protocolNode.Value<string>(), out var protocol) ? protocol : Protocol.None;
        }

        private static bool ShouldTreatAsQueuingGetTicketGL(Protocol protocol, JObject jsonNode)
        {
            if (protocol != Protocol.Queuing_GetCryptoKeys)
                return false;

            if (jsonNode["ClientGeneratedKey"] != null || jsonNode["ClientGeneratedIV"] != null)
                return false;

            return jsonNode["NpSN"] != null || jsonNode["NpToken"] != null || jsonNode["Npacode"] != null;
        }

        private static string OverridePacketProtocol(string responseJson, int protocol)
        {
            var responseNode = JObject.Parse(responseJson);
            responseNode["Protocol"] = protocol;
            return responseNode.ToString(Formatting.None);
        }

        private static byte[] ReadExact(BinaryReader reader, int count, string fieldName)
        {
            if (count < 0)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Invalid gateway {fieldName} length");

            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Truncated gateway {fieldName}");

            return bytes;
        }

        private static byte[] DecompressGZip(byte[] compressedPayload)
        {
            using var gzStream = new GZipStream(new MemoryStream(compressedPayload), CompressionMode.Decompress);
            using var payloadMs = new MemoryStream();
            gzStream.CopyTo(payloadMs);
            return payloadMs.ToArray();
        }

        private static List<GatewayDecodedPayload> DecodeGatewayPayloadBodies(byte[] rawPayload, List<string> failures)
        {
            var decoded = new List<GatewayDecodedPayload>();

            var xorLengthPayload = (byte[])rawPayload.Clone();
            XOR.Crypt(xorLengthPayload, RequestXorKey);

            var xorExpectedLength = BitConverter.ToInt32(xorLengthPayload, 0);
            if (TryDecompressGZip(xorLengthPayload[4..], out var xorLengthPlain))
                decoded.Add(new GatewayDecodedPayload("xor-length-prefix", xorLengthPlain, xorExpectedLength));
            else
                failures.Add("xor-length-prefix:gzip failed");

            var clearExpectedLength = BitConverter.ToInt32(rawPayload, 0);
            var clearLengthCompressed = rawPayload[4..].ToArray();
            XOR.Crypt(clearLengthCompressed, RequestXorKey);

            if (TryDecompressGZip(clearLengthCompressed, out var clearLengthPlain))
                decoded.Add(new GatewayDecodedPayload("clear-length-prefix", clearLengthPlain, clearExpectedLength));
            else
                failures.Add("clear-length-prefix:gzip failed");

            var xorNoLengthPayload = (byte[])rawPayload.Clone();
            XOR.Crypt(xorNoLengthPayload, RequestXorKey);

            if (TryDecompressGZip(xorNoLengthPayload, out var xorNoLengthPlain))
                decoded.Add(new GatewayDecodedPayload("xor-no-length", xorNoLengthPlain, null));
            else
                failures.Add("xor-no-length:gzip failed");

            return decoded;
        }

        private GatewayPayload TryBuildGatewayPayload(
            GatewayDecodedPayload payload,
            uint crc,
            int typeConversion,
            byte keyLength,
            byte ivLength,
            byte[] headerKey,
            byte[] headerIv,
            out string failure)
        {
            if (TryReadJson(payload.Payload, out var plainJson))
            {
                LogGatewayPayloadLength(payload, crc, typeConversion);
                _logger.LogDebug(
                    "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, Format: {Format}, AES: false",
                    crc,
                    typeConversion,
                    payload.Format);

                // The request BODY is gzip+XOR (decodes to plaintext here), but the packet HEADER
                // carries the per-request AES key/IV the client will use to DECRYPT THE RESPONSE.
                // (Confirmed by RE of MX.Core.Crypto.PacketCryptManager.EncryptRequest @0x180F33600:
                // it writes [crc][typeConversion][keyLen][ivLen][aesKey][aesIV][gzip+XOR body];
                // the server's DecodeGatewayPayload already parses these as headerKey/headerIv.)
                // Handshake requests (GetCryptoKeys/CheckNexon) send keyLen=0 -> plaintext reply;
                // in-session requests (Account_Auth and after) send the key -> the reply MUST be
                // AES-encrypted with it. We previously discarded the header key (responseCrypto=None)
                // and replied in plaintext, so the client's HttpGameMessage.DecodeResponse
                // (DecryptOrReturnOriginal with a non-null key) failed to decrypt it ->
                // "A request that cannot be processed has been received." right after Account_Auth.
                var responseCrypto = (headerKey.Length > 0 && headerIv.Length == 16 && IsValidAesKeyLength(headerKey.Length))
                    ? new GatewayCryptoContext(true, headerKey, headerIv)
                    : GatewayCryptoContext.None;

                _logger.LogInformation(
                    "Gateway plaintext-body request: TypeConversion={TypeConversion}, headerKeyLen={KeyLen}, headerIvLen={IvLen} -> responseAes={ResponseAes}",
                    typeConversion,
                    headerKey.Length,
                    headerIv.Length,
                    responseCrypto.UseAes);

                failure = "";
                return new GatewayPayload(plainJson, crc, typeConversion, responseCrypto);
            }

            if (IsValidAesKeyLength(headerKey.Length) && headerIv.Length == 16)
            {
                try
                {
                    var decryptedPayload = HybridCryptor.DecryptTextAES(payload.Payload, headerKey, headerIv);
                    if (TryReadJson(decryptedPayload, out var decryptedJson))
                    {
                        GatewaySessionCryptoBuilder.TouchAes(headerKey, headerIv);
                        LogGatewayPayloadLength(payload, crc, typeConversion);
                        _logger.LogDebug(
                            "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, Format: {Format}, AES: true",
                            crc,
                            typeConversion,
                            payload.Format);

                        failure = "";
                        return new GatewayPayload(decryptedJson, crc, typeConversion, new GatewayCryptoContext(true, headerKey, headerIv));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Gateway AES decrypt attempt failed. Format: {Format}", payload.Format);
                }
            }

            foreach (var session in GatewaySessionCryptoBuilder.GetAesCandidates())
            {
                try
                {
                    var decryptedPayload = HybridCryptor.DecryptTextAES(payload.Payload, session.Key, session.Iv);
                    if (TryReadJson(decryptedPayload, out var decryptedJson))
                    {
                        GatewaySessionCryptoBuilder.TouchAes(session.Key, session.Iv);
                        LogGatewayPayloadLength(payload, crc, typeConversion);
                        _logger.LogDebug(
                            "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, Format: {Format}, AES: remembered",
                            crc,
                            typeConversion,
                            payload.Format);

                        failure = "";
                        return new GatewayPayload(decryptedJson, crc, typeConversion, new GatewayCryptoContext(true, session.Key, session.Iv));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Remembered gateway AES decrypt attempt failed. Format: {Format}", payload.Format);
                }
            }

            if (TryDecryptRsaPayload(payload.Payload, out var rsaJson))
            {
                LogGatewayPayloadLength(payload, crc, typeConversion);
                _logger.LogDebug(
                    "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, Format: {Format}, RSA: true",
                    crc,
                    typeConversion,
                    payload.Format);

                failure = "";
                return new GatewayPayload(rsaJson, crc, typeConversion, GatewayCryptoContext.None);
            }

            var preview = Convert.ToHexString(payload.Payload.AsSpan(0, Math.Min(payload.Payload.Length, 32)));
            failure = $"{payload.Format}:not JSON or decryptable JSON; KeyLength={keyLength}; IvLength={ivLength}; FirstBytes={preview}";
            return null;
        }

        private void LogGatewayPayloadLength(GatewayDecodedPayload payload, uint crc, int typeConversion)
        {
            if (payload.ExpectedPlainLength >= 0 && payload.ExpectedPlainLength != payload.Payload.Length)
            {
                _logger.LogWarning(
                    "Gateway payload length mismatch. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, Format: {Format}, Expected: {ExpectedLength}, Actual: {ActualLength}",
                    crc,
                    typeConversion,
                    payload.Format,
                    payload.ExpectedPlainLength,
                    payload.Payload.Length);
            }
        }

        private static bool TryDecompressGZip(byte[] compressedPayload, out byte[] plainPayload)
        {
            try
            {
                plainPayload = DecompressGZip(compressedPayload);
                return true;
            }
            catch (InvalidDataException)
            {
            }
            catch (IOException)
            {
            }

            plainPayload = Array.Empty<byte>();
            return false;
        }

        private static bool TryReadJson(byte[] payload, out string json)
        {
            json = Encoding.UTF8.GetString(payload);

            var firstJsonChar = false;
            foreach (var value in json)
            {
                if (char.IsWhiteSpace(value))
                    continue;

                firstJsonChar = value == '{';
                break;
            }

            if (!firstJsonChar)
                return false;

            try
            {
                JObject.Parse(json);
                return true;
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        private static bool IsValidAesKeyLength(int length)
        {
            return length is 16 or 24 or 32;
        }

        private static bool TryDecryptRsaPayload(byte[] payload, out string json)
        {
            json = "";
            var privateKey = GetGatewayRsaPrivateKey();

            if (string.IsNullOrWhiteSpace(privateKey))
                return false;

            try
            {
                using var rsa = RSA.Create();
                if (!TryImportRsaPrivateKey(rsa, privateKey))
                    return false;

                foreach (var padding in GetRsaPaddings())
                {
                    try
                    {
                        var decryptedPayload = rsa.Decrypt(payload, padding);
                        if (TryReadJson(decryptedPayload, out json))
                            return true;
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

        private static string GetGatewayRsaPrivateKey()
        {
            var privateKey = Environment.GetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PRIVATE_KEY");
            if (!string.IsNullOrWhiteSpace(privateKey))
                return privateKey;

            var privateKeyPath = Environment.GetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PRIVATE_KEY_PATH");
            if (string.IsNullOrWhiteSpace(privateKeyPath))
                privateKeyPath = Config.Instance.ServerConfiguration.GatewayRsaPrivateKeyPath;

            if (!string.IsNullOrWhiteSpace(privateKeyPath) && System.IO.File.Exists(privateKeyPath))
                return System.IO.File.ReadAllText(privateKeyPath);

            var defaultPrivateKeyPath = Path.Combine(Config.ConfigDirectory, "GatewayPrivateKey.pem");
            if (System.IO.File.Exists(defaultPrivateKeyPath))
                return System.IO.File.ReadAllText(defaultPrivateKeyPath);

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

        private static IEnumerable<RSAEncryptionPadding> GetRsaPaddings()
        {
            yield return RSAEncryptionPadding.OaepSHA1;
            yield return RSAEncryptionPadding.Pkcs1;
            yield return RSAEncryptionPadding.OaepSHA256;
            yield return RSAEncryptionPadding.OaepSHA384;
            yield return RSAEncryptionPadding.OaepSHA512;
        }

        private static bool ShouldUseAes(GatewayCryptoContext crypto)
        {
            return crypto.UseAes && IsValidAesKeyLength(crypto.Key.Length) && crypto.Iv.Length == 16;
        }

        // DIAGNOSTIC: pick the in-session response key from sweep.txt first token. Returns null
        // (=> plaintext reply, the baseline) unless sweep.txt explicitly names a key source. The
        // exact in-session crypto is UNRESOLVED — an exhaustive sweep of the handshake keys ×
        // standard modes was rejected by the client (see memory: account-auth-response-crypto).
        private static GatewayAesCrypto? SelectSweepKey()
        {
            string? src = null;
            try
            {
                const string f = @"C:\Users\tomda\Documents\Shittim-Server\sweep.txt";
                if (System.IO.File.Exists(f))
                {
                    using var fs = new System.IO.FileStream(f, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                    using var sr = new System.IO.StreamReader(fs);
                    foreach (var tok in sr.ReadToEnd().Trim().ToUpperInvariant().Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (tok is "CLIENT0" or "CLIENT1" or "SERVER0" or "SERVER1") { src = tok; break; }
                    }
                }
            }
            catch { }

            if (src == null)
                return null; // plaintext reply

            var clients = GatewaySessionCryptoBuilder.GetClientCryptoCandidates();
            var servers = GatewaySessionCryptoBuilder.GetAesCandidates();
            return src switch
            {
                "CLIENT0" => clients.ElementAtOrDefault(0),
                "CLIENT1" => clients.ElementAtOrDefault(1),
                "SERVER0" => servers.ElementAtOrDefault(0),
                "SERVER1" => servers.ElementAtOrDefault(1),
                _ => null,
            };
        }

        // In-session gateway responses are AES-encrypted with the client's session key. The exact AES
        // mode used by the obfuscated client decryptor (MX.Core.Crypto.Ob8c791dd...O5093e0d3) can't be
        // read statically (control-flow flattened), so the mode is selectable via env var while we
        // confirm it empirically against the live client. Default ECB-PKCS7 (matches the Nexon toy
        // SDK crypto; CBC-PKCS7 was rejected by the client).
        private static byte[] EncryptResponseAes(byte[] plain, byte[] key, byte[] iv)
        {
            // Sweep config from sweep.txt: "<keyIdx> <mode> <padding>" (e.g. "0 CBC PKCS7").
            // keyIdx picks the client crypto candidate (0=most recent=CheckNexon, 1=GetCryptoKeys);
            // -1 keeps the passed key/iv. Read per request so we can sweep without restarting.
            int keyIdx = 0;
            string mode = "ECB", pad = "PKCS7";
            try
            {
                const string f = @"C:\Users\tomda\Documents\Shittim-Server\sweep.txt";
                string? cfg = null;
                if (System.IO.File.Exists(f))
                {
                    using var fs = new System.IO.FileStream(f, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                    using var sr = new System.IO.StreamReader(fs);
                    cfg = sr.ReadToEnd();
                }
                if (!string.IsNullOrWhiteSpace(cfg))
                {
                    var parts = cfg.Trim().ToUpperInvariant().Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0) int.TryParse(parts[0], out keyIdx);
                    if (parts.Length > 1) mode = parts[1];
                    if (parts.Length > 2) pad = parts[2];
                }
            }
            catch { }

            if (keyIdx >= 0)
            {
                var cands = GatewaySessionCryptoBuilder.GetClientCryptoCandidates();
                if (keyIdx < cands.Count) { key = cands[keyIdx].Key; iv = cands[keyIdx].Iv; }
            }

            Console.WriteLine($"[SWEEP] keyIdx={keyIdx} mode={mode} pad={pad} keyHex={Convert.ToHexString(key)} ivHex={Convert.ToHexString(iv)} plainLen={plain.Length}");

            var padMode = pad switch
            {
                "NONE" => PaddingMode.None,
                "ZEROS" => PaddingMode.Zeros,
                "ANSIX923" => PaddingMode.ANSIX923,
                _ => PaddingMode.PKCS7,
            };

            if (mode == "CTR")
                return AesCtrCrypt(plain, key, iv);

            if (padMode == PaddingMode.None && plain.Length % 16 != 0)
            {
                var padded = new byte[((plain.Length / 16) + 1) * 16];
                Buffer.BlockCopy(plain, 0, padded, 0, plain.Length);
                plain = padded;
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Padding = padMode;
            switch (mode)
            {
                case "CBC": aes.Mode = CipherMode.CBC; aes.IV = iv; break;
                case "CFB": aes.Mode = CipherMode.CFB; aes.FeedbackSize = 128; aes.IV = iv; break;
                case "ECB": default: aes.Mode = CipherMode.ECB; break;
            }
            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plain, 0, plain.Length);
        }

        // AES-CTR (no native CipherMode.CTR in .NET): keystream = AES-ECB(counter), XOR with data.
        private static byte[] AesCtrCrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using var ecb = aes.CreateEncryptor();

            var output = new byte[data.Length];
            var counter = (byte[])iv.Clone();
            var keystream = new byte[16];
            for (int offset = 0; offset < data.Length; offset += 16)
            {
                ecb.TransformBlock(counter, 0, 16, keystream, 0);
                int block = Math.Min(16, data.Length - offset);
                for (int i = 0; i < block; i++)
                    output[offset + i] = (byte)(data[offset + i] ^ keystream[i]);
                for (int i = 15; i >= 0 && ++counter[i] == 0; i--) { }
            }
            return output;
        }

        private sealed record GatewayPayload(string Json, uint Crc, int TypeConversion, GatewayCryptoContext ResponseCrypto);

        private sealed record GatewayDecodedPayload(string Format, byte[] Payload, int? ExpectedPlainLength);

        private sealed record GatewayCryptoContext(bool UseAes, byte[] Key, byte[] Iv)
        {
            public static GatewayCryptoContext None { get; } = new(false, Array.Empty<byte>(), Array.Empty<byte>());
        }

        private async Task CreateProtocolErrorResponse(string reason, WebAPIErrorCode errorCode, GatewayCryptoContext crypto)
        {
            var errorPacket = new ErrorPacket { Reason = reason, ErrorCode = errorCode };
            var res = new ServerResponsePacket { Protocol = Protocol.Error.ToString(), Packet = JsonConvert.SerializeObject(errorPacket, jsonSettings) };

            _logger.LogInformation("Error Response: {Rsp}", res.Packet);

            // Same envelope rule as CreateProtocolResponse: plaintext envelope, encrypt only the inner packet.
            if (ShouldUseAes(crypto))
            {
                byte[] innerPlain = Encoding.UTF8.GetBytes(res.Packet);
                byte[] innerEnc = HybridCryptor.EncryptSweep(innerPlain, crypto.Key, crypto.Iv);
                res.Packet = Convert.ToBase64String(innerEnc);
            }

            string json = JsonConvert.SerializeObject(res, serverPacketSettings);
            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }

        private async Task CreateProtocolResponse(ServerResponsePacket packet, GatewayCryptoContext crypto)
        {
            // The outer {protocol, packet} envelope must stay PLAINTEXT json so the client can route
            // it by protocol; only the inner `packet` payload is AES-encrypted. The client parses the
            // envelope, then HttpGameMessage.DecodeResponse(aesKey, aesIV, packet) base64-decodes +
            // AES-decrypts the packet field (DecryptOrReturnOriginal). We previously encrypted the
            // whole envelope, so the client's envelope JSON parse failed before it ever decrypted ->
            // "A request that cannot be processed has been received." regardless of key/mode.
            if (ShouldUseAes(crypto))
            {
                byte[] innerPlain = Encoding.UTF8.GetBytes(packet.Packet);
                byte[] innerEnc = HybridCryptor.EncryptSweep(innerPlain, crypto.Key, crypto.Iv);
                packet.Packet = Convert.ToBase64String(innerEnc);
            }

            string json = JsonConvert.SerializeObject(packet, serverPacketSettings);
            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }
    }
}
