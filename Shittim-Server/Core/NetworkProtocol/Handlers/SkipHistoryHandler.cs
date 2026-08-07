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

    [ProtocolHandler(Protocol.SkipHistory_Save)]
    public async Task<SkipHistorySaveResponse> Save(
        SchaleDataContext db,
        SkipHistorySaveRequest request,
        SkipHistorySaveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.GameSettings.SkipHistory = request.SkipHistoryDB;
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        response.SkipHistoryDB = account.GameSettings.SkipHistory;
        return response;
    }
}
