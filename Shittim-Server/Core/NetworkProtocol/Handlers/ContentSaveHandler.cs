using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ContentSaveHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;

    public ContentSaveHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.ContentSave_Get)]
    public async Task<ContentSaveGetResponse> Get(
        SchaleDataContext db,
        ContentSaveGetRequest request,
        ContentSaveGetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
