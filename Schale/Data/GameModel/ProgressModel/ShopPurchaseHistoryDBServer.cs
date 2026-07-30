using System.ComponentModel.DataAnnotations;

namespace Schale.Data.GameModel
{
    // Per-account, per-shop purchase counter. ShopExcel carries a PurchaseCountLimit (20 for the AP
    // shop) and a PurchaseCountResetType (None/Day/Week/Month), but the count itself has to live
    // server-side: without this row a limited product is buyable forever and Shop_List reports
    // zero. PeriodStart records which reset window the count belongs to; when the window rolls over
    // the counter is zeroed rather than deleted, so the row stays stable for the account's lifetime.
    public class ShopPurchaseHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long ShopUniqueId { get; set; }
        public long PurchaseCount { get; set; }

        /// <summary>
        /// Start of the reset window <see cref="PurchaseCount"/> was accumulated in.
        /// <see cref="DateTime.MinValue"/> for shops that never reset.
        /// </summary>
        public DateTime PeriodStart { get; set; }
    }

    public static class ShopPurchaseHistoryDBServerExtensions
    {
        public static IQueryable<ShopPurchaseHistoryDBServer> GetAccountShopPurchases(this SchaleDataContext context, long accountId)
        {
            return context.ShopPurchaseHistories.Where(x => x.AccountServerId == accountId);
        }
    }
}
