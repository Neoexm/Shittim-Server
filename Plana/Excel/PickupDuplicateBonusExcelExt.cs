using Plana.FlatData;

namespace Plana.Excel
{
    public static class PickupDuplicateBonusExcelExt
    {
        public static PickupDuplicateBonusExcelT GetPickupDuplicateBonusByShopId(this List<PickupDuplicateBonusExcelT> pickupDupBonus, long shopId)
        {
            return pickupDupBonus.First(x => x.ShopId == shopId);
        }
    }
}