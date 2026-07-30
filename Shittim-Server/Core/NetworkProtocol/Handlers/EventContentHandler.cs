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
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EventContentHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ShopManager _shopManager;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    public EventContentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ShopManager shopManager,
        ParcelHandler parcelHandler,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _shopManager = shopManager;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.EventContent_ShopList)]
    public async Task<EventContentShopListResponse> ShopList(
        SchaleDataContext db,
        EventContentShopListRequest request,
        EventContentShopListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // The event shop rides the regular shop pipeline: ShopExcel has no EventContentId, selection is purely by the EventContent_* category types the client asks for.
        var shopExcels = _excelService.GetTable<ShopExcelT>();
        response.ShopInfos = await _shopManager.GetShopList(db, account, shopExcels, request.CategoryList?.ToList() ?? []);
        response.ShopEligmaHistoryDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ShopBuyMerchandise)]
    public async Task<EventContentShopBuyMerchandiseResponse> ShopBuyMerchandise(
        SchaleDataContext db,
        EventContentShopBuyMerchandiseRequest request,
        EventContentShopBuyMerchandiseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        ShopHandler.ValidatePurchaseCount(request.PurchaseCount);

        var shopExcel = _excelService.GetTable<ShopExcelT>().FirstOrDefault(x => x.Id == request.ShopUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.ShopExcelNotFound, $"Shop {request.ShopUniqueId} not found");

        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == request.GoodsUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {request.GoodsUniqueId} not found");

        var purchaseHistory = await ShopManager.EnsurePurchasable(
            db, account, request.ShopUniqueId, shopExcel, request.PurchaseCount);

        var consumeParcels = new List<ParcelResult>();
        for (int i = 0; i < (goodsExcel.ConsumeParcelType?.Count ?? 0); i++)
            consumeParcels.Add(new ParcelResult(
                goodsExcel.ConsumeParcelType![i],
                goodsExcel.ConsumeParcelId![i],
                goodsExcel.ConsumeParcelAmount![i] * request.PurchaseCount));

        var rewardParcels = new List<ParcelResult>();
        for (int i = 0; i < (goodsExcel.ParcelType?.Count ?? 0); i++)
            rewardParcels.Add(new ParcelResult(
                goodsExcel.ParcelType![i],
                goodsExcel.ParcelId![i],
                goodsExcel.ParcelAmount![i] * request.PurchaseCount));

        if (consumeParcels.Count > 0)
            await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true);

        if (rewardParcels.Count > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, rewardParcels);
            response.ParcelResultDB = resolver.ParcelResult;
        }

        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper);

        purchaseHistory.PurchaseCount += request.PurchaseCount;

        response.ShopProductDB = new ShopProductDB
        {
            ShopExcelId = request.ShopUniqueId,
            Category = shopExcel.CategoryType,
            DisplayOrder = shopExcel.DisplayOrder,
            PurchaseCount = purchaseHistory.PurchaseCount,
            SoldOut = shopExcel.PurchaseCountLimit > 0
                && purchaseHistory.PurchaseCount >= shopExcel.PurchaseCountLimit,
            PurchaseCountLimit = shopExcel.PurchaseCountLimit
        };
        response.EventContentCollectionDBs = [];

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ReceiveStageTotalReward)]
    public async Task<EventContentReceiveStageTotalRewardResponse> ReceiveStageTotalReward(
        SchaleDataContext db,
        EventContentReceiveStageTotalRewardRequest request,
        EventContentReceiveStageTotalRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var received = account.ContentInfo.ReceivedEventStageTotalRewards ??= [];
        if (!received.TryGetValue(request.EventContentId, out var claimedIds))
        {
            claimedIds = [];
            received[request.EventContentId] = claimedIds;
        }

        // Progress gate: the current stack of the event's point item (season excel names it).
        var season = _excelService.GetTable<EventContentSeasonExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId);
        long eventItemAmount = 0;
        if (season != null && season.EventItemId != 0)
        {
            eventItemAmount = db.Items
                .FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == season.EventItemId)
                ?.StackCount ?? 0;
        }

        var claimable = _excelService.GetTable<EventContentStageTotalRewardExcelT>()
            .Where(x => x.EventContentId == request.EventContentId
                && !claimedIds.Contains(x.Id)
                && x.RequiredEventItemAmount <= eventItemAmount)
            .ToList();

        var parcels = new List<ParcelResult>();
        foreach (var row in claimable)
        {
            var count = new[]
            {
                row.RewardParcelType?.Count ?? 0,
                row.RewardParcelId?.Count ?? 0,
                row.RewardParcelAmount?.Count ?? 0
            }.Min();
            for (int i = 0; i < count; i++)
                parcels.Add(new ParcelResult(row.RewardParcelType![i], row.RewardParcelId![i], row.RewardParcelAmount![i]));

            claimedIds.Add(row.Id);
        }

        if (parcels.Count > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResultDB = resolver.ParcelResult;
        }
        else
        {
            response.ParcelResultDB = new ParcelResultDB
            {
                AccountDB = account.ToMap(_mapper),
                AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
            };
        }

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.EventContentId = request.EventContentId;
        response.AlreadyReceiveRewardId = claimedIds.ToList();

        return response;
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
            // Must be stable per account+event: the client lays the box out from this seed, and a fresh random seed on every open both reshuffles the box and (with the empty dict below) made the UI read the whole box as already drained.
            Seed = account.ServerId * 1_000_003 + request.EventContentId
        };

        // Remaining element count per group for round 1 - an empty dict means "box exhausted".
        response.BoxGachaGroupIdByCount = _excelService.GetTable<EventContentBoxGachaShopExcelT>()
            .Where(x => x.EventContentId == request.EventContentId && x.Round == 1)
            .GroupBy(x => x.GroupId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.GroupElementAmount));

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
