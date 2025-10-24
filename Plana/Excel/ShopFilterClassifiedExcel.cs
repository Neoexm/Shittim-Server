using Plana.FlatData;

namespace Plana.Excel
{
    public static class ShopFilterClassifiedExcel
    {
        public static List<ShopFilterClassifiedExcelT> GetCategorizedShopFilter(this List<ShopFilterClassifiedExcelT> shopFilterClassifiedExcels, ShopCategoryType type)
        {
            return shopFilterClassifiedExcels.Where(x => x.CategoryType == type).ToList();
        }
    }
}