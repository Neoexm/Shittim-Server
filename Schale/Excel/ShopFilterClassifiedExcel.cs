using Schale.FlatData;

namespace Schale.Excel
{
    public static class ShopFilterClassifiedExcel
    {
        public static List<ShopFilterClassifiedExcelT> GetCategorizedShopFilter(
            this List<ShopFilterClassifiedExcelT> filters, ShopCategoryType type) =>
            filters.Where(filter => filter.CategoryType == type).ToList();
    }
}


