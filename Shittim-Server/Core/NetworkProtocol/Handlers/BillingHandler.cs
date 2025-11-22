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
}
