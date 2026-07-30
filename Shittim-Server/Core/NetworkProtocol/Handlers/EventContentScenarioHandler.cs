using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EventContentScenarioHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public EventContentScenarioHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.EventContent_ScenarioGroupHistoryUpdate)]
    public async Task<EventContentScenarioGroupHistoryUpdateResponse> ScenarioGroupHistoryUpdate(
        SchaleDataContext db,
        EventContentScenarioGroupHistoryUpdateRequest request,
        EventContentScenarioGroupHistoryUpdateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (!db.GetAccountScenarioGroupHistories(account.ServerId).Any(x =>
                x.ScenarioGroupUqniueId == request.ScenarioGroupUniqueId
                && x.EventContentId == request.EventContentId))
        {
            db.ScenarioGroupHistories.Add(new ScenarioGroupHistoryDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId,
                ScenarioGroupUqniueId = request.ScenarioGroupUniqueId,
                ScenarioType = request.ScenarioType,
                ClearDateTime = account.GameSettings.ServerDateTime()
            });

            await db.SaveChangesAsync();
        }

        // Only this event's rows: leaking the account's whole scenario history (main story rows with no EventContentId) into the per-event view wedged the client after event stories.
        response.ScenarioGroupHistoryDBs = db.GetAccountScenarioGroupHistories(account.ServerId)
            .Where(x => x.EventContentId == request.EventContentId)
            .ToMapList(_mapper);
        response.EventContentCollectionDBs = [];
        response.ParcelResultDB = new ParcelResultDB();

        return response;
    }
}
