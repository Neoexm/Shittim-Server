using Schale.FlatData;

namespace Schale.Excel
{
    public static class ShopExcelExt
    {
        public static List<ShopExcelT> GetAssignedSaleShopExcel(this List<ShopExcelT> shops)
        {
            var saleItems = shops.Where(shop => shop.SalePeriodFrom != "" && shop.SalePeriodTo != "");
            return saleItems.ToList();
        }

        public static List<ShopExcelT> GetNonSaleShopExcel(this List<ShopExcelT> shops) =>
            shops.Where(shop => shop.SalePeriodFrom == "" && shop.SalePeriodTo == "").ToList();

        public static List<ShopExcelT> GetCategorizedShopExcel(this List<ShopExcelT> shops, ShopCategoryType type) =>
            shops.Where(shop => shop.CategoryType == type).ToList();

        public static List<ShopExcelT> GetTimelinedShopExcel(this List<ShopExcelT> shops, DateTime dateTime)
        {
            var filtered = shops.Where(shop => 
                DateTime.Parse(shop.SalePeriodFrom) <= dateTime && 
                DateTime.Parse(shop.SalePeriodTo) >= dateTime);
            return filtered.ToList();
        }
    }
}


