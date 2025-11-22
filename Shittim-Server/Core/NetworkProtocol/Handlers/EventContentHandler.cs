using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EventContentHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public EventContentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.EventContent_CollectionList)]
    public async Task<EventContentCollectionListResponse> CollectionList(
        SchaleDataContext db,
        EventContentCollectionListRequest request,
        EventContentCollectionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_BoxGachaShopList)]
    public async Task<EventContentBoxGachaShopListResponse> BoxGachaShopList(
        SchaleDataContext db,
        EventContentBoxGachaShopListRequest request,
        EventContentBoxGachaShopListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.BoxGachaDB = new EventContentBoxGachaDB
        {
            EventContentId = request.EventContentId,
            PurchaseCount = 0,
            Round = 1,
            Seed = DateTime.UtcNow.Ticks
        };

        response.BoxGachaGroupIdByCount = [];

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_DiceRaceLobby)]
    public async Task<EventContentDiceRaceLobbyResponse> DiceRaceLobby(
        SchaleDataContext db,
        EventContentDiceRaceLobbyRequest request,
        EventContentDiceRaceLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.DiceRaceDB = new EventContentDiceRaceDB
        {
            EventContentId = request.EventContentId,
            DiceRollCount = 1,
            LapCount = 1,
            Node = 1,
            ReceiveRewardLapCount = 0
        };

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_PermanentList)]
    public async Task<EventContentPermanentListResponse> PermanentList(
        SchaleDataContext db,
        EventContentPermanentListRequest request,
        EventContentPermanentListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var permanents = db.GetAccountEventContentPermanents(account.ServerId).ToList();

        response.PermanentDBs = _mapper.Map<List<EventContentPermanentDB>>(permanents);

        return response;
    }
}
