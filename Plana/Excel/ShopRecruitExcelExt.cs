using Plana.FlatData;

namespace Plana.Excel
{
    public static class ShopRecruitExcelExt
    {
        public static List<ShopRecruitExcelT> GetAssignedCharacterIdShopRecruit(this List<ShopRecruitExcelT> shopRecruitExcels)
        {
            return shopRecruitExcels.Where(x => x.InfoCharacterId != null).ToList();
        }

        public static List<ShopRecruitExcelT> GetAssignedSaleShopRecruit(this List<ShopRecruitExcelT> shopRecruitExcels)
        {
            return shopRecruitExcels.Where(x => x.SalePeriodFrom != "" && x.SalePeriodTo != "").ToList();
        }

        public static List<ShopRecruitExcelT> GetNonSaleShopRecruit(this List<ShopRecruitExcelT> shopRecruitExcels)
        {
            return shopRecruitExcels.Where(x => x.SalePeriodFrom == "" && x.SalePeriodTo == "").ToList();
        }

        public static List<ShopRecruitExcelT> GetTimelinedShopRecruit(this List<ShopRecruitExcelT> shopRecruitExcels, DateTime dateTime)
        {
            return shopRecruitExcels.Where(x => DateTime.Parse(x.SalePeriodFrom) <= dateTime && DateTime.Parse(x.SalePeriodTo) >= dateTime).ToList();
        }

        public static List<ShopRecruitExcelT> GetCategorizedShopRecruit(this List<ShopRecruitExcelT> shopRecruitExcels, ShopCategoryType shopCategory)
        {
            return shopRecruitExcels.Where(x => x.CategoryType == shopCategory).ToList();
        }
    }
}