using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CumulativeTimeRewardHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public CumulativeTimeRewardHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.CumulativeTimeReward_List)]
    public async Task<CumulativeTimeRewardListResponse> List(
        SchaleDataContext db,
        CumulativeTimeRewardListRequest request,
        CumulativeTimeRewardListResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.CumulativeTimeReward_Reward)]
    public async Task<CumulativeTimeRewardRewardResponse> Reward(
        SchaleDataContext db,
        CumulativeTimeRewardRewardRequest request,
        CumulativeTimeRewardRewardResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
