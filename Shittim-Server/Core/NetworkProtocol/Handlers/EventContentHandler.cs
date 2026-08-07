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
    private readonly EventContentCollectionService _collectionService;
    private readonly MiniGameMissionService _missionService;

    public EventContentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ShopManager shopManager,
        ParcelHandler parcelHandler,
        IMapper mapper,
        EventContentCollectionService collectionService,
        MiniGameMissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _shopManager = shopManager;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
        _collectionService = collectionService;
        _missionService = missionService;
    }

    [ProtocolHandler(Protocol.EventContent_ShopList)]
    public async Task<EventContentShopListResponse> ShopList(
        SchaleDataContext db,
        EventContentShopListRequest request,
        EventContentShopListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var shopExcels = EventShop(request.EventContentId);
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

        var shopExcel = EventShop(request.EventContentId).FirstOrDefault(x => x.Id == request.ShopUniqueId)
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
        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.PurchaseSpecificItemCount);

        await db.SaveChangesAsync();

        return response;
    }

    // ShopExcel carries no EventContent_* row at all, so the tab is fed from EventContentShopExcel, which is the same shape keyed by event. SalePeriod on those rows is the phase calendar the run originally had - three rows on event 854 have one and the rest are blank - and keeping it would empty the shop of any event replayed outside its old window, which is the whole point of the schedule override.
    private List<ShopExcelT> EventShop(long eventContentId) => _excelService.GetTable<EventContentShopExcelT>()
        .Where(x => x.EventContentId == eventContentId)
        .Select(x => new ShopExcelT
        {
            Id = x.Id,
            LocalizeEtcId = x.LocalizeEtcId,
            CategoryType = x.CategoryType,
            IsLegacy = x.IsLegacy,
            GoodsId = x.GoodsId,
            DisplayOrder = x.DisplayOrder,
            PurchaseCooltimeMin = x.PurchaseCooltimeMin,
            PurchaseCountLimit = x.PurchaseCountLimit,
            PurchaseCountResetType = x.PurchaseCountResetType,
            BuyReportEventName = x.BuyReportEventName,
            RestrictBuyWhenInventoryFull = x.RestrictBuyWhenInventoryFull,
            SalePeriodFrom = "",
            SalePeriodTo = ""
        })
        .ToList();

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

        // Progress gate: the current stack of the event's point item. EventContentSeasonExcel.EventItemId is blank on about half the events (836 and 10836 among them) while the currency table always carries an EventPoint row, so that is the one to trust.
        var pointItemId = _excelService.GetTable<EventContentCurrencyItemExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId && x.EventContentItemType == EventContentItemType.EventPoint)
            ?.ItemUniqueId ?? 0;
        if (pointItemId == 0)
        {
            pointItemId = _excelService.GetTable<EventContentSeasonExcelT>()
                .FirstOrDefault(x => x.EventContentId == request.EventContentId)?.EventItemId ?? 0;
        }

        long eventItemAmount = 0;
        if (pointItemId != 0)
        {
            eventItemAmount = db.Items
                .FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == pointItemId)
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

        // Opening the album is the only moment a lot of these conditions get re-measured - the stage/scenario/mission handlers that satisfy them have no reason to know collections exist.
        _collectionService.UnlockAll(db, account, request.EventContentId);
        await db.SaveChangesAsync();

        var owned = db.GetAccountEventContentCollections(account.ServerId)
            .Where(x => x.EventContentId == request.EventContentId)
            .ToList();

        if (request.GroupId.HasValue)
            owned = owned.Where(x => x.GroupId == request.GroupId.Value).ToList();

        response.EventContentUnlockCGDBs = owned
            .Select(x => new EventContentCollectionDB
            {
                EventContentId = x.EventContentId,
                GroupId = x.GroupId,
                UniqueId = x.UniqueId,
                ReceiveDate = x.ReceiveDate
            })
            .ToList();

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_CollectionForMission)]
    public async Task<EventContentCollectionForMissionResponse> CollectionForMission(
        SchaleDataContext db,
        EventContentCollectionForMissionRequest request,
        EventContentCollectionForMissionResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var unlocked = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.ClearSpecificEventMission);
        await db.SaveChangesAsync();

        response.EventContentCollectionDBs = unlocked;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_BoxGachaShopList)]
    public async Task<EventContentBoxGachaShopListResponse> BoxGachaShopList(
        SchaleDataContext db,
        EventContentBoxGachaShopListRequest request,
        EventContentBoxGachaShopListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        if (play.BoxGachaRound == 0)
        {
            play.BoxGachaRound = 1;
            play.BoxGachaSeed = Random.Shared.NextInt64(1, int.MaxValue);
            await db.SaveChangesAsync();
        }

        response.BoxGachaDB = BoxGachaDB(play);
        response.BoxGachaGroupIdByCount = RemainingBoxGachaGroups(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_BoxGachaShopPurchase)]
    public async Task<EventContentBoxGachaShopPurchaseResponse> BoxGachaShopPurchase(
        SchaleDataContext db,
        EventContentBoxGachaShopPurchaseRequest request,
        EventContentBoxGachaShopPurchaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        if (play.BoxGachaRound == 0)
        {
            play.BoxGachaRound = 1;
            play.BoxGachaSeed = Random.Shared.NextInt64(1, int.MaxValue);
        }

        var drawnIds = play.BoxGachaDrawnElementIds.ToHashSet();
        var pool = _excelService.GetTable<EventContentBoxGachaElementExcelT>()
            .Where(x => x.EventContentId == request.EventContentId && x.Round == play.BoxGachaRound && !drawnIds.Contains(x.Id))
            .ToList();

        var drawCount = request.PurchaseAll ? pool.Count : (int)request.PurchaseCount;
        if (drawCount <= 0 || drawCount > pool.Count)
            throw new WebAPIException(WebAPIErrorCode.EventContentBoxGachaPurchaseFailed, $"Event {request.EventContentId} round {play.BoxGachaRound}: asked for {drawCount} of {pool.Count} remaining");

        var manage = _excelService.GetTable<EventContentBoxGachaManageExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId && x.Round == play.BoxGachaRound)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentBoxGachaPurchaseFailed, $"No box gacha manage row for event {request.EventContentId} round {play.BoxGachaRound}");

        var goodsTable = _excelService.GetTable<GoodsExcelT>();
        var costGoods = goodsTable.FirstOrDefault(x => x.Id == manage.GoodsId)
            ?? throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {manage.GoodsId} not found");

        var consumeParcels = new List<ParcelResult>();
        for (int i = 0; i < (costGoods.ConsumeParcelType?.Count ?? 0); i++)
            consumeParcels.Add(new ParcelResult(costGoods.ConsumeParcelType![i], costGoods.ConsumeParcelId![i], costGoods.ConsumeParcelAmount![i] * drawCount));

        var shopRows = _excelService.GetTable<EventContentBoxGachaShopExcelT>()
            .Where(x => x.EventContentId == request.EventContentId && x.Round == play.BoxGachaRound)
            .ToDictionary(x => x.GroupId);

        var elements = new List<EventContentBoxGachaElement>();
        var rewardParcels = new List<ParcelResult>();

        foreach (var drawn in pool.OrderBy(_ => Random.Shared.Next()).Take(drawCount))
        {
            shopRows.TryGetValue(drawn.GroupId, out var shopRow);

            var elementRewards = new List<ParcelInfo>();
            foreach (var goodsId in shopRow?.GoodsId ?? [])
            {
                var goods = goodsTable.FirstOrDefault(x => x.Id == goodsId);
                if (goods == null)
                    continue;

                for (int i = 0; i < (goods.ParcelType?.Count ?? 0); i++)
                {
                    elementRewards.AddRange(ParcelInfo.CreateParcelInfo(goods.ParcelType![i], goods.ParcelId![i], goods.ParcelAmount![i]));
                    rewardParcels.Add(new ParcelResult(goods.ParcelType![i], goods.ParcelId![i], goods.ParcelAmount![i]));
                }
            }

            elements.Add(new EventContentBoxGachaElement
            {
                EventContentId = request.EventContentId,
                Round = play.BoxGachaRound,
                GroupId = drawn.GroupId,
                UniqueId = drawn.Id,
                IsPrize = shopRow != null && shopRow.IsPrize,
                Rewards = elementRewards
            });

            play.BoxGachaDrawnElementIds.Add(drawn.Id);
        }

        play.BoxGachaPurchaseCount += drawCount;

        ParcelResultDB? consumed = null;
        if (consumeParcels.Count > 0)
            consumed = (await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true)).ParcelResult;

        var resolver = await _parcelHandler.BuildParcel(db, account, rewardParcels, consumed);
        response.ParcelResultDB = resolver.ParcelResult;

        await db.SaveChangesAsync();

        response.BoxGachaDB = BoxGachaDB(play);
        response.BoxGachaGroupIdByCount = RemainingBoxGachaGroups(play);
        response.BoxGachaElements = elements;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_BoxGachaShopRefresh)]
    public async Task<EventContentBoxGachaShopRefreshResponse> BoxGachaShopRefresh(
        SchaleDataContext db,
        EventContentBoxGachaShopRefreshRequest request,
        EventContentBoxGachaShopRefreshResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        var lastRound = _excelService.GetTable<EventContentBoxGachaManageExcelT>()
            .Where(x => x.EventContentId == request.EventContentId)
            .Select(x => x.Round)
            .DefaultIfEmpty(0)
            .Max();

        if (play.BoxGachaRound == 0 || lastRound == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentBoxGachaCannotRefresh, $"Event {request.EventContentId} has no box gacha to refresh");

        if (RemainingBoxGachaGroups(play).Count > 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentBoxGachaCannotRefresh, $"Event {request.EventContentId} round {play.BoxGachaRound} still has undrawn elements");

        // The last round loops on itself; every earlier one hands over to the next. Matches the client's CanRefresh, which only lets the button through on a drained pool and only stops at the last round when it is not a loop round.
        var isLoop = _excelService.GetTable<EventContentBoxGachaManageExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId && x.Round == play.BoxGachaRound)?.IsLoop ?? false;

        if (!isLoop && play.BoxGachaRound >= lastRound)
            throw new WebAPIException(WebAPIErrorCode.EventContentBoxGachaCannotRefresh, $"Event {request.EventContentId} is on its final round {play.BoxGachaRound}");

        if (!isLoop)
            play.BoxGachaRound++;

        play.BoxGachaDrawnElementIds = [];
        play.BoxGachaSeed = Random.Shared.NextInt64(1, int.MaxValue);

        await db.SaveChangesAsync();

        response.BoxGachaDB = BoxGachaDB(play);
        response.BoxGachaGroupIdByCount = RemainingBoxGachaGroups(play);

        return response;
    }

    private static EventContentBoxGachaDB BoxGachaDB(EventContentPlayDBServer play) => new()
    {
        EventContentId = play.EventContentId,
        Seed = play.BoxGachaSeed,
        Round = play.BoxGachaRound,
        PurchaseCount = play.BoxGachaPurchaseCount
    };

    // GroupId -> how many of that group's elements are still in the box. The client sums the values for its "N left" counter and treats an empty dictionary as a drained box, so a group that has run out has to disappear rather than sit at zero.
    private Dictionary<long, long> RemainingBoxGachaGroups(EventContentPlayDBServer play)
    {
        var drawnIds = play.BoxGachaDrawnElementIds.ToHashSet();
        return _excelService.GetTable<EventContentBoxGachaElementExcelT>()
            .Where(x => x.EventContentId == play.EventContentId && x.Round == play.BoxGachaRound && !drawnIds.Contains(x.Id))
            .GroupBy(x => x.GroupId)
            .ToDictionary(g => g.Key, g => (long)g.Count());
    }

    [ProtocolHandler(Protocol.EventContent_CardShopList)]
    public async Task<EventContentCardShopListResponse> CardShopList(
        SchaleDataContext db,
        EventContentCardShopListRequest request,
        EventContentCardShopListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        if (play.CardShopElements.Count == 0)
        {
            play.CardShopElements = RollCardShop(request.EventContentId);
            await db.SaveChangesAsync();
        }

        response.CardShopElementDBs = play.CardShopElements;
        response.RewardHistory = play.CardShopRewardHistory;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_CardShopShuffle)]
    public async Task<EventContentCardShopShuffleResponse> CardShopShuffle(
        SchaleDataContext db,
        EventContentCardShopShuffleRequest request,
        EventContentCardShopShuffleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);

        // Shuffling costs nothing - the response carries no parcel result - so the board has to be spent before a new one is dealt, otherwise you reshuffle until the SSR slot lands where you want it.
        if (play.CardShopElements.Count > 0 && play.CardShopElements.Any(x => !x.SoldOut))
            throw new WebAPIException(WebAPIErrorCode.EventContentCardShopCannotShuffle, $"Event {request.EventContentId} still has unflipped cards");

        play.CardShopElements = RollCardShop(request.EventContentId);
        play.CardShopRewardHistory = [];

        await db.SaveChangesAsync();

        response.CardShopElementDBs = play.CardShopElements;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_CardShopPurchase)]
    public async Task<EventContentCardShopPurchaseResponse> CardShopPurchase(
        SchaleDataContext db,
        EventContentCardShopPurchaseRequest request,
        EventContentCardShopPurchaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        if (play.CardShopElements.Count == 0)
            play.CardShopElements = RollCardShop(request.EventContentId);

        var slot = play.CardShopElements.FirstOrDefault(x => x.SlotNumber == request.SlotNumber)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentElementDoesNotExist, $"Event {request.EventContentId} has no card slot {request.SlotNumber}");

        if (slot.SoldOut)
            throw new WebAPIException(WebAPIErrorCode.EventContentElementAlreadyPurchased, $"Event {request.EventContentId} card slot {request.SlotNumber} is already flipped");

        var resolver = await FlipCards(db, account, play, [slot]);
        response.ParcelResultDB = resolver.ParcelResult;

        await db.SaveChangesAsync();

        response.CardShopElementDB = slot;
        response.CardShopPurchaseHistoryDBs = CardShopHistory(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_CardShopPurchaseAll)]
    public async Task<EventContentCardShopPurchaseAllResponse> CardShopPurchaseAll(
        SchaleDataContext db,
        EventContentCardShopPurchaseAllRequest request,
        EventContentCardShopPurchaseAllResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        if (play.CardShopElements.Count == 0)
            play.CardShopElements = RollCardShop(request.EventContentId);

        var slots = play.CardShopElements.Where(x => !x.SoldOut).ToList();
        if (slots.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentElementAlreadyPurchased, $"Event {request.EventContentId} has no unflipped cards left");

        var resolver = await FlipCards(db, account, play, slots);
        response.ParcelResultDB = resolver.ParcelResult;

        await db.SaveChangesAsync();

        response.CardShopElementDBs = play.CardShopElements;
        response.CardShopPurchaseHistoryDBs = CardShopHistory(play);
        response.RewardHistory = play.CardShopRewardHistory;

        return response;
    }

    private async Task<ParcelResolver> FlipCards(SchaleDataContext db, AccountDBServer account, EventContentPlayDBServer play, List<CardShopElementDB> slots)
    {
        var cards = _excelService.GetTable<EventContentCardShopExcelT>()
            .Where(x => x.EventContentId == play.EventContentId)
            .ToDictionary(x => x.Id);

        var goodsTable = _excelService.GetTable<GoodsExcelT>();
        var consumeParcels = new List<ParcelResult>();
        var rewardParcels = new List<ParcelResult>();

        foreach (var slot in slots)
        {
            if (!cards.TryGetValue(slot.CardShopElementId, out var card))
                throw new WebAPIException(WebAPIErrorCode.EventContentElementDoesNotExist, $"Card shop element {slot.CardShopElementId} not found");

            var costGoods = goodsTable.FirstOrDefault(x => x.Id == card.CostGoodsId)
                ?? throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {card.CostGoodsId} not found");

            for (int i = 0; i < (costGoods.ConsumeParcelType?.Count ?? 0); i++)
                consumeParcels.Add(new ParcelResult(costGoods.ConsumeParcelType![i], costGoods.ConsumeParcelId![i], costGoods.ConsumeParcelAmount![i]));

            var rewards = new List<ParcelInfo>();
            var rewardCount = new[] { card.RewardParcelType?.Count ?? 0, card.RewardParcelId?.Count ?? 0, card.RewardParcelAmount?.Count ?? 0 }.Min();
            for (int i = 0; i < rewardCount; i++)
            {
                rewards.AddRange(ParcelInfo.CreateParcelInfo(card.RewardParcelType![i], card.RewardParcelId![i], card.RewardParcelAmount![i]));
                rewardParcels.Add(new ParcelResult(card.RewardParcelType![i], card.RewardParcelId![i], card.RewardParcelAmount![i]));
            }

            slot.SoldOut = true;
            play.CardShopRewardHistory[slot.SlotNumber] = rewards;
            play.CardShopPurchaseCounts[card.Rarity] = play.CardShopPurchaseCounts.GetValueOrDefault(card.Rarity) + 1;
        }

        ParcelResultDB? consumed = null;
        if (consumeParcels.Count > 0)
            consumed = (await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true)).ParcelResult;

        return await _parcelHandler.BuildParcel(db, account, rewardParcels, consumed);
    }

    private static List<CardShopPurchaseHistoryDB> CardShopHistory(EventContentPlayDBServer play)
        => play.CardShopPurchaseCounts
            .Select(x => new CardShopPurchaseHistoryDB { EventContentId = play.EventContentId, Rarity = x.Key, PurchaseCount = x.Value })
            .ToList();

    // One card per CardGroupId, so a board never deals the same group twice. The highest refresh group drops the low rarities entirely and is the pity board ConstEventCommon gates behind CardShopProbWeightCount draws
    // without a CardShopProbWeightRarity card - this data version sets that count to 99999999, so it is unreachable and the board always comes off one of the ordinary groups.
    private List<CardShopElementDB> RollCardShop(long eventContentId)
    {
        var rows = _excelService.GetTable<EventContentCardShopExcelT>().Where(x => x.EventContentId == eventContentId).ToList();
        if (rows.Count == 0)
            return [];

        var refreshGroups = rows.Select(x => x.RefreshGroup).Distinct().OrderBy(x => x).ToList();
        var refreshGroup = refreshGroups[Random.Shared.Next(Math.Max(1, refreshGroups.Count - 1))];

        var elements = new List<CardShopElementDB>();
        foreach (var cardGroup in rows.Where(x => x.RefreshGroup == refreshGroup).GroupBy(x => x.CardGroupId).OrderBy(g => g.Key))
        {
            var candidates = cardGroup.ToList();
            var roll = Random.Shared.Next(Math.Max(1, candidates.Sum(x => x.Prob)));
            var pick = candidates[^1];
            foreach (var candidate in candidates)
            {
                roll -= candidate.Prob;
                if (roll < 0)
                {
                    pick = candidate;
                    break;
                }
            }

            elements.Add(new CardShopElementDB
            {
                EventContentId = eventContentId,
                SlotNumber = elements.Count,
                CardShopElementId = pick.Id,
                SoldOut = false
            });
        }

        return elements;
    }

    [ProtocolHandler(Protocol.EventContent_FortuneGachaPurchase)]
    public async Task<EventContentFortuneGachaPurchaseResponse> FortuneGachaPurchase(
        SchaleDataContext db,
        EventContentFortuneGachaPurchaseRequest request,
        EventContentFortuneGachaPurchaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);

        var shop = _excelService.GetTable<EventContentFortuneGachaShopExcelT>()
            .Where(x => x.EventContentId == request.EventContentId)
            .ToList();
        if (shop.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentElementDoesNotExist, $"Event {request.EventContentId} has no fortune gacha");

        var modify = _excelService.GetTable<EventContentFortuneGachaModifyExcelT>()
            .Where(x => x.EventContentId == request.EventContentId)
            .ToList();
        var startCount = modify.Count > 0 ? modify.Min(x => x.ProbModifyStartCount) : int.MaxValue;
        var steps = Math.Max(0, play.FortuneGachaStackCount - startCount + 1);

        var weights = shop.ToDictionary(x => x.Id, x =>
        {
            var weight = x.Prob + x.ProbModifyValue * steps;
            // ProbModifyLimit is the far end of the ramp, so it floors the rows being drained and caps the row being fed
            return x.ProbModifyValue < 0 ? Math.Max(weight, x.ProbModifyLimit) : Math.Min(weight, x.ProbModifyLimit == 0 ? weight : x.ProbModifyLimit);
        });

        var total = weights.Values.Sum();
        var roll = Random.Shared.Next(Math.Max(1, total));
        var drawn = shop[^1];
        foreach (var candidate in shop)
        {
            roll -= weights[candidate.Id];
            if (roll < 0)
            {
                drawn = candidate;
                break;
            }
        }

        var goodsTable = _excelService.GetTable<GoodsExcelT>();
        var costGoods = goodsTable.FirstOrDefault(x => x.Id == drawn.CostGoodsId)
            ?? throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {drawn.CostGoodsId} not found");

        var consumeParcels = new List<ParcelResult>();
        for (int i = 0; i < (costGoods.ConsumeParcelType?.Count ?? 0); i++)
            consumeParcels.Add(new ParcelResult(costGoods.ConsumeParcelType![i], costGoods.ConsumeParcelId![i], costGoods.ConsumeParcelAmount![i]));

        var rewardParcels = new List<ParcelResult>();
        var rewardCount = new[] { drawn.RewardParcelType?.Count ?? 0, drawn.RewardParcelId?.Count ?? 0, drawn.RewardParcelAmount?.Count ?? 0 }.Min();
        for (int i = 0; i < rewardCount; i++)
            rewardParcels.Add(new ParcelResult(drawn.RewardParcelType![i], drawn.RewardParcelId![i], drawn.RewardParcelAmount![i]));

        if (modify.Any(x => x.TargetGrade == drawn.Grade))
            play.FortuneGachaStackCount = 0;
        else
            play.FortuneGachaStackCount++;

        ParcelResultDB? consumed = null;
        if (consumeParcels.Count > 0)
            consumed = (await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true)).ParcelResult;

        var resolver = await _parcelHandler.BuildParcel(db, account, rewardParcels, consumed);
        response.ParcelResultDB = resolver.ParcelResult;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_FortuneGachaCount);
        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_FortuneGachaCountByGrade, 1, drawn.Grade);

        await db.SaveChangesAsync();

        response.FortuneGachaShopUniqueId = drawn.Id;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ShopRefresh)]
    public async Task<EventContentShopRefreshResponse> ShopRefresh(
        SchaleDataContext db,
        EventContentShopRefreshRequest request,
        EventContentShopRefreshResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        play.RefreshShopIds = RollRefreshShop(request.EventContentId, request.ShopCategoryType);
        play.RefreshShopUseCount++;
        play.RefreshShopUpdateTime = account.GameSettings.ServerDateTime();

        await db.SaveChangesAsync();

        response.ShopInfoDB = RefreshShopInfo(play, request.ShopCategoryType);
        response.ParcelResultDB = new ParcelResultDB
        {
            AccountDB = account.ToMap(_mapper),
            AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
        };

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ShopBuyRefreshMerchandise)]
    public async Task<EventContentShopBuyRefreshMerchandiseResponse> ShopBuyRefreshMerchandise(
        SchaleDataContext db,
        EventContentShopBuyRefreshMerchandiseRequest request,
        EventContentShopBuyRefreshMerchandiseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        var wanted = request.ShopUniqueIds ?? [];

        var rows = _excelService.GetTable<EventContentShopRefreshExcelT>()
            .Where(x => x.EventContentId == request.EventContentId && wanted.Contains(x.Id) && play.RefreshShopIds.Contains(x.Id))
            .ToList();

        if (rows.Count != wanted.Count)
            throw new WebAPIException(WebAPIErrorCode.EventContentElementDoesNotExist, $"Event {request.EventContentId} refresh shop does not currently stock all of {string.Join(",", wanted)}");

        var goodsTable = _excelService.GetTable<GoodsExcelT>();
        var consumeParcels = new List<ParcelResult>();
        var rewardParcels = new List<ParcelResult>();
        var products = new List<ShopProductDB>();

        foreach (var row in rows)
        {
            var goods = goodsTable.FirstOrDefault(x => x.Id == row.GoodsId)
                ?? throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {row.GoodsId} not found");

            for (int i = 0; i < (goods.ConsumeParcelType?.Count ?? 0); i++)
                consumeParcels.Add(new ParcelResult(goods.ConsumeParcelType![i], goods.ConsumeParcelId![i], goods.ConsumeParcelAmount![i]));

            for (int i = 0; i < (goods.ParcelType?.Count ?? 0); i++)
                rewardParcels.Add(new ParcelResult(goods.ParcelType![i], goods.ParcelId![i], goods.ParcelAmount![i]));

            play.RefreshShopIds.Remove(row.Id);

            products.Add(new ShopProductDB
            {
                EventContentId = request.EventContentId,
                ShopExcelId = row.Id,
                Category = row.CategoryType,
                DisplayOrder = row.DisplayOrder,
                PurchaseCount = 1,
                SoldOut = true,
                PurchaseCountLimit = 1
            });
        }

        ParcelResultDB? consumed = null;
        if (consumeParcels.Count > 0)
            consumed = (await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true)).ParcelResult;

        var resolver = await _parcelHandler.BuildParcel(db, account, rewardParcels, consumed);
        response.ParcelResultDB = resolver.ParcelResult;
        response.ConsumeResultDB = new ConsumeResultDB();
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper);

        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.PurchaseSpecificItemCount);

        await db.SaveChangesAsync();

        response.ShopProductDB = products;

        return response;
    }

    private List<long> RollRefreshShop(long eventContentId, ShopCategoryType category)
    {
        var rows = _excelService.GetTable<EventContentShopRefreshExcelT>()
            .Where(x => x.EventContentId == eventContentId && x.CategoryType == category)
            .ToList();

        var picked = new List<long>();
        foreach (var group in rows.GroupBy(x => x.RefreshGroup).OrderBy(g => g.Key))
        {
            var candidates = group.ToList();
            var roll = Random.Shared.Next(Math.Max(1, candidates.Sum(x => x.Prob)));
            var pick = candidates[^1];
            foreach (var candidate in candidates)
            {
                roll -= candidate.Prob;
                if (roll < 0)
                {
                    pick = candidate;
                    break;
                }
            }

            picked.Add(pick.Id);
        }

        return picked;
    }

    private ShopInfoDB RefreshShopInfo(EventContentPlayDBServer play, ShopCategoryType category)
    {
        var rows = _excelService.GetTable<EventContentShopRefreshExcelT>()
            .Where(x => x.EventContentId == play.EventContentId && play.RefreshShopIds.Contains(x.Id))
            .ToList();

        return new ShopInfoDB
        {
            EventContentId = play.EventContentId,
            Category = category,
            ManualRefreshCount = play.RefreshShopUseCount,
            IsRefresh = true,
            LastAutoRefreshDate = play.RefreshShopUpdateTime,
            ShopProductList = rows.Select(x => new ShopProductDB
            {
                EventContentId = play.EventContentId,
                ShopExcelId = x.Id,
                Category = x.CategoryType,
                DisplayOrder = x.DisplayOrder,
                PurchaseCount = 0,
                SoldOut = false,
                PurchaseCountLimit = 1
            }).ToList()
        };
    }

    [ProtocolHandler(Protocol.EventContent_SubEventLobby)]
    public async Task<EventContentSubEventLobbyResponse> SubEventLobby(
        SchaleDataContext db,
        EventContentSubEventLobbyRequest request,
        EventContentSubEventLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        await db.SaveChangesAsync();

        var limit = _excelService.GetTable<ConstEventCommonExcelT>().FirstOrDefault()?.SubEventChangeLimitSeconds ?? 0;
        var elapsed = account.GameSettings.ServerDateTime() - play.ChangeUpdateTime;

        response.EventContentChangeDB = new EventContentChangeDB
        {
            EventContentId = request.EventContentId,
            UseAmount = play.ChangeUseAmount,
            ChangeCount = play.ChangeCount,
            AccumulateChangeCount = play.AccumulateChangeCount,
            LastUpdateDate = play.ChangeUpdateTime,
            ChangeFlag = play.ChangeCount > 0
        };
        response.IsOnSubEvent = play.ChangeUpdateTime != default && elapsed.TotalSeconds < limit;

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
