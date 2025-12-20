using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
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
        
        // Populate BattlePassProductList
        var battlePasses = db.BattlePasses.Where(x => x.AccountServerId == account.ServerId && x.PurchaseGroupId != 0).ToList();
        var battlePassProductList = new List<BattlePassProductPurchaseDB>();

        if (battlePasses.Any())
        {
            var productExcels = _excelTableService.GetTable<ProductExcelT>();
            var shopCashExcels = _excelTableService.GetTable<ShopCashExcelT>();

            foreach (var bp in battlePasses)
            {
                // Aggressive Debugging for BattlePass Product
                Console.WriteLine($"[BillingHandler] Debugging BattlePassId: {bp.BattlePassId}. Total Products: {productExcels.Count}");

                // Targeted search for ProductBattlePass
                var battlePassProducts = productExcels.Where(x => x.ParcelType.Contains(ParcelType.ProductBattlePass)).ToList();
                Console.WriteLine($"[BillingHandler] Found {battlePassProducts.Count} products containing ProductBattlePass type.");
                
                foreach(var p in battlePassProducts)
                {
                     // Find the index of ProductBattlePass
                     var idx = p.ParcelType.IndexOf(ParcelType.ProductBattlePass);
                     var bpId = idx >= 0 && idx < p.ParcelId.Count ? p.ParcelId[idx] : -1;
                     Console.WriteLine($"[BillingHandler] BP Product: Id={p.Id}, ProductId={p.ProductId}, BP_ID={bpId}");
                }

                ProductExcelT product = null;
                
                // New Logic: Find product where the linked BattlePassId matches our current BP ID
                product = battlePassProducts.FirstOrDefault(x => {
                    var idx = x.ParcelType.IndexOf(ParcelType.ProductBattlePass);
                    return idx >= 0 && idx < x.ParcelId.Count && x.ParcelId[idx] == bp.BattlePassId;
                });

                if (product != null)
                {
                    // Find shop cash entry for this product
                    var shopCash = shopCashExcels.FirstOrDefault(x => x.CashProductId == product.Id);
                    
                    if (shopCash != null)
                    {
                        battlePassProductList.Add(new BattlePassProductPurchaseDB
                        {
                            ProductId = shopCash.Id, // Billing response expects ShopCashId as ProductId
                            BattlePassId = bp.BattlePassId,
                            PurchaseBattlePassGroupId = bp.PurchaseGroupId
                        });
                        Console.WriteLine($"[BillingHandler] Successfully mapped BattlePassId {bp.BattlePassId} to ShopCashId {shopCash.Id} via ProductId {product.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"[BillingHandler] ShopCash not found for ProductId: {product.Id} (BattlePassId: {bp.BattlePassId})");
                    }
                }
                else
                {
                    Console.WriteLine($"[BillingHandler] CRITICAL: No product found for BattlePassId: {bp.BattlePassId} among {battlePassProducts.Count} candidates.");
                }
            }
        }
        else
        {
             Console.WriteLine($"[BillingHandler] No BattlePasses found for Account: {account.ServerId} with PurchaseGroupId != 0");
        }

        response.BattlePassProductList = battlePassProductList;
        response.BattlePassIdInMailList = [];
        response.IsTeenage = request.IsTeenage;

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
            PurchaseDate = DateTime.Now,
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
}
