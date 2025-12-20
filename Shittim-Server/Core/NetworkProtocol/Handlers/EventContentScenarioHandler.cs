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

        if (!db.GetAccountScenarioGroupHistories(account.ServerId).Any(x => x.ScenarioGroupUqniueId == request.ScenarioGroupUniqueId))
        {
            db.ScenarioGroupHistories.Add(new ScenarioGroupHistoryDBServer
            {
                AccountServerId = request.AccountId,
                EventContentId = request.EventContentId,
                ScenarioGroupUqniueId = request.ScenarioGroupUniqueId,
                ScenarioType = request.ScenarioType,
                ClearDateTime = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        response.ScenarioGroupHistoryDBs = db.GetAccountScenarioGroupHistories(account.ServerId).ToMapList(_mapper);
        response.ParcelResultDB = new ParcelResultDB();

        return response;
    }
}
