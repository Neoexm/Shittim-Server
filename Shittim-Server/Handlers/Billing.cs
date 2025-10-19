using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Billing
    {
        public class PurchaseListByNexon : BaseHandler<BillingPurchaseListByNexonRequest, BillingPurchaseListByNexonResponse>
        {
            protected override async Task<BillingPurchaseListByNexonResponse> Handle(BillingPurchaseListByNexonRequest request)
            {
                return new BillingPurchaseListByNexonResponse
                {
                    CountList = new List<PurchaseCountDB>(),
                    OrderList = new List<PurchaseOrderDB>(),
                    MonthlyProductList = new List<MonthlyProductPurchaseDB>(),
                    ProductMonthlyIdInMailList = new List<long>(),
                    GachaTicketItemIdList = new List<long>(),
                    BlockedProductDBs = new List<BlockedProductDB>()
                };
            }
        }
    }
}
