using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ClearDeckHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public ClearDeckHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.ClearDeck_List)]
    public async Task<ClearDeckListResponse> List(
        SchaleDataContext db,
        ClearDeckListRequest request,
        ClearDeckListResponse response)
    {
        // No clear decks are recorded server-side; an empty list reads as "nobody has cleared this yet".
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClearDeckDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.ClearDeck_GroupedList)]
    public async Task<ClearDeckGroupedListResponse> GroupedList(
        SchaleDataContext db,
        ClearDeckGroupedListRequest request,
        ClearDeckGroupedListResponse response)
    {
        // No clear decks are recorded server-side; an empty list reads as "nobody has cleared this yet".
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClearDeckGroupedDBs = [];

        return response;
    }
}
