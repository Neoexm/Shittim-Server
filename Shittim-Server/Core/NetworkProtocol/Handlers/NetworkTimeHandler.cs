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

        // Official semantics (live captures): ReceiveTick equals ServerTimeTicks exactly
        // (server receive time, whole-second precision, same clock base), while EchoSendTick is
        // the full-precision server time at send. The previous code used DateTime.UtcNow here
        // while ServerTimeTicks used local time — the two clocks disagreed by the UTC offset.
        response.ReceiveTick = response.ServerTimeTicks;
        response.EchoSendTick = DateTimeOffset.Now.Ticks;

        return response;
    }
}
