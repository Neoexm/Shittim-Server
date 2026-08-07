using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Serilog;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class BillingHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelTableService;
    private readonly ParcelHandler _parcelHandler;

    public BillingHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelTableService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelTableService = excelTableService;
        _parcelHandler = parcelHandler;
    }

    // A battle pass is sold through a ProductExcel row carrying a ProductBattlePass parcel; the parcel id
    // at that slot is the BattlePassId, and billing responses key the purchase by the ShopCash row's id.
    internal static List<BattlePassProductPurchaseDB> BuildBattlePassProductList(
        SchaleDataContext db, Schale.Data.GameModel.AccountDBServer account, ExcelTableService excelService)
    {
        var battlePasses = db.BattlePasses.Where(x => x.AccountServerId == account.ServerId && x.PurchaseGroupId != 0).ToList();
        var result = new List<BattlePassProductPurchaseDB>();
        if (battlePasses.Count == 0)
            return result;

        var productExcels = excelService.GetTable<ProductExcelT>();
        var shopCashExcels = excelService.GetTable<ShopCashExcelT>();
        var battlePassProducts = productExcels.Where(x => x.ParcelType.Contains(ParcelType.ProductBattlePass)).ToList();

        foreach (var bp in battlePasses)
        {
            var product = battlePassProducts.FirstOrDefault(x =>
            {
                var idx = x.ParcelType.IndexOf(ParcelType.ProductBattlePass);
                return idx >= 0 && idx < x.ParcelId.Count && x.ParcelId[idx] == bp.BattlePassId;
            });
            if (product == null)
                continue;

            var shopCash = shopCashExcels.FirstOrDefault(x => x.CashProductId == product.Id);
            if (shopCash == null)
                continue;

            result.Add(new BattlePassProductPurchaseDB
            {
                ProductId = shopCash.Id,
                BattlePassId = bp.BattlePassId,
                PurchaseBattlePassGroupId = bp.PurchaseGroupId
            });
        }

        return result;
    }

    [ProtocolHandler(Protocol.Billing_PurchaseListByNexon)]
    public async Task<BillingPurchaseListByNexonResponse> PurchaseListByNexon(
        SchaleDataContext db,
        BillingPurchaseListByNexonRequest request,
        BillingPurchaseListByNexonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.CountList = [];
        response.OrderList = [];
        response.MonthlyProductList = [];
        response.BlockedProductDBs = [];
        response.GachaTicketItemIdList = [];
        response.ProductMonthlyIdInMailList = [];
        // Official's standalone response carries this (as [] when empty) and has no IsTeenage key - that's a request-side field.
        response.DailyRecordIdInMailList = [];
        
        response.BattlePassProductList = BuildBattlePassProductList(db, account, _excelTableService);
        response.BattlePassIdInMailList = [];

        return response;
    }

    [ProtocolHandler(Protocol.Billing_PurchaseFreeProduct)]
    public async Task<BillingPurchaseFreeProductResponse> PurchaseFreeProduct(
        SchaleDataContext db,
        BillingPurchaseFreeProductRequest request,
        BillingPurchaseFreeProductResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var shopCashExcels = _excelTableService.GetTable<ShopCashExcelT>();
        var productExcels = _excelTableService.GetTable<ProductExcelT>();

        var shopCash = shopCashExcels.FirstOrDefault(x => x.Id == request.ShopCashId);
        if (shopCash == null)
        {
            response.PurchaseProduct = new();
            response.ParcelResult = new();
            return response;
        }

        var product = productExcels.FirstOrDefault(x => x.Id == shopCash.CashProductId);
        if (product == null)
        {
            response.PurchaseProduct = new();
            response.ParcelResult = new();
            return response;
        }

        var parcelTypes = product.ParcelType ?? [];
        var parcelIds = product.ParcelId ?? [];
        var parcelAmounts = product.ParcelAmount ?? [];

        var parcels = new List<ParcelInfo>();
        for (int i = 0; i < parcelTypes.Count; i++)
        {
            parcels.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = parcelTypes[i], Id = parcelIds[i] },
                Amount = parcelAmounts[i]
            });
        }

        var parcelResultDB = new ParcelResultDB();
        var parcelResult = await _parcelHandler.BuildParcel(db, account, parcels, parcelResultDB);

        response.ParcelResult = parcelResultDB;
        response.PurchaseProduct = new PurchaseCountDB
        {
            ShopCashId = request.ShopCashId,
            PurchaseCount = 1,
            ResetDate = DateTime.MinValue,
            PurchaseDate = account.GameSettings.ServerDateTime(),
            ManualResetDate = null
        };

        return response;
    }

    [ProtocolHandler(Protocol.Billing_CheckConditionCashShopGoods)]
    public async Task<BillingCheckConditionCashGoodsResponse> CheckConditionCashShopGoods(
        SchaleDataContext db,
        BillingCheckConditionCashGoodsRequest request,
        BillingCheckConditionCashGoodsResponse response)
    {
        // Always allow
        response.result = true;
        return response;
    }

    [ProtocolHandler(Protocol.Billing_PurchaseCashShopVerifyByNexon)]
    public async Task<BillingPurchaseCashShopVerifyByNexonResponse> PurchaseCashShopVerifyByNexon(
        SchaleDataContext db,
        BillingPurchaseCashShopVerifyByNexonRequest request,
        BillingPurchaseCashShopVerifyByNexonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        
        var shopCashExcels = _excelTableService.GetTable<ShopCashExcelT>();
        var productExcels = _excelTableService.GetTable<ProductExcelT>();

        var shopCash = shopCashExcels.FirstOrDefault(x => x.Id == request.ShopCashId);
        if (shopCash == null)
            return response;

        var product = productExcels.FirstOrDefault(x => x.Id == shopCash.CashProductId);
        if (product == null)
            return response;

        var parcelTypes = product.ParcelType ?? [];
        var parcelIds = product.ParcelId ?? [];
        var parcelAmounts = product.ParcelAmount ?? [];

        var parcels = new List<ParcelInfo>();
        for (int i = 0; i < parcelTypes.Count; i++)
        {
            parcels.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = parcelTypes[i], Id = parcelIds[i] },
                Amount = parcelAmounts[i]
            });
        }

        var parcelResultDB = new ParcelResultDB();
        var parcelResult = await _parcelHandler.BuildParcel(db, account, parcels, parcelResultDB);

        response.ParcelResult = parcelResultDB;
        response.PurchaseCount = 1;
        
        // Basic response fields to satisfy client
        response.shopId = request.ShopCashId.ToString();
        response.currency = request.CurrencyCode ?? "USD";
        response.itemPrice = request.CurrencyValue;

        // Update history skipped (no table)
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Billing_PurchaseListByYostar)]
    public async Task<BillingPurchaseListByYostarResponse> PurchaseListByYostar(
        SchaleDataContext db,
        BillingPurchaseListByYostarRequest request,
        BillingPurchaseListByYostarResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.CountList = [];
        response.OrderList = [];
        response.MonthlyProductList = [];
        response.BlockedProductDBs = [];
        response.BattlePassProductList = BuildBattlePassProductList(db, account, _excelTableService);

        return response;
    }

    [ProtocolHandler(Protocol.Billing_TransactionStartByYostar)]
    public async Task<BillingTransactionStartByYostarResponse> TransactionStartByYostar(
        SchaleDataContext db,
        BillingTransactionStartByYostarRequest request,
        BillingTransactionStartByYostarResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var shopCash = _excelTableService.GetTable<ShopCashExcelT>().FirstOrDefault(x => x.Id == request.ShopCashId)
            ?? throw new WebAPIException(WebAPIErrorCode.BillingStartShopCashIdNotFound,
                $"ShopCash {request.ShopCashId} not found");

        // Monotonic enough for one player; the ticks double as a record of when the order opened.
        var orderId = account.GameSettings.ServerDateTimeTicks();
        account.GameSettings.PendingPurchaseOrders[orderId] = shopCash.Id;
        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        response.PurchaseCount = 0;
        response.PurchaseResetDate = DateTime.MinValue;
        response.PurchaseOrderId = orderId;
        response.MXSeedKey = "";
        response.PurchaseServerTag = PurchaseServerTag.Production;
        response.PurchaseServerCallbackUrl = "";

        return response;
    }

    [ProtocolHandler(Protocol.Billing_TransactionEndByYostar)]
    public async Task<BillingTransactionEndByYostarResponse> TransactionEndByYostar(
        SchaleDataContext db,
        BillingTransactionEndByYostarRequest request,
        BillingTransactionEndByYostarResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (!account.GameSettings.PendingPurchaseOrders.TryGetValue(request.PurchaseOrderId, out var shopCashId))
            throw new WebAPIException(WebAPIErrorCode.PaymentNotFoundPurchase,
                $"Order {request.PurchaseOrderId} not found");

        account.GameSettings.PendingPurchaseOrders.Remove(request.PurchaseOrderId);
        db.Accounts.Update(account);

        response.CountList = [];
        response.MonthlyProductList = [];
        response.BattlePassProductList = [];

        if (request.EndType != BillingTransactionEndType.Success)
        {
            await db.SaveChangesAsync();
            return response;
        }

        var shopCash = _excelTableService.GetTable<ShopCashExcelT>().FirstOrDefault(x => x.Id == shopCashId)
            ?? throw new WebAPIException(WebAPIErrorCode.BillingEndShopCashIdNotFound,
                $"ShopCash {shopCashId} not found");
        var product = _excelTableService.GetTable<ProductExcelT>().FirstOrDefault(x => x.Id == shopCash.CashProductId)
            ?? throw new WebAPIException(WebAPIErrorCode.BillingEndShopCashIdNotFound,
                $"Product {shopCash.CashProductId} not found");

        var count = ShopHandler.AlignedColumnCount(
            product.ParcelType?.Count, product.ParcelId?.Count, product.ParcelAmount?.Count);
        var parcels = new List<ParcelInfo>();
        Schale.Data.GameModel.BattlePassDBServer? purchasedPass = null;
        for (int i = 0; i < count; i++)
        {
            if (product.ParcelType![i] == ParcelType.ProductBattlePass)
            {
                var passId = product.ParcelId![i];
                purchasedPass = db.BattlePasses.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.BattlePassId == passId);
                if (purchasedPass == null)
                {
                    purchasedPass = new Schale.Data.GameModel.BattlePassDBServer
                    {
                        AccountServerId = account.ServerId,
                        BattlePassId = passId,
                        PassLevel = 1,
                        LastWeeklyPassExpLimitRefreshDate = account.GameSettings.ServerDateTime()
                    };
                    db.BattlePasses.Add(purchasedPass);
                }
                // Readers only test PurchaseGroupId != 0 to unlock the paid track.
                purchasedPass.PurchaseGroupId = shopCash.Id;
                continue;
            }

            parcels.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = product.ParcelType[i], Id = product.ParcelId![i] },
                Amount = product.ParcelAmount![i]
            });
        }

        var parcelResultDB = new ParcelResultDB();
        if (parcels.Count > 0)
            await _parcelHandler.BuildParcel(db, account, parcels, parcelResultDB);

        await db.SaveChangesAsync();

        response.ParcelResult = parcelResultDB;
        response.PurchaseCount = 1;
        response.BattlePassInfo = purchasedPass;
        response.BattlePassProductList = BuildBattlePassProductList(db, account, _excelTableService);

        return response;
    }
}
