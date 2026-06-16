using System.Text;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class QueuingHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public QueuingHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.Queuing_GetCryptoKeys)]
    public Task<QueuingGetCryptoKeysResponse> GetCryptoKeys(
        SchaleDataContext db,
        QueuingGetCryptoKeysRequest request,
        QueuingGetCryptoKeysResponse response)
    {
        var gatewayCrypto = GatewaySessionCryptoBuilder.Build(request.ClientGeneratedKey, request.ClientGeneratedIV);
        var sqlCipher = BuildSqlCipherResponse(request.ClientGeneratedKey, request.ClientGeneratedIV);

        response.EncryptedKey = gatewayCrypto.EncryptedKey;
        response.SignedKey = gatewayCrypto.SignedKey;
        response.EncryptedIV = gatewayCrypto.EncryptedIV;
        response.SignedIV = gatewayCrypto.SignedIV;
        response.EncryptedSqlCipherKey = sqlCipher.EncryptedKey;
        response.EncryptedSqlCipherLicense = sqlCipher.EncryptedLicense;

        return Task.FromResult(response);
    }

    [ProtocolHandler(Protocol.Queuing_GetTicket)]
    public Task<QueuingGetTicketResponse> GetTicket(
        SchaleDataContext db,
        QueuingGetTicketRequest request,
        QueuingGetTicketResponse response)
    {
        if (!string.IsNullOrEmpty(request.ClientVersion))
        {
            if (Version.TryParse(request.ClientVersion, out var clientVersion))
            {
                var serverVersion = Config.Instance.ServerConfiguration.GameVersion;
                var majorMinorMismatch = clientVersion.Major != serverVersion.Major ||
                                         clientVersion.Minor != serverVersion.Minor;

                // Do not hard-fail queue bootstrap when client and server versions differ.
                // Returning a ticket lets startup continue so downstream bootstrap endpoints
                // (including gtable fetch) can be observed and aligned.
                _ = majorMinorMismatch;
            }
        }
        
        byte[] rawTicketBytes = Encoding.UTF8.GetBytes($"{request.NpSN}/{request.NpToken}");
        response.EnterTicket = Convert.ToBase64String(rawTicketBytes);
        response.Birth = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        response.ServerSeed = "";

        return Task.FromResult(response);
    }

    [ProtocolHandler(Protocol.Queuing_GetAuthTicket)]
    public Task<QueuingGetAuthTicketResponse> GetAuthTicket(
        SchaleDataContext db,
        QueuingGetAuthTicketRequest request,
        QueuingGetAuthTicketResponse response)
    {
        var gatewayCrypto = GatewaySessionCryptoBuilder.Build(request.ClientGeneratedKey, request.ClientGeneratedIV);
        var sqlCipher = BuildSqlCipherResponse(request.ClientGeneratedKey, request.ClientGeneratedIV);
        var rawTicketBytes = Encoding.UTF8.GetBytes($"{request.YostarUID}/{request.YostarToken}");

        response.EncryptedKey = gatewayCrypto.EncryptedKey;
        response.SignedKey = gatewayCrypto.SignedKey;
        response.EncryptedIV = gatewayCrypto.EncryptedIV;
        response.SignedIV = gatewayCrypto.SignedIV;
        response.EncryptedSqlCipherKey = sqlCipher.EncryptedKey;
        response.EncryptedSqlCipherLicense = sqlCipher.EncryptedLicense;
        response.Birth = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        response.AuthTicket = Convert.ToBase64String(rawTicketBytes);

        return Task.FromResult(response);
    }

    [ProtocolHandler(Protocol.Queuing_ProcessWaitingQueue)]
    public Task<QueuingProcessWaitingQueueResponse> ProcessWaitingQueue(
        SchaleDataContext db,
        QueuingProcessWaitingQueueRequest request,
        QueuingProcessWaitingQueueResponse response)
    {
        response.WaitingTicket = request.WaitingTicket;
        response.EnterTicket = string.IsNullOrEmpty(request.AuthTicket) ? request.WaitingTicket : request.AuthTicket;
        response.ServerSeed = "";

        return Task.FromResult(response);
    }

    private static (string EncryptedKey, string EncryptedLicense) BuildSqlCipherResponse(string? clientKeyText, string? clientIvText)
    {
        var (clientKey, clientIv) = GatewaySessionCryptoBuilder.DecodeClientCrypto(clientKeyText, clientIvText);
        var sqlCipherKey = Convert.ToBase64String(GetSqlCipherKeyBytes());
        var sqlCipherLicense = GetSqlCipherLicense();

        return (
            GatewaySessionCryptoBuilder.EncryptAesBase64(sqlCipherKey, clientKey, clientIv),
            GatewaySessionCryptoBuilder.EncryptAesBase64(sqlCipherLicense, clientKey, clientIv)
        );
    }

    private static byte[] GetSqlCipherKeyBytes()
    {
        var key = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY");
        if (string.IsNullOrWhiteSpace(key))
            key = Config.Instance.ServerConfiguration.ExcelDbSqlCipherKey;

        key = (key ?? "").Trim();

        if (key.StartsWith("x'", StringComparison.OrdinalIgnoreCase) && key.EndsWith("'"))
            key = key[2..^1];

        if (key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            key = key[2..];

        if (TryHexKey(key, out var hexKey))
            return hexKey;

        if (TryBase64Key(key, out var base64Key))
            return base64Key;

        return Encoding.UTF8.GetBytes(key);
    }

    private static string GetSqlCipherLicense()
    {
        var license = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_LICENSE");
        if (license == null)
            license = Config.Instance.ServerConfiguration.ExcelDbSqlCipherLicense;

        return license ?? "";
    }

    private static bool TryHexKey(string key, out byte[] keyBytes)
    {
        keyBytes = [];

        if (key.Length == 0 || key.Length % 2 != 0 || key.Any(value => !Uri.IsHexDigit(value)))
            return false;

        keyBytes = Convert.FromHexString(key);
        return true;
    }

    private static bool TryBase64Key(string key, out byte[] keyBytes)
    {
        try
        {
            keyBytes = Convert.FromBase64String(key);
            return keyBytes.Length > 0;
        }
        catch (FormatException)
        {
            keyBytes = [];
            return false;
        }
    }
}
