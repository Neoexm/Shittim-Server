using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ContentLogHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;

    public ContentLogHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.ContentLog_UIOpenStatistics)]
    public async Task<ContentLogUIOpenStatisticsResponse> UIOpenStatistics(
        SchaleDataContext db,
        ContentLogUIOpenStatisticsRequest request,
        ContentLogUIOpenStatisticsResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
