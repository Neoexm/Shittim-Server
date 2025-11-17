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
    private readonly SessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ShopManager _shopManager;

    public ShopHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ShopManager shopManager) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _shopManager = shopManager;
    }

    [ProtocolHandler(Protocol.Shop_GachaRecruitList)]
    public async Task<ShopGachaRecruitListResponse> GachaRecruitList(
        SchaleDataContext db,
        ShopGachaRecruitListRequest request,
        ShopGachaRecruitListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ShopFreeRecruitHistoryDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Shop_BeforehandGachaGet)]
    public async Task<ShopBeforehandGachaGetResponse> BeforehandGachaGet(
        SchaleDataContext db,
        ShopBeforehandGachaGetRequest request,
        ShopBeforehandGachaGetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

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

    [ProtocolHandler(Protocol.Shop_BuyGacha3)]
    public async Task<ShopBuyGacha3Response> BuyGacha3(
        SchaleDataContext db,
        ShopBuyGacha3Request request,
        ShopBuyGacha3Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (accountCurrency, consumedItems, gachaAmount) = await _shopManager.ConsumeCurrency(db, account, request);
        response.GemBonusRemain = accountCurrency.CurrencyDict[CurrencyTypes.GemBonus];
        response.GemPaidRemain = accountCurrency.CurrencyDict[CurrencyTypes.GemPaid];
        response.ConsumedItems = consumedItems ?? [];

        var (itemDbList, gachaResults) = await _shopManager.CreateTenGacha(db, account, request, gachaAmount);
        response.GachaResults = gachaResults;
        response.AcquiredItems = itemDbList.ToMapList(_mapper);
        response.UpdateTime = account.GameSettings.ServerDateTime();

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
