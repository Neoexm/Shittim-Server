using Schale.FlatData;

namespace Schale.Excel
{
    public static class ShopExcelExt
    {
        public static List<ShopExcelT> GetAssignedSaleShopExcel(this List<ShopExcelT> shops)
        {
            var saleItems = shops.Where(shop => !string.IsNullOrEmpty(shop.SalePeriodFrom) && !string.IsNullOrEmpty(shop.SalePeriodTo));
            return saleItems.ToList();
        }

        public static List<ShopExcelT> GetNonSaleShopExcel(this List<ShopExcelT> shops) =>
            shops.Where(shop => string.IsNullOrEmpty(shop.SalePeriodFrom) && string.IsNullOrEmpty(shop.SalePeriodTo)).ToList();

        public static List<ShopExcelT> GetCategorizedShopExcel(this List<ShopExcelT> shops, ShopCategoryType type) =>
            shops.Where(shop => shop.CategoryType == type).ToList();

        public static List<ShopExcelT> GetTimelinedShopExcel(this List<ShopExcelT> shops, DateTime dateTime)
        {
            var filtered = shops.Where(shop => 
                DateTime.TryParse(shop.SalePeriodFrom, out var from) && 
                DateTime.TryParse(shop.SalePeriodTo, out var to) &&
                from <= dateTime && 
                to >= dateTime);
            return filtered.ToList();
        }
    }
}


