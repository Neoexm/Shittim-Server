using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ToastHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public ToastHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.Toast_List)]
    public async Task<ToastListResponse> List(
        SchaleDataContext db,
        ToastListRequest request,
        ToastListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ToastDBs = [];

        return response;
    }
}
