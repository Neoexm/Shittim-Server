using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class AuditHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public AuditHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.Audit_GachaStatistics)]
    public async Task<AuditGachaStatisticsResponse> GachaStatistics(
        SchaleDataContext db,
        AuditGachaStatisticsRequest request,
        AuditGachaStatisticsResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // The disclosure screen aggregates pulls across the player base; there is no population here.
        response.GachaResult = new Dictionary<long, long>();

        return response;
    }
}
