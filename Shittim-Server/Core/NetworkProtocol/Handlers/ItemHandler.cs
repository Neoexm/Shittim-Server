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
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ItemManager _itemManager;

    public ItemHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
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

    [ProtocolHandler(Protocol.Item_Sell)]
    public async Task<ItemSellResponse> Sell(
        SchaleDataContext db,
        ItemSellRequest request,
        ItemSellResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // nothing in ItemExcel carries a sell price, so selling just discards the stacks
        var targetIds = request.TargetServerIds ?? [];
        var targets = db.GetAccountItems(account.ServerId).Where(x => targetIds.Contains(x.ServerId)).ToList();
        db.Items.RemoveRange(targets);
        await db.SaveChangesAsync();

        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Item_Lock)]
    public async Task<ItemLockResponse> Lock(
        SchaleDataContext db,
        ItemLockRequest request,
        ItemLockResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // lock state lives client-side; the wire ItemDB has no field to carry it back anyway
        response.ItemDB = db.GetAccountItems(account.ServerId).Where(x => x.ServerId == request.TargetServerId).FirstMapTo(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Item_SelectTicket)]
    public async Task<ItemSelectTicketResponse> SelectTicket(
        SchaleDataContext db,
        ItemSelectTicketRequest request,
        ItemSelectTicketResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (usedItem, parcelResultDB) = await _itemManager.SelectTicket(db, account, request);

        response.UsedItemDB = usedItem.ToMap(_mapper);
        response.ParcelResultDB = parcelResultDB;

        return response;
    }

    [ProtocolHandler(Protocol.Item_Consume)]
    public async Task<ItemConsumeResponse> Consume(
        SchaleDataContext db,
        ItemConsumeRequest request,
        ItemConsumeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (usedItem, parcelResultDB) = await _itemManager.Consume(db, account, request);

        response.UsedItemDB = usedItem.ToMap(_mapper);
        response.NewParcelResultDB = parcelResultDB;

        return response;
    }

    [ProtocolHandler(Protocol.Item_BulkConsume)]
    public async Task<ItemBulkConsumeResponse> BulkConsume(
        SchaleDataContext db,
        ItemBulkConsumeRequest request,
        ItemBulkConsumeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (usedItem, parcelInfos) = await _itemManager.BulkConsume(db, account, request);

        response.UsedItemDB = usedItem.ToMap(_mapper);
        response.ParcelInfosInMailBox = parcelInfos;

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
