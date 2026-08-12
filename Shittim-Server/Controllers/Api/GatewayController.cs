using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Core.Crypto;
using Microsoft.EntityFrameworkCore;
using Schale.Data.GameModel;
using Schale.MX.NetworkProtocol;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shittim_Server.Core;
using Shittim_Server.Services;
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

    // Official gateway DateTimes are ISO strings with NO timezone offset and whole-second precision, DB-backed values like "2026-07-27T04:12:50". The one fractional value official ever emits is DateTime.MaxValue ("9999-12-31T23:59:59.9999999") for never-ending excel dates, so that one keeps full precision. Also stops DateTimeKind.Local values leaking a "+01:00" suffix.
    // Not JsonConverter<DateTime>: that base seals CanConvert as typeof(DateTime).IsAssignableFrom(objectType), which is false for DateTime?, so every nullable date on the wire skips the converter and falls back to Newtonsoft's ISO format.
    public class OfficialDateTimeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            objectType == typeof(DateTime) || objectType == typeof(DateTime?);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not DateTime dateTime)
            {
                writer.WriteNull();
                return;
            }

            if (dateTime.Ticks >= DateTime.MaxValue.Ticks - TimeSpan.TicksPerSecond)
            {
                writer.WriteValue("9999-12-31T23:59:59.9999999");
                return;
            }

            writer.WriteValue(dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var nullable = objectType == typeof(DateTime?);

            return reader.Value switch
            {
                null => nullable ? null : default(DateTime),
                DateTime dt => dt,
                string s => DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
                _ => Convert.ToDateTime(reader.Value, System.Globalization.CultureInfo.InvariantCulture),
            };
        }
    }

    // The official server omits members it never assigned, collections included: ParcelResultDB only carries the handful of collections a given reward actually touched, and EchelonDB/AcademyDB never carry their server-internal maps. Newtonsoft's DefaultValueHandling.Ignore drops 0/false/null but keeps [] and {}, so honour [OmitWhenEmpty] here to reproduce the official key set.
    public class OfficialContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
    {
        protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(
            System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            var omitEmpty = member.IsDefined(typeof(Schale.MX.OmitWhenEmptyAttribute), true)
                || (member.DeclaringType?.IsDefined(typeof(Schale.MX.OmitWhenEmptyAttribute), true) ?? false);

            if (omitEmpty)
            {
                var previous = property.ShouldSerialize;
                var valueProvider = property.ValueProvider;
                property.ShouldSerialize = instance =>
                    (previous == null || previous(instance))
                    && valueProvider?.GetValue(instance) is not System.Collections.ICollection { Count: 0 };
            }

            return property;
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
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<Schale.Data.SchaleDataContext> _dbFactory;
        private static readonly byte[] RequestXorKey = { 0xD9 };

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, SemaphoreSlim> _accountGates = new();

        private static SemaphoreSlim GetAccountGate(long accountServerId)
            => _accountGates.GetOrAdd(accountServerId, _ => new SemaphoreSlim(1, 1));

        // Controllers are instantiated per request, so this safely carries the decoded body to CreateProtocolErrorResponse, which is reached from catch blocks where the local is out of scope and needs it to write a complete exchange to the wire dump.
        private string _wireRequestJson = "";
        
        // Official packet serialization rules, verified against live captures:
        // - null members omitted, AND default-valued members omitted (0 / false / default enum / default DateTime): ServerNotification only appears when non-zero, ItemDB fields with 0/false vanish, an empty AccountRestrictionsDB serializes as {}.
        // - floats keep Newtonsoft's default formatting (whole values carry ".0", e.g. 270.0), so no FloatConverter here.
        // - DateTimes: see OfficialDateTimeConverter.
        // - empty arrays/objects are only sent where official actually assigns the member; members it leaves unassigned are absent, which DefaultValueHandling does not cover, so [OmitWhenEmpty] members get dropped by OfficialContractResolver.
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            ContractResolver = new OfficialContractResolver(),
            Converters = { new OfficialDateTimeConverter() }
        };

        // Exposed so the wire-contract tests assert against the real settings rather than a copy.
        public static JsonSerializerSettings OfficialPacketJsonSettings => jsonSettings;

        private static readonly JsonSerializerSettings serverPacketSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            Converters = { new FloatConverter() }
        };

        public GatewayController(
            ILogger<GatewayController> logger,
            HandlerManager handlerManager,
            Microsoft.EntityFrameworkCore.IDbContextFactory<Schale.Data.SchaleDataContext> dbFactory)
        {
            _logger = logger;
            _handlerManager = handlerManager;
            _dbFactory = dbFactory;
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var protocol = Protocol.None;

            try
            {
                var gatewayPayload = DecodeGatewayPayload(formFile);
                responseCrypto = gatewayPayload.ResponseCrypto;

                var payloadStr = gatewayPayload.Json;
                _wireRequestJson = payloadStr;
                var jsonNode = JObject.Parse(payloadStr);
                var readProtocol = ReadProtocol(jsonNode);
                if (readProtocol == null)
                {
                    _logger.LogError("Failed to read protocol from JsonNode, {Payload}", payloadStr);
                    await CreateProtocolErrorResponse("Failed to read protocol", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }
                protocol = readProtocol.Value;
                var responseProtocolName = protocol.ToString();
                int? responseProtocolOverride = null;

                if (ShouldTreatAsQueuingGetTicketGL(protocol, jsonNode))
                {
                    protocol = Protocol.Queuing_GetTicket;
                    responseProtocolName = "Queuing_GetTicketGL";
                    responseProtocolOverride = 50001;
                }

                // Bodies are Debug, not Information: at default verbosity this would write every packet a player sends, session material included. The finally below emits one concise Information line per request instead.
                _logger.LogDebug("Request {ProtocolInt} / {Protocol}: {Payload}", (int)protocol, protocol, payloadStr);

                if (ServerNoticeService.IsGated(protocol))
                {
                    await CreateProtocolErrorResponse(ServerNoticeService.GateMessage, ServerNoticeService.GateError!.Value, responseCrypto);
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

                if (!_handlerManager.IsImplemented(protocol))
                {
                    // The payload rides along on the error rather than as a second Information-level body dump; this is a failure, so it keeps its context.
                    _logger.LogError("Protocol {Protocol} is unimplemented and left unhandled. Request: {Payload}", protocol, payloadStr);

                    await CreateProtocolErrorResponse("Protocol not implemented (Server Error)", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                // Official evaluates the notification flags at request ENTRY, not after handling: the Mail_Receive response that empties the mailbox still carries HasUnreadMail, and only the next response drops it.
                var notification = await ReadServerNotificationAsync(payload, protocol);

                // The client fires bursts of claims concurrently, e.g. several Mission_Reward at once, and parallel write transactions on one SQLite file can stall past the client's socket timeout; an unanswered request soft-locks the game, so dispatch is serialized per account.
                var accountGate = GetAccountGate(payload.SessionKey?.AccountServerId ?? 0);
                if (!await accountGate.WaitAsync(TimeSpan.FromSeconds(20)))
                {
                    _logger.LogError("Timed out waiting for the per-account dispatch gate on {Protocol}", protocol);
                    await CreateProtocolErrorResponse("Server busy", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                ResponsePacket rsp;
                try
                {
                    rsp = await _handlerManager.Dispatch(protocol, payload);
                }
                finally
                {
                    accountGate.Release();
                }

                if (rsp == null)
                {
                    _logger.LogError("Handler returned null for protocol {Protocol}", protocol);
                    await CreateProtocolErrorResponse("Handler error", WebAPIErrorCode.ServerFailedToHandleRequest, responseCrypto);
                    return;
                }

                // Official responses echo SessionKey plus the derived top-level AccountId ONLY on the account/security handshake protocols; every other response omits both. Handlers that mint a new session (CheckNexon/Create) set SessionKey themselves.
                if (rsp.SessionKey == null && ShouldEchoSessionKey(protocol))
                    rsp.SessionKey = payload.SessionKey;

                // OR rather than assign: ServerNotification is a flag set and official combines a handler's own bits with the mailbox baseline instead of letting either win, which is what produces Clan_Check = 2056 (CanReceiveClanAttendanceReward | HasUnreadMail).
                rsp.ServerNotification |= notification;

                var responseJson = JsonConvert.SerializeObject(rsp, jsonSettings);
                if (responseProtocolOverride.HasValue)
                    responseJson = OverridePacketProtocol(responseJson, responseProtocolOverride.Value);

                _logger.LogDebug("Response: {Rsp}", responseJson);
                Core.Diagnostics.GatewayWireLog.Write(
                    payloadStr, responseProtocolName, responseJson,
                    ShouldUseAes(responseCrypto), responseCrypto.Key.Length);

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
            catch (UnauthorizedAccessException ex)
            {
                // A dead or mismatched session must NOT surface as the generic 500 ("A request that cannot be processed"); InvalidSession triggers the client's clean return-to-title + relogin flow.
                _logger.LogWarning("Rejected request with invalid session: {Message}", ex.Message);
                if (!Response.HasStarted)
                {
                    await CreateProtocolErrorResponse(ex.Message, WebAPIErrorCode.InvalidSession, responseCrypto);
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
            finally
            {
                // One Information-level line per gateway request so normal operation stays observable without the payloads.
                _logger.LogInformation("{Method} {Route} {Protocol}({ProtocolInt}) -> {StatusCode} in {ElapsedMs}ms",
                    Request.Method,
                    Request.Path.Value,
                    protocol,
                    (int)protocol,
                    Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        // Protocols whose responses officially carry SessionKey/AccountId, from live captures: Account_CheckNexon, Account_Auth, ProofToken_RequestQuestion, ProofToken_Submit.
        private static bool ShouldEchoSessionKey(Protocol protocol) => protocol
            is Protocol.Account_Auth
            or Protocol.Account_Auth2
            or Protocol.ProofToken_RequestQuestion
            or Protocol.ProofToken_Submit;

        // The official server stamps ServerNotification on every in-session response; the baseline computed here is HasUnreadMail (8) whenever unclaimed mail exists, and handlers contribute their own bits on top - ClanHandler.Check adds CanReceiveClanAttendanceReward (2048), giving 2048 alone or 2056 when mail is also waiting.
        // NewMailArrived (4, seen as 12 = 8|4 on official's Attendance_Reward) is not baseline either: MailNotificationService holds it per account, and only the delivering handler and Mail_Check (report-and-consume) surface it.
        // These four are answered outside the account pipeline. In a capture whose account had unread mail from login to logout every response carried ServerNotification=8 except ProofToken_RequestQuestion / NetworkTime_Sync / ProofToken_Submit / Account_GetTutorial.
        private static bool SkipsServerNotification(Protocol protocol) => protocol
            is Protocol.NetworkTime_Sync
            or Protocol.ProofToken_RequestQuestion
            or Protocol.ProofToken_Submit
            or Protocol.Account_GetTutorial;

        private async Task<ServerNotificationFlag> ReadServerNotificationAsync(RequestPacket payload, Protocol protocol)
        {
            var accountServerId = payload.SessionKey?.AccountServerId ?? 0;
            if (accountServerId == 0 || SkipsServerNotification(protocol))
                return ServerNotificationFlag.None;

            // Operator-forced bits ride the same path as the mailbox baseline so the four skip protocols and pre-login requests stay unstamped, which is what official does.
            var forced = ServerNoticeService.Flags;

            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                // Official drops the flag as soon as the mail is claimed, and this goes through GetAccountMailbox so it uses the same predicate AND the same clock as Mail_Check/Mail_List. An account with ForceDateTime evaluates mail expiry against its own ServerDateTime(), so reading DateTime.Now here would let the two disagree in either direction: a red dot over an empty mailbox, or unread mail the client is never told about. In both captures the flag and the count appear together and vanish together on Mail_Receive.
                var account = await db.Accounts.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.ServerId == accountServerId);
                var now = account?.GameSettings?.ServerDateTime() ?? DateTime.Now;
                if (await db.GetAccountMailbox(accountServerId, now).AnyAsync())
                    return forced | ServerNotificationFlag.HasUnreadMail;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ServerNotification mail check failed");
            }

            return forced;
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

        // null means the field is absent or unreadable; an explicit 0 is a real protocol and routes like any other
        private static Protocol? ReadProtocol(JObject jsonNode)
        {
            var protocolNode = jsonNode["Protocol"] ?? jsonNode["protocol"];
            if (protocolNode == null)
                return null;

            if (protocolNode.Type == JTokenType.Integer)
                return (Protocol)protocolNode.Value<int>();

            return Enum.TryParse<Protocol>(protocolNode.Value<string>(), out var protocol) ? (Protocol?)protocol : null;
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

                // PacketCryptManager.EncryptRequest writes [crc][typeConversion][keyLen][ivLen][aesEncryptedKey][aesEncryptedIV][gzip+XOR body], so headerKey/headerIv are the handshake blobs we handed out, not key material - 32 bytes each, since they wrap the base64 of a 16-byte key under PKCS7. Handshake requests (GetCryptoKeys/CheckNexon) send keyLen=0 and both directions stay plaintext.
                var responseCrypto = (headerKey.Length > 0 && headerIv.Length == 16 && IsValidAesKeyLength(headerKey.Length))
                    ? new GatewayCryptoContext(true, headerKey, headerIv)
                    : GatewayCryptoContext.None;

                _logger.LogDebug(
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
            Core.Diagnostics.GatewayWireLog.Write(
                _wireRequestJson, res.Protocol, res.Packet,
                ShouldUseAes(crypto), crypto.Key.Length);

            string json = JsonConvert.SerializeObject(res, serverPacketSettings);
            if (ShouldUseAes(crypto))
                json = Convert.ToBase64String(HybridCryptor.EncryptGatewayResponse(Encoding.UTF8.GetBytes(json), crypto.Key, crypto.Iv));

            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }

        private async Task CreateProtocolResponse(ServerResponsePacket packet, GatewayCryptoContext crypto)
        {
            // The whole {protocol, packet} envelope goes inside the ciphertext, not around it. HttpGameSession's WaitForRequest coroutine (0x180da2773) hands the raw DownloadText to HttpGameMessage.DecodeResponse, which base64-decodes and AES-decrypts the entire body before anything looks at the protocol field; the plaintext-envelope shape is only reachable while the client still has no session blobs.
            string json = JsonConvert.SerializeObject(packet, serverPacketSettings);
            if (ShouldUseAes(crypto))
                json = Convert.ToBase64String(HybridCryptor.EncryptGatewayResponse(Encoding.UTF8.GetBytes(json), crypto.Key, crypto.Iv));

            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }
    }
}
