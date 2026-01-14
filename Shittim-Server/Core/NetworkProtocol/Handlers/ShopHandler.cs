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

        response.BeforehandGachaSnapshot = new BeforehandGachaSnapshotDB
        {
            ShopUniqueId = 1,
            GoodsId = 1,
            LastResults = []
        };

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
        
        // Wrap into Gacha3 request (Cost is null, which is handled by our previous fix)
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
             // Map other results if needed, but for now ensure we don't crash
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
        
        // Return empty selection for now
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
        response.ShopInfos = await _shopManager.GetShopList(account, shopExcels, request.CategoryList);
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

        var shopExcel = _excelService.GetTable<ShopExcelT>().FirstOrDefault(x => x.Id == request.ShopUniqueId);
        if (shopExcel == null) return response;

        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == request.GoodsId);
        if (goodsExcel == null) return response;

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

        response.ShopProductDB = new ShopProductDB
        {
            ShopExcelId = request.ShopUniqueId,
            Category = shopExcel.CategoryType,
            DisplayOrder = shopExcel.DisplayOrder,
            PurchaseCount = 0,
            SoldOut = false,
            PurchaseCountLimit = shopExcel.PurchaseCountLimit,
            Price = consumeParcelAmounts.FirstOrDefault()
        };

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
        
        var shopExcel = _excelService.GetTable<ShopExcelT>().FirstOrDefault(x => x.Id == request.ShopUniqueId);
        if (shopExcel == null || shopExcel.GoodsId == null || shopExcel.GoodsId.Count == 0) return response;

        var goodsExcel = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == shopExcel.GoodsId[0]);
        if (goodsExcel == null) return response;

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
        var rewardAmount = (goodsExcel.ParcelAmount != null && goodsExcel.ParcelAmount.Count > 0 
            ? goodsExcel.ParcelAmount[0] 
            : 0) * request.PurchaseCount;

        var rewardCurrency = rewardParcelType == ParcelType.Currency ? (CurrencyTypes)rewardParcelId : CurrencyTypes.ActionPoint;
        
        var accountCurrency = db.GetAccountCurrencies(account.ServerId).FirstOrDefault();
        if (accountCurrency == null) return response;
        
        if (accountCurrency.CurrencyDict[costCurrency] < costAmount)
        {
            return response;
        }

        accountCurrency.CurrencyDict[costCurrency] -= costAmount;
        accountCurrency.UpdateTimeDict[costCurrency] = DateTime.Now;
        
        accountCurrency.CurrencyDict[rewardCurrency] += rewardAmount;
        accountCurrency.UpdateTimeDict[rewardCurrency] = DateTime.Now;

        await db.SaveChangesAsync();

        response.AccountCurrencyDB = accountCurrency.ToMap(_mapper);
        response.ConsumeResultDB = new ConsumeResultDB();
        response.ParcelResultDB = new ParcelResultDB();
        response.ShopProductDB = new ShopProductDB
        {
            ShopExcelId = request.ShopUniqueId,
            Category = shopExcel.CategoryType,
            DisplayOrder = shopExcel.DisplayOrder,
            PurchaseCount = 0,
            SoldOut = false,
            PurchaseCountLimit = shopExcel.PurchaseCountLimit,
            Price = costAmount / request.PurchaseCount
        };

        return response;
    }
}
