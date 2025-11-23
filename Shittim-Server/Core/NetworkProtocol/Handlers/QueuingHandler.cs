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

    [ProtocolHandler(Protocol.Queuing_GetTicketGL)]
    public async Task<QueuingGetTicketResponse> GetTicket(
        SchaleDataContext db,
        QueuingGetTicketRequest request,
        QueuingGetTicketResponse response)
    {
        if (!string.IsNullOrEmpty(request.ClientVersion))
        {
            var clientVersion = new Version(request.ClientVersion);
            var serverVersion = Config.Instance.ServerConfiguration.GameVersion;
            if (clientVersion.Major != serverVersion.Major || clientVersion.Minor != serverVersion.Minor)
                throw new WebAPIException(WebAPIErrorCode.InvalidVersion);
        }
        
        byte[] rawTicketBytes = Encoding.UTF8.GetBytes($"{request.NpSN}/{request.NpToken}");
        response.EnterTicket = Convert.ToBase64String(rawTicketBytes);

        return response;
    }
}
