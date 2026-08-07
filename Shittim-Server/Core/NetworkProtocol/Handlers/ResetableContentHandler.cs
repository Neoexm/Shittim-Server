using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ResetableContentHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public ResetableContentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.ResetableContent_Get)]
    public async Task<ResetableContentGetResponse> Get(
        SchaleDataContext db,
        ResetableContentGetRequest request,
        ResetableContentGetResponse response)
    {
        // nothing tracks per-reset content values server-side; empty means everything sits at its default
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ResetableContentValueDBs = [];

        return response;
    }
}
