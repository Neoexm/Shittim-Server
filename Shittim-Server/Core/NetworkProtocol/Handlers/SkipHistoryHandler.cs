using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class SkipHistoryHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public SkipHistoryHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.SkipHistory_List)]
    public async Task<SkipHistoryListResponse> List(
        SchaleDataContext db,
        SkipHistoryListRequest request,
        SkipHistoryListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var history = db.GetAccountSkipHistories(account.ServerId).FirstOrDefault();
        if (history != null)
            response.SkipHistoryDB = _mapper.Map<SkipHistoryDB>(history);

        return response;
    }

    [ProtocolHandler(Protocol.SkipHistory_Save)]
    public async Task<SkipHistorySaveResponse> Save(
        SchaleDataContext db,
        SkipHistorySaveRequest request,
        SkipHistorySaveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var history = db.GetAccountSkipHistories(account.ServerId).FirstOrDefault();
        if (history == null)
        {
            history = new SkipHistoryDBServer { AccountServerId = account.ServerId };
            db.SkipHistories.Add(history);
        }

        history.Prologue = request.SkipHistoryDB.Prologue;
        history.Tutorial = request.SkipHistoryDB.Tutorial?.ToDictionary(x => x.Key, x => x.Value);
        await db.SaveChangesAsync();

        response.SkipHistoryDB = _mapper.Map<SkipHistoryDB>(history);

        return response;
    }
}
