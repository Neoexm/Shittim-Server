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
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ShopHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ShopManager _shopManager;
    private readonly MissionService _missionService;
    private readonly ParcelHandler _parcelHandler;

    public ShopHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ShopManager shopManager,
        MissionService missionService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _shopManager = shopManager;
        _missionService = missionService;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Shop_GachaRecruitList)]
    public async Task<ShopGachaRecruitListResponse> GachaRecruitList(
        SchaleDataContext db,
        ShopGachaRecruitListRequest request,
        ShopGachaRecruitListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var recruitHistories = db.GetAccountRecruitHistory(account.ServerId).ToList();
        response.ShopFreeRecruitHistoryDBs = recruitHistories
            .Select(h => new ShopFreeRecruitHistoryDB
            {
                UniqueId = h.UniqueId,
                RecruitCount = h.RecruitCount,
                LastUpdateDate = h.LastUpdateDate
            })
            .ToList();

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BeforehandGachaGet)]
    public async Task<ShopBeforehandGachaGetResponse> BeforehandGachaGet(
        SchaleDataContext db,
        ShopBeforehandGachaGetRequest request,
        ShopBeforehandGachaGetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Official reports AlreadyPicked: true once the account has committed its beforehand
        // pick; that state must survive the session, hence the history row.
        var history = db.BeforehandGachaHistories.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        response.AlreadyPicked = history?.Picked ?? false;

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BeforehandGachaRun)]
    public async Task<ShopBeforehandGachaRunResponse> BeforehandGachaRun(
        SchaleDataContext db,
        ShopBeforehandGachaRunRequest request,
        ShopBeforehandGachaRunResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var gachaResults = new List<long> { 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000, 10000 };

        // Remember the roll so Save/Pick/Get agree across sessions. (No official capture covers
        // Run/Save/Pick payloads; only Get's AlreadyPicked is verified.)
        var history = db.BeforehandGachaHistories.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (history == null)
        {
            history = new BeforehandGachaHistoryDBServer { AccountServerId = account.ServerId };
            db.BeforehandGachaHistories.Add(history);
        }
        history.ShopUniqueId = request.ShopUniqueId;
        history.GoodsId = request.GoodsId;
        history.SavedResults = gachaResults;
        await db.SaveChangesAsync();

        response.SelectGachaSnapshot = new BeforehandGachaSnapshotDB
        {
            ShopUniqueId = request.ShopUniqueId,
            GoodsId = request.GoodsId,
            LastResults = gachaResults
        };

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BeforehandGachaSave)]
    public async Task<ShopBeforehandGachaSaveResponse> BeforehandGachaSave(
        SchaleDataContext db,
        ShopBeforehandGachaSaveRequest request,
        ShopBeforehandGachaSaveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.SelectGachaSnapshot = new BeforehandGachaSnapshotDB
        {
            ShopUniqueId = 0,
            GoodsId = 1,
            LastResults = []
        };

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BeforehandGachaPick)]
    public async Task<ShopBeforehandGachaPickResponse> BeforehandGachaPick(
        SchaleDataContext db,
        ShopBeforehandGachaPickRequest request,
        ShopBeforehandGachaPickResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // The pick is the committing step: from here on Shop_BeforehandGachaGet must report
        // AlreadyPicked (official sends true on an account that picked long ago).
        var history = db.BeforehandGachaHistories.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (history == null)
        {
            history = new BeforehandGachaHistoryDBServer
            {
                AccountServerId = account.ServerId,
                ShopUniqueId = request.ShopUniqueId,
                GoodsId = request.GoodsId
            };
            db.BeforehandGachaHistories.Add(history);
        }
        history.Picked = true;
        await db.SaveChangesAsync();

        var gachaResults = new List<GachaResult>
        {
            new GachaResult
            {
                Character = new CharacterDB
                {
                    ServerId = account.ServerId,
                    UniqueId = 10000,
                    StarGrade = 3,
                    Level = 1,
                    FavorRank = 1,
                    PublicSkillLevel = 1,
                    ExSkillLevel = 1,
                    PassiveSkillLevel = 1,
                    ExtraPassiveSkillLevel = 1,
                    LeaderSkillLevel = 1
                }
            }
        };

        response.GachaResults = gachaResults;

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BuyGacha)]
    public async Task<ShopBuyGachaResponse> BuyGacha(
        SchaleDataContext db,
        ShopBuyGachaRequest request,
        ShopBuyGachaResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        
        // Gacha1 carries no Cost; ConsumeCurrency resolves it from the shop row when null.
        var req3 = new ShopBuyGacha3Request
        { 
            ShopUniqueId = request.ShopUniqueId, 
            GoodsId = request.GoodsId,
            Cost = null 
        };

        var (accountCurrency, _, gachaAmount) = await _shopManager.ConsumeCurrency(db, account, req3);
        var (itemDbList, gachaResults) = await _shopManager.CreateTenGacha(db, account, req3, gachaAmount);

        response.AccountCurrencyDB = accountCurrency;
        response.ParcelResultDB = new ParcelResultDB
        {
             AccountCurrencyDB = accountCurrency,
        };

        await db.SaveChangesAsync();
        return response;
    }

    [ProtocolHandler(Protocol.Shop_BuyGacha2)]
    public async Task<ShopBuyGacha2Response> BuyGacha2(
        SchaleDataContext db,
        ShopBuyGacha2Request request,
        ShopBuyGacha2Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var req3 = new ShopBuyGacha3Request 
        { 
            ShopUniqueId = request.ShopUniqueId, 
            GoodsId = request.GoodsId,
            Cost = null // Cost null is now safe
        };

        var (accountCurrency, _, gachaAmount) = await _shopManager.ConsumeCurrency(db, account, req3);
        response.GemBonusRemain = accountCurrency.CurrencyDict.GetValueOrDefault(CurrencyTypes.GemBonus);
        response.GemPaidRemain = accountCurrency.CurrencyDict.GetValueOrDefault(CurrencyTypes.GemPaid);

        var (itemDbList, gachaResults) = await _shopManager.CreateTenGacha(db, account, req3, gachaAmount);
        response.GachaResults = gachaResults;
        response.AcquiredItems = itemDbList.ToMapList(_mapper);
        response.UpdateTime = account.GameSettings.ServerDateTime();

        // Update history (same as Gacha3)
        var recruitHistory = db.GetAccountRecruitHistory(account.ServerId)
            .FirstOrDefault(x => x.UniqueId == request.ShopUniqueId);
        
        if (recruitHistory == null)
        {
            recruitHistory = new ShopFreeRecruitHistoryDBServer
            {
                AccountServerId = account.ServerId,
                UniqueId = request.ShopUniqueId,
                RecruitCount = (int)gachaAmount,
                LastUpdateDate = DateTime.UtcNow
            };
            db.ShopFreeRecruitHistories.Add(recruitHistory);
        }
        else
        {
            recruitHistory.RecruitCount += (int)gachaAmount;
            recruitHistory.LastUpdateDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return response;


    }

    [ProtocolHandler(Protocol.Shop_BuyGacha3)]
    public async Task<ShopBuyGacha3Response> BuyGacha3(
        SchaleDataContext db,
        ShopBuyGacha3Request request,
        ShopBuyGacha3Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (accountCurrency, _, gachaAmount) = await _shopManager.ConsumeCurrency(db, account, request);
        response.GemBonusRemain = accountCurrency.CurrencyDict.GetValueOrDefault(CurrencyTypes.GemBonus);
        response.GemPaidRemain = accountCurrency.CurrencyDict.GetValueOrDefault(CurrencyTypes.GemPaid);

        var (itemDbList, gachaResults) = await _shopManager.CreateTenGacha(db, account, request, gachaAmount);
        response.GachaResults = gachaResults;
        response.AcquiredItems = itemDbList.ToMapList(_mapper);
        response.UpdateTime = account.GameSettings.ServerDateTime();

        var recruitHistory = db.GetAccountRecruitHistory(account.ServerId)
            .FirstOrDefault(x => x.UniqueId == request.ShopUniqueId);
        
        if (recruitHistory == null)
        {
            recruitHistory = new ShopFreeRecruitHistoryDBServer
            {
                AccountServerId = account.ServerId,
                UniqueId = request.ShopUniqueId,
                RecruitCount = (int)gachaAmount,
                LastUpdateDate = DateTime.UtcNow
            };
            db.ShopFreeRecruitHistories.Add(recruitHistory);
        }
        else
        {
            recruitHistory.RecruitCount += (int)gachaAmount;
            recruitHistory.LastUpdateDate = DateTime.UtcNow;
        }

        response.PickupFirstGetHistoryDBs = [];

        var updatedMissions = _missionService.UpdateMissionProgress(
            db, 
            account, 
            MissionCompleteConditionType.Reset_GachaCount, 
            gachaAmount);

        response.MissionProgressDBs = updatedMissions.Count > 0 ? updatedMissions : null;
        response.ServerTimeTicks = account.GameSettings.ServerDateTimeTicks();

        await db.SaveChangesAsync();

        return response;
    }



    [ProtocolHandler(Protocol.Shop_PickupSelectionGachaGet)]
    public async Task<ShopPickupSelectionGachaGetResponse> PickupSelectionGachaGet(
        SchaleDataContext db,
        ShopPickupSelectionGachaGetRequest request,
        ShopPickupSelectionGachaGetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        
        // Pickup selections are not persisted; the client accepts an empty map.
        response.PickupCharacterSelection = new Dictionary<long, long>();

        return response;
    }

    [ProtocolHandler(Protocol.Shop_PickupSelectionGachaSet)]
    public async Task<ShopPickupSelectionGachaSetResponse> PickupSelectionGachaSet(
        SchaleDataContext db,
        ShopPickupSelectionGachaSetRequest request,
        ShopPickupSelectionGachaSetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        // Just acknowledge
        return response;
    }

    [ProtocolHandler(Protocol.Shop_PickupSelectionGachaBuy)]
    public async Task<ShopPickupSelectionGachaBuyResponse> PickupSelectionGachaBuy(
        SchaleDataContext db,
        ShopPickupSelectionGachaBuyRequest request,
        ShopPickupSelectionGachaBuyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Map to Gacha3 request to reuse logic
        var req3 = new ShopBuyGacha3Request
        {
            ShopUniqueId = request.ShopUniqueId,
            GoodsId = request.GoodsId,
            FreeRecruitId = request.FreeRecruitId,
            Cost = request.Cost
        };

        var (accountCurrency, _, gachaAmount) = await _shopManager.ConsumeCurrency(db, account, req3);
        response.GemBonusRemain = accountCurrency.CurrencyDict.GetValueOrDefault(CurrencyTypes.GemBonus);
        response.GemPaidRemain = accountCurrency.CurrencyDict.GetValueOrDefault(CurrencyTypes.GemPaid);

        var (itemDbList, gachaResults) = await _shopManager.CreateTenGacha(db, account, req3, gachaAmount);
        response.GachaResults = gachaResults;
        response.AcquiredItems = itemDbList.ToMapList(_mapper);
        response.UpdateTime = account.GameSettings.ServerDateTime();

        // Update history
        var recruitHistory = db.GetAccountRecruitHistory(account.ServerId)
            .FirstOrDefault(x => x.UniqueId == request.ShopUniqueId);

        if (recruitHistory == null)
        {
            recruitHistory = new ShopFreeRecruitHistoryDBServer
            {
                AccountServerId = account.ServerId,
                UniqueId = request.ShopUniqueId,
                RecruitCount = (int)gachaAmount,
                LastUpdateDate = DateTime.UtcNow
            };
            db.ShopFreeRecruitHistories.Add(recruitHistory);
        }
        else
        {
            recruitHistory.RecruitCount += (int)gachaAmount;
            recruitHistory.LastUpdateDate = DateTime.UtcNow;
        }
        

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Shop_List)]
    public async Task<ShopListResponse> List(
        SchaleDataContext db,
        ShopListRequest request,
        ShopListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var shopExcels = _excelService.GetTable<ShopExcelT>();
        response.ShopInfos = await _shopManager.GetShopList(db, account, shopExcels, request.CategoryList);
        response.ShopEligmaHistoryDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BuyMerchandise)]
    public async Task<ShopBuyMerchandiseResponse> BuyMerchandise(
        SchaleDataContext db,
        ShopBuyMerchandiseRequest request,
        ShopBuyMerchandiseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        ValidatePurchaseCount(request.PurchaseCount);

        var shopExcel = _excelService.GetTable<ShopExcelT>().FirstOrDefault(x => x.Id == request.ShopUniqueId);
        if (shopExcel == null)
            throw new WebAPIException(WebAPIErrorCode.ShopExcelNotFound, $"Shop {request.ShopUniqueId} not found");

        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == request.GoodsId);
        if (goodsExcel == null)
            throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {request.GoodsId} not found");

        var purchaseHistory = await ShopManager.EnsurePurchasable(
            db, account, request.ShopUniqueId, shopExcel, request.PurchaseCount);

        var consumeParcelTypes = goodsExcel.ConsumeParcelType ?? [];
        var consumeParcelIds = goodsExcel.ConsumeParcelId ?? [];
        var consumeParcelAmounts = goodsExcel.ConsumeParcelAmount ?? [];

        var rewardParcelTypes = goodsExcel.ParcelType ?? [];
        var rewardParcelIds = goodsExcel.ParcelId ?? [];
        var rewardParcelAmounts = goodsExcel.ParcelAmount ?? [];

        var consumeParcels = new List<ParcelInfo>();
        for (int i = 0; i < consumeParcelTypes.Count; i++)
        {
            consumeParcels.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = consumeParcelTypes[i], Id = consumeParcelIds[i] },
                Amount = consumeParcelAmounts[i] * request.PurchaseCount
            });
        }

        var rewardParcels = new List<ParcelInfo>();
        for (int i = 0; i < rewardParcelTypes.Count; i++)
        {
            rewardParcels.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = rewardParcelTypes[i], Id = rewardParcelIds[i] },
                Amount = rewardParcelAmounts[i] * request.PurchaseCount
            });
        }

        if (consumeParcels.Count > 0)
        {
            await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true);
        }

        var parcelResultDB = new ParcelResultDB();
        if (rewardParcels.Count > 0)
        {
            await _parcelHandler.BuildParcel(db, account, rewardParcels, parcelResultDB);
            parcelResultDB.AcquiredItems = rewardParcels;
            response.ParcelResultDB = parcelResultDB;
        }

        var accountCurrency = db.GetAccountCurrencies(account.ServerId).FirstOrDefault();
        if (accountCurrency != null)
        {
            response.AccountCurrencyDB = accountCurrency.ToMap(_mapper);
        }

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
            // No Price - official omits it (see ShopManager.GetShopList).
        };

        await db.SaveChangesAsync();

        return response;
    }

    // PurchaseCount arrives from the client and multiplies both the consume and the reward. A
    // non-positive count inverts the consume into a grant (a -30-gem cost is credited, not
    // debited), and a huge one overflows the int64 multiply back into negative territory, so the
    // bound has to be two-sided. The ceiling is far above any real shop's PurchaseCountLimit;
    // per-shop limits are enforced separately in ShopManager.EnsurePurchasable.
    internal const long MaxPurchaseCountPerRequest = 10_000;

    internal static void ValidatePurchaseCount(long purchaseCount)
    {
        if (purchaseCount is <= 0 or > MaxPurchaseCountPerRequest)
            throw new WebAPIException(WebAPIErrorCode.ShopInvalidCostOrReward,
                $"Invalid purchase count {purchaseCount}");
    }

    [ProtocolHandler(Protocol.Shop_BuyRefreshMerchandise)]
    public async Task<ShopBuyRefreshMerchandiseResponse> BuyRefreshMerchandise(
        SchaleDataContext db,
        ShopBuyRefreshMerchandiseRequest request,
        ShopBuyRefreshMerchandiseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var refreshExcels = _excelService.GetTable<ShopRefreshExcelT>();
        var goodsExcels = _excelService.GetTable<GoodsExcelT>();

        var consumeParcels = new List<ParcelInfo>();
        var rewardParcels = new List<ParcelInfo>();
        var purchasedProducts = new List<ShopProductDB>();

        foreach (var shopUniqueId in request.ShopUniqueIds ?? [])
        {
            var refreshExcel = refreshExcels.FirstOrDefault(x => x.Id == shopUniqueId);
            if (refreshExcel == null)
                continue;

            var goodsExcel = goodsExcels.FirstOrDefault(x => x.Id == refreshExcel.GoodsId);
            if (goodsExcel == null)
                continue;

            var consumeParcelTypes = goodsExcel.ConsumeParcelType ?? [];
            var consumeParcelIds = goodsExcel.ConsumeParcelId ?? [];
            var consumeParcelAmounts = goodsExcel.ConsumeParcelAmount ?? [];

            for (int i = 0; i < consumeParcelTypes.Count; i++)
            {
                consumeParcels.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair
                    {
                        Type = consumeParcelTypes[i],
                        Id = i < consumeParcelIds.Count ? consumeParcelIds[i] : 0
                    },
                    Amount = i < consumeParcelAmounts.Count ? consumeParcelAmounts[i] : 0
                });
            }

            var rewardParcelTypes = goodsExcel.ParcelType ?? [];
            var rewardParcelIds = goodsExcel.ParcelId ?? [];
            var rewardParcelAmounts = goodsExcel.ParcelAmount ?? [];

            for (int i = 0; i < rewardParcelTypes.Count; i++)
            {
                rewardParcels.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair
                    {
                        Type = rewardParcelTypes[i],
                        Id = i < rewardParcelIds.Count ? rewardParcelIds[i] : 0
                    },
                    Amount = i < rewardParcelAmounts.Count ? rewardParcelAmounts[i] : 0
                });
            }

            purchasedProducts.Add(new ShopProductDB
            {
                ShopExcelId = refreshExcel.Id,
                Category = refreshExcel.CategoryType,
                DisplayOrder = refreshExcel.DisplayOrder,
                PurchaseCount = 0,
                SoldOut = false,
                PurchaseCountLimit = refreshExcel.PurchaseCountLimit,
                Price = consumeParcelAmounts.FirstOrDefault()
            });
        }

        if (consumeParcels.Count > 0)
            await _parcelHandler.BuildParcel(db, account, consumeParcels, isConsume: true);

        if (rewardParcels.Count > 0)
        {
            var parcelResult = new ParcelResultDB();
            await _parcelHandler.BuildParcel(db, account, rewardParcels, parcelResult);
            parcelResult.AcquiredItems = rewardParcels;
            response.ParcelResultDB = parcelResult;
        }

        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper);
        response.ConsumeResultDB = new ConsumeResultDB();
        response.ShopProductDB = purchasedProducts;

        if (purchasedProducts.Count > 0)
        {
            var missions = _missionService.UpdateMissionProgress(
                db,
                account,
                MissionCompleteConditionType.Reset_BuyShopGoods,
                purchasedProducts.Count);

            if (missions.Count > 0)
                response.MissionProgressDBs = missions;
        }

        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BuyEligma)]
    public async Task<ShopBuyEligmaResponse> BuyEligma(
        SchaleDataContext db,
        ShopBuyEligmaRequest request,
        ShopBuyEligmaResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Shop_Refresh)]
    public async Task<ShopRefreshResponse> Refresh(
        SchaleDataContext db,
        ShopRefreshRequest request,
        ShopRefreshResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BuyAP)]
    public async Task<ShopBuyAPResponse> BuyAP(
        SchaleDataContext db,
        ShopBuyAPRequest request,
        ShopBuyAPResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        ValidatePurchaseCount(request.PurchaseCount);

        // Failures are Error packets, never empty successes - official rejects with a bare
        // {Protocol:-1, ErrorCode, ServerTimeTicks} envelope (captured on the AP-cap rejection).
        var shopExcel = _excelService.GetTable<ShopExcelT>().FirstOrDefault(x => x.Id == request.ShopUniqueId);
        if (shopExcel == null || shopExcel.GoodsId == null || shopExcel.GoodsId.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.ShopExcelNotFound, $"Shop {request.ShopUniqueId} not found");

        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == shopExcel.GoodsId[0]);
        if (goodsExcel == null)
            throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods for shop {request.ShopUniqueId} not found");

        var costParcelType = goodsExcel.ConsumeParcelType != null && goodsExcel.ConsumeParcelType.Count > 0 
            ? goodsExcel.ConsumeParcelType[0] 
            : ParcelType.Currency;
        var costParcelId = goodsExcel.ConsumeParcelId != null && goodsExcel.ConsumeParcelId.Count > 0 
            ? goodsExcel.ConsumeParcelId[0] 
            : (long)CurrencyTypes.Gem;
        var costAmount = (goodsExcel.ConsumeParcelAmount != null && goodsExcel.ConsumeParcelAmount.Count > 0 
            ? goodsExcel.ConsumeParcelAmount[0] 
            : 0) * request.PurchaseCount;

        var costCurrency = costParcelType == ParcelType.Currency ? (CurrencyTypes)costParcelId : CurrencyTypes.Gem;
        
        var rewardParcelType = goodsExcel.ParcelType != null && goodsExcel.ParcelType.Count > 0
            ? goodsExcel.ParcelType[0]
            : ParcelType.Currency;
        var rewardParcelId = goodsExcel.ParcelId != null && goodsExcel.ParcelId.Count > 0
            ? goodsExcel.ParcelId[0]
            : (long)CurrencyTypes.ActionPoint;
        var rewardPerCount = goodsExcel.ParcelAmount != null && goodsExcel.ParcelAmount.Count > 0
            ? goodsExcel.ParcelAmount[0]
            : 0;

        // Shop_BuyAP grants ActionPoint by definition - the goods rows for shop 21 all carry
        // Currency/5/120 now that the GoodsExcel model has its recovered 22-slot layout; the
        // captured official buy matches (x3: +360 AP, -90 gems). The 120 fallback only covers a
        // future data change that stops naming ActionPoint plausibly.
        const CurrencyTypes rewardCurrency = CurrencyTypes.ActionPoint;
        var rewardAmount = (rewardParcelType == ParcelType.Currency
            && rewardParcelId == (long)CurrencyTypes.ActionPoint
            && rewardPerCount is > 0 and <= 999
                ? rewardPerCount
                : 120) * request.PurchaseCount;
        
        // The AP shop is capped at 20 purchases per game day (PurchaseCountLimit in the excel,
        // confirmed on the wire); tracked per reset window so the counter rolls over at 04:00.
        var purchaseHistory = await ShopManager.EnsurePurchasable(
            db, account, request.ShopUniqueId, shopExcel, request.PurchaseCount);

        var accountCurrency = db.GetAccountCurrencies(account.ServerId).FirstOrDefault();
        if (accountCurrency == null)
            throw new WebAPIException(WebAPIErrorCode.ShopInvalidCostOrReward, "Account has no currency row");

        if (accountCurrency.CurrencyDict[costCurrency] < costAmount)
        {
            // Official's error code for an unaffordable purchase is unobserved; this is the
            // closest shop code. The envelope shape is what matters and matches.
            throw new WebAPIException(WebAPIErrorCode.ShopInvalidCostOrReward,
                $"Not enough {costCurrency} ({accountCurrency.CurrencyDict[costCurrency]} < {costAmount})");
        }

        // AP is capped at 999. Official refuses a purchase that would overflow the cap with
        // error 10006: the captured session bought at 170 AP (+360 -> 530, allowed) and was
        // rejected at 970 AP (+120 would pass 999).
        const long ActionPointCap = 999;
        if (rewardCurrency == CurrencyTypes.ActionPoint
            && accountCurrency.CurrencyDict[CurrencyTypes.ActionPoint] + rewardAmount > ActionPointCap)
        {
            throw new WebAPIException(WebAPIErrorCode.ShopCannotPurchaseActionPointLimitOver,
                $"AP {accountCurrency.CurrencyDict[CurrencyTypes.ActionPoint]} + {rewardAmount} would exceed {ActionPointCap}");
        }

        accountCurrency.CurrencyDict[costCurrency] -= costAmount;
        accountCurrency.UpdateTimeDict[costCurrency] = account.GameSettings.ServerDateTime();
        
        accountCurrency.CurrencyDict[rewardCurrency] += rewardAmount;
        accountCurrency.UpdateTimeDict[rewardCurrency] = account.GameSettings.ServerDateTime();

        purchaseHistory.PurchaseCount += request.PurchaseCount;

        await db.SaveChangesAsync();

        var currencyDb = accountCurrency.ToMap(_mapper);
        // Official's DisplaySequence parcel: {Key: {Type: 2, Id: 5}, Amount: <AP granted>, 1x, 1x}.
        var apParcel = new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = ParcelType.Currency, Id = (long)CurrencyTypes.ActionPoint },
            Amount = rewardAmount,
            Multiplier = Schale.MX.Core.Math.BasisPoint.One,
            Probability = Schale.MX.Core.Math.BasisPoint.One
        };

        response.AccountCurrencyDB = currencyDb;
        response.ConsumeResultDB = new ConsumeResultDB();
        // Official's successful purchase carries the granted parcel in ParcelResultDB
        // (AccountCurrencyDB + DisplaySequence + ParcelForMission), not an empty object.
        response.ParcelResultDB = new ParcelResultDB
        {
            AccountCurrencyDB = currencyDb,
            DisplaySequence = [apParcel],
            ParcelForMission = [apParcel]
        };
        // Official's product carries ProductType and the purchase count - and no Price key. The
        // count is the running total for the reset window: the captured official Shop_List omitted
        // it (0) immediately before a 3-unit buy whose response reported 3, so cumulative and
        // per-request coincide in that sample; cumulative is the reading that can enforce the
        // limit of 20, so that is what is tracked and reported.
        response.ShopProductDB = new ShopProductDB
        {
            ShopExcelId = request.ShopUniqueId,
            Category = shopExcel.CategoryType,
            DisplayOrder = shopExcel.DisplayOrder,
            PurchaseCount = purchaseHistory.PurchaseCount,
            PurchaseCountLimit = shopExcel.PurchaseCountLimit,
            ProductType = ShopProductType.General
        };

        var buyApMissions = _missionService.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Reset_ShopBuyActionPointCount, request.PurchaseCount);
        if (buyApMissions.Count > 0)
            response.MissionProgressDBs = buyApMissions;

        return response;
    }
}
