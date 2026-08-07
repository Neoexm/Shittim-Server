using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class OptionHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public OptionHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.Option_Save)]
    public async Task<OptionSaveResponse> Save(
        SchaleDataContext db,
        OptionSaveRequest request,
        OptionSaveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.OptionDB != null)
        {
            account.ContentInfo.Option = request.OptionDB;
            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();
        }

        return response;
    }
}
