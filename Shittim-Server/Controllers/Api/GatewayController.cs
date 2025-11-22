using System.IO.Compression;
using System.Text;
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
    [ApiController]
    [Route("api")]
    public class GatewayController : ControllerBase
    {
        private readonly ILogger<GatewayController> _logger;
        private readonly HandlerManager _handlerManager;
        
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        
        private static readonly JsonSerializerSettings serverPacketSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
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

            using var reader = new BinaryReader(formFile.OpenReadStream());

            reader.ReadBytes(4); // CRC checksum
            reader.ReadBytes(4); // Type conversion
            var keyLength = reader.ReadByte();
            var ivLength = reader.ReadByte();

            bool needAes = keyLength != 0 && ivLength != 0;
            int headerSize = 0;
            byte[] aesKey = Array.Empty<byte>();
            byte[] aesIv = Array.Empty<byte>();

            if (needAes)
            {
                aesKey = (keyLength > 0) ? reader.ReadBytes(keyLength) : Array.Empty<byte>();
                aesIv = (ivLength > 0) ? reader.ReadBytes(ivLength) : Array.Empty<byte>();
                reader.ReadBytes(4); // payload length
                headerSize = 14 + keyLength + ivLength;
            }
            else
            {
                reader.ReadBytes(4); // payload length
                headerSize = 14;
            }

            byte[] compressedPayload = reader.ReadBytes((int)(reader.BaseStream.Length - headerSize));
            XOR.Crypt(compressedPayload, new byte[] { 0xD9 });
            
            // GZip decompress
            using var gzStream = new GZipStream(new MemoryStream(compressedPayload), CompressionMode.Decompress);
            using var payloadMs = new MemoryStream();
            gzStream.CopyTo(payloadMs);
            byte[] gzippedPayload = payloadMs.ToArray();

            // AES decrypt if needed
            byte[] decryptedPayload;
            if (needAes && aesKey.Length == 16 && aesIv.Length == 16)
            {
                decryptedPayload = HybridCryptor.DecryptTextAES(gzippedPayload, aesKey, aesIv);
            }
            else
            {
                decryptedPayload = gzippedPayload;
            }

            try
            {
                var payloadStr = Encoding.UTF8.GetString(decryptedPayload);
                var jsonNode = JObject.Parse(payloadStr);
                var protocol = (Protocol?)(jsonNode["Protocol"]?.Value<int>()) ?? Protocol.None;

                _logger.LogInformation("Protocol: {ProtocolInt} / {Protocol}", (int)protocol, protocol);
                _logger.LogInformation("Request: {Payload}", payloadStr);

                if (protocol == Protocol.None)
                {
                    _logger.LogError("Failed to read protocol from JsonNode, {Payload}", payloadStr);
                    await CreateProtocolErrorResponse("Failed to read protocol", WebAPIErrorCode.ServerFailedToHandleRequest, needAes, aesKey, aesIv);
                    return;
                }

                var requestType = _handlerManager.GetRequestType(protocol);
                if (requestType == null)
                {
                    _logger.LogError("Protocol {Protocol} doesn't have corresponding type registered", protocol);
                    await CreateProtocolErrorResponse("Failed to handle protocol", WebAPIErrorCode.ServerFailedToHandleRequest, needAes, aesKey, aesIv);
                    return;
                }

                var payload = (RequestPacket)JsonConvert.DeserializeObject(payloadStr, requestType)!;
                if (payload == null)
                {
                    _logger.LogError("Failed to deserialize payload to type {Type}", requestType.FullName);
                    await CreateProtocolErrorResponse("Malformed request", WebAPIErrorCode.ServerFailedToHandleRequest, needAes, aesKey, aesIv);
                    return;
                }

                using var lease = _handlerManager.GetHandlerLease(protocol);
                if (!lease.IsValid)
                {
                    _logger.LogInformation("{Protocol} {Payload}", protocol, payloadStr);
                    _logger.LogError("Protocol {Protocol} is unimplemented and left unhandled", protocol);

                    await CreateProtocolErrorResponse("Protocol not implemented (Server Error)", WebAPIErrorCode.ServerFailedToHandleRequest, needAes, aesKey, aesIv);
                    return;
                }

                var rsp = await lease.Handler.Handle(payload);

                if (rsp == null)
                {
                    _logger.LogError("Handler returned null for protocol {Protocol}", protocol);
                    await CreateProtocolErrorResponse("Handler error", WebAPIErrorCode.ServerFailedToHandleRequest, needAes, aesKey, aesIv);
                    return;
                }

                if (rsp.SessionKey == null)
                    rsp.SessionKey = payload.SessionKey;

                var responseJson = JsonConvert.SerializeObject(rsp, jsonSettings);
                _logger.LogInformation("Response: {Rsp}", responseJson);

                var serverPacket = new ServerResponsePacket { Protocol = protocol.ToString(), Packet = responseJson };
                await CreateProtocolResponse(serverPacket, needAes, aesKey, aesIv);
            }
            catch (WebAPIException ex)
            {
                if (!Response.HasStarted)
                {
                    await CreateProtocolErrorResponse(ex.Message, ex.ErrorCode, needAes, aesKey, aesIv);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing gateway request");
                if (!Response.HasStarted)
                {
                    await CreateProtocolErrorResponse(ex.Message, WebAPIErrorCode.ServerFailedToHandleRequest, needAes, aesKey, aesIv);
                }
            }
        }

        private async Task CreateProtocolErrorResponse(string reason, WebAPIErrorCode errorCode, bool aes, byte[] aesKey, byte[] aesIv)
        {
            var errorPacket = new ErrorPacket { Reason = reason, ErrorCode = errorCode };
            var res = new ServerResponsePacket { Protocol = Protocol.Error.ToString(), Packet = JsonConvert.SerializeObject(errorPacket, jsonSettings) };

            string json = JsonConvert.SerializeObject(res, serverPacketSettings);

            if (aes && aesKey.Length == 16 && aesIv.Length == 16)
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = HybridCryptor.EncryptTextAES(plainBytes, aesKey, aesIv);
                string encryptedBase64 = Convert.ToBase64String(encryptedBytes);
                
                Response.ContentType = "text/plain";
                await Response.WriteAsync(encryptedBase64);
                return;
            }

            _logger.LogInformation("Error Response: {Rsp}", JsonConvert.SerializeObject(errorPacket, jsonSettings));

            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }

        private async Task CreateProtocolResponse(ServerResponsePacket packet, bool aes, byte[] aesKey, byte[] aesIv)
        {
            string json = JsonConvert.SerializeObject(packet, serverPacketSettings);

            if (aes && aesKey.Length == 16 && aesIv.Length == 16)
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = HybridCryptor.EncryptTextAES(plainBytes, aesKey, aesIv);
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
