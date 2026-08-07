using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class SkipHistoryHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public SkipHistoryHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.SkipHistory_List)]
    public async Task<SkipHistoryListResponse> List(
        SchaleDataContext db,
        SkipHistoryListRequest request,
        SkipHistoryListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.SkipHistoryDB = account.GameSettings.SkipHistory;
        return response;
    }

}
