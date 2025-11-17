using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Managers;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ItemHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ItemManager _itemManager;

    public ItemHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        IMapper mapper,
        ItemManager itemManager) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _itemManager = itemManager;
    }

    [ProtocolHandler(Protocol.Item_List)]
    public async Task<ItemListResponse> List(
        SchaleDataContext db,
        ItemListRequest request,
        ItemListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ItemDBs = db.GetAccountItems(account.ServerId).ToMapList(_mapper);
        response.ExpiryItemDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Item_SelectTicket)]
    public async Task<ItemSelectTicketResponse> SelectTicket(
        SchaleDataContext db,
        ItemSelectTicketRequest request,
        ItemSelectTicketResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelResultDB = await _itemManager.SelectTicket(db, account, request);

        response.ParcelResultDB = parcelResultDB;

        return response;
    }

    [ProtocolHandler(Protocol.Item_AutoSynth)]
    public async Task<ItemAutoSynthResponse> AutoSynth(
        SchaleDataContext db,
        ItemAutoSynthRequest request,
        ItemAutoSynthResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (itemDBParcel, equipmentDBParcel) = await _itemManager.AutoSynth(db, account, request);
            
        Dictionary<long, ItemDB> itemDB = _mapper.Map<Dictionary<long, ItemDB>>(itemDBParcel);
        Dictionary<long, EquipmentDB> eqDb = _mapper.Map<Dictionary<long, EquipmentDB>>(equipmentDBParcel);

        response.ParcelResultDB = new()
        {
            AccountDB = account.ToMap(_mapper),
            AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper),
            ItemDBs = itemDB,
            EquipmentDBs = eqDb
        };

        return response;
    }
}
