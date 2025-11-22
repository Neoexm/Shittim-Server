using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EventHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public EventHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.Event_RewardIncrease)]
    public async Task<EventRewardIncreaseResponse> RewardIncrease(
        SchaleDataContext db,
        EventRewardIncreaseRequest request,
        EventRewardIncreaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EventRewardIncreaseDBs = [];

        return response;
    }
}
