using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class DailyRecordHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public DailyRecordHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.DailyRecord_Reward)]
    public async Task<DailyRecordRewardResponse> Reward(
        SchaleDataContext db,
        DailyRecordRewardRequest request,
        DailyRecordRewardResponse response)
    {
        // Daily record books ride on cash product purchases, which never happen here; official omits DailyRecordDBs from Account_Auth so the client never has a record to claim against.
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
