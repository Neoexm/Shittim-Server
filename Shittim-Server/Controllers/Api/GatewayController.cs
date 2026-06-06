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
                _logger.LogInformation("Response: {Rsp}", responseJson);

                var serverPacket = new ServerResponsePacket { Protocol = protocol.ToString(), Packet = responseJson };
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
            var payload = ReadExact(reader, (int)(reader.BaseStream.Length - reader.BaseStream.Position), "payload");

            if (payload.Length < 4)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Gateway payload is too short");

            XOR.Crypt(payload, RequestXorKey);

            var expectedPlainLength = BitConverter.ToInt32(payload, 0);
            var compressedPayload = payload[4..];
            var plainPayload = DecompressGZip(compressedPayload);

            if (expectedPlainLength >= 0 && expectedPlainLength != plainPayload.Length)
            {
                _logger.LogWarning(
                    "Gateway payload length mismatch. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, Expected: {ExpectedLength}, Actual: {ActualLength}",
                    crc,
                    typeConversion,
                    expectedPlainLength,
                    plainPayload.Length);
            }

            if (TryReadJson(plainPayload, out var plainJson))
            {
                _logger.LogDebug(
                    "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, AES: false",
                    crc,
                    typeConversion);

                return new GatewayPayload(plainJson, crc, typeConversion, GatewayCryptoContext.None);
            }

            if (IsValidAesKeyLength(headerKey.Length) && headerIv.Length == 16)
            {
                try
                {
                    var decryptedPayload = HybridCryptor.DecryptTextAES(plainPayload, headerKey, headerIv);
                    if (TryReadJson(decryptedPayload, out var decryptedJson))
                    {
                        _logger.LogDebug(
                            "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, AES: true",
                            crc,
                            typeConversion);

                        return new GatewayPayload(decryptedJson, crc, typeConversion, new GatewayCryptoContext(true, headerKey, headerIv));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Gateway AES decrypt attempt failed");
                }
            }

            if (TryDecryptRsaPayload(plainPayload, out var rsaJson))
            {
                _logger.LogDebug(
                    "Decoded gateway payload. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, RSA: true",
                    crc,
                    typeConversion);

                return new GatewayPayload(rsaJson, crc, typeConversion, GatewayCryptoContext.None);
            }

            var preview = Convert.ToHexString(plainPayload.AsSpan(0, Math.Min(plainPayload.Length, 32)));
            _logger.LogError(
                "Decoded gateway payload is not JSON. CRC: 0x{Crc:X8}, TypeConversion: {TypeConversion}, KeyLength: {KeyLength}, IvLength: {IvLength}, FirstBytes: {FirstBytes}",
                crc,
                typeConversion,
                keyLength,
                ivLength,
                preview);

            throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Decoded gateway payload is not JSON. First bytes: {preview}");
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

        private static bool TryReadJson(byte[] payload, out string json)
        {
            json = Encoding.UTF8.GetString(payload);

            var firstJsonChar = false;
            foreach (var value in json)
            {
                if (char.IsWhiteSpace(value))
                    continue;

                firstJsonChar = value == '{' || value == '[';
                break;
            }

            if (!firstJsonChar)
                return false;

            try
            {
                JToken.Parse(json);
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

        private sealed record GatewayPayload(string Json, uint Crc, int TypeConversion, GatewayCryptoContext ResponseCrypto);

        private sealed record GatewayCryptoContext(bool UseAes, byte[] Key, byte[] Iv)
        {
            public static GatewayCryptoContext None { get; } = new(false, Array.Empty<byte>(), Array.Empty<byte>());
        }

        private async Task CreateProtocolErrorResponse(string reason, WebAPIErrorCode errorCode, GatewayCryptoContext crypto)
        {
            var errorPacket = new ErrorPacket { Reason = reason, ErrorCode = errorCode };
            var res = new ServerResponsePacket { Protocol = Protocol.Error.ToString(), Packet = JsonConvert.SerializeObject(errorPacket, jsonSettings) };

            string json = JsonConvert.SerializeObject(res, serverPacketSettings);

            if (ShouldUseAes(crypto))
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = HybridCryptor.EncryptTextAES(plainBytes, crypto.Key, crypto.Iv);
                string encryptedBase64 = Convert.ToBase64String(encryptedBytes);
                
                Response.ContentType = "text/plain";
                await Response.WriteAsync(encryptedBase64);
                return;
            }

            _logger.LogInformation("Error Response: {Rsp}", JsonConvert.SerializeObject(errorPacket, jsonSettings));

            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }

        private async Task CreateProtocolResponse(ServerResponsePacket packet, GatewayCryptoContext crypto)
        {
            string json = JsonConvert.SerializeObject(packet, serverPacketSettings);

            if (ShouldUseAes(crypto))
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = HybridCryptor.EncryptTextAES(plainBytes, crypto.Key, crypto.Iv);
                string encryptedBase64 = Convert.ToBase64String(encryptedBytes);
                
                Response.ContentType = "text/plain";
                await Response.WriteAsync(encryptedBase64);
                return;
            }

            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }
    }
}
