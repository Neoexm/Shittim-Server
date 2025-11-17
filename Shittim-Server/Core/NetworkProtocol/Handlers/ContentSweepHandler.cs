using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ContentSweepHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;

    public ContentSweepHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.ContentSweep_Request)]
    public async Task<ContentSweepResponse> Request(
        SchaleDataContext db,
        ContentSweepRequest request,
        ContentSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClearParcels = [];
        response.CampaignStageHistoryDB = new();

        return response;
    }
}
