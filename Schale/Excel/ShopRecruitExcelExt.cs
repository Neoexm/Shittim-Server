using Schale.FlatData;

namespace Schale.Excel
{
    public static class ShopRecruitExcelExt
    {
        public static List<ShopRecruitExcelT> GetAssignedCharacterIdShopRecruit(this List<ShopRecruitExcelT> recruits) =>
            recruits.Where(r => r.InfoCharacterId != null).ToList();

        public static List<ShopRecruitExcelT> GetAssignedSaleShopRecruit(this List<ShopRecruitExcelT> recruits)
        {
            var saleItems = recruits.Where(r => r.SalePeriodFrom != "" && r.SalePeriodTo != "");
            return saleItems.ToList();
        }

        public static List<ShopRecruitExcelT> GetNonSaleShopRecruit(this List<ShopRecruitExcelT> recruits) =>
            recruits.Where(r => r.SalePeriodFrom == "" && r.SalePeriodTo == "").ToList();

        public static List<ShopRecruitExcelT> GetTimelinedShopRecruit(this List<ShopRecruitExcelT> recruits, DateTime dateTime)
        {
            var filtered = recruits.Where(r => 
                DateTime.Parse(r.SalePeriodFrom) <= dateTime && 
                DateTime.Parse(r.SalePeriodTo) >= dateTime);
            return filtered.ToList();
        }

        public static List<ShopRecruitExcelT> GetCategorizedShopRecruit(
            this List<ShopRecruitExcelT> recruits, ShopCategoryType shopCategory) =>
            recruits.Where(r => r.CategoryType == shopCategory).ToList();
    }
}


