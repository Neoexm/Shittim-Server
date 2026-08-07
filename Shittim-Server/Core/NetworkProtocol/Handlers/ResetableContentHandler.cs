using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

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
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ResetableContentValueDBs = ResetableContentService.LiveValues(
            account, ResetableContentService.ResetWindow);
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        return response;
    }
}
