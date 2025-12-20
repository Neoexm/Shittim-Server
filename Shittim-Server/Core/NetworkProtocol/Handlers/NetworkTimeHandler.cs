using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class NetworkTimeHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public NetworkTimeHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.NetworkTime_Sync)]
    public async Task<NetworkTimeSyncResponse> Sync(
        SchaleDataContext db,
        NetworkTimeSyncRequest request,
        NetworkTimeSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var ticks = DateTime.UtcNow.Ticks;
        response.ReceiveTick = ticks;
        response.EchoSendTick = ticks;

        return response;
    }
}
