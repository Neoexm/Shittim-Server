using Plana.FlatData;

namespace Plana.Excel
{
    public static class ShopExcelExt
    {
        public static List<ShopExcelT> GetAssignedSaleShopExcel(this List<ShopExcelT> shopExcels)
        {
            return shopExcels.Where(x => x.SalePeriodFrom != "" && x.SalePeriodTo != "").ToList();
        }

        public static List<ShopExcelT> GetNonSaleShopExcel(this List<ShopExcelT> shopExcels)
        {
            return shopExcels.Where(x => x.SalePeriodFrom == "" && x.SalePeriodTo == "").ToList();
        }

        public static List<ShopExcelT> GetCategorizedShopExcel(this List<ShopExcelT> shopExcels, ShopCategoryType type)
        {
            return shopExcels.Where(x => x.CategoryType == type).ToList();
        }

        public static List<ShopExcelT> GetTimelinedShopExcel(this List<ShopExcelT> shopExcels, DateTime dateTime)
        {
            return shopExcels.Where(x => DateTime.Parse(x.SalePeriodFrom) <= dateTime && DateTime.Parse(x.SalePeriodTo) >= dateTime).ToList();
        }
    }
}