using Schale.FlatData;

namespace Schale.Excel
{
    public static class PickupDuplicateBonusExcelExt
    {
        public static PickupDuplicateBonusExcelT GetPickupDuplicateBonusByShopId(
            this List<PickupDuplicateBonusExcelT> bonuses, long shopId) =>
            bonuses.First(bonus => bonus.ShopId == shopId);
    }
}


