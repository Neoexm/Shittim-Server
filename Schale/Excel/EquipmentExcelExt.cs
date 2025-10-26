using Schale.FlatData;

namespace Schale.Excel
{
    public static class EquipmentExcelExt
    {
        public static EquipmentExcelT GetEquipmentExcelById(this List<EquipmentExcelT> equipments, long id) =>
            equipments.First(eq => eq.Id == id);

        public static EquipmentExcelT GetEquipmentExcelByTier(this List<EquipmentExcelT> equipments, long tier) =>
            equipments.First(eq => eq.TierInit == tier);

        public static EquipmentExcelT GetEquipmentExcelByLevel(this List<EquipmentExcelT> equipments, long level)
        {
            var filtered = equipments.Where(eq => eq.MaxLevel <= level);
            return filtered.OrderByDescending(eq => eq.TierInit).Last();
        }

        public static List<EquipmentExcelT> GetCharacterEquipment(this List<EquipmentExcelT> equipments) =>
            equipments.Where(eq => eq.MaxLevel != 1).ToList();

        public static List<EquipmentExcelT> GetItemEquipment(this List<EquipmentExcelT> equipments)
        {
            var characterEquipment = equipments.GetCharacterEquipment();
            var wornItems = characterEquipment.Where(eq => eq.Wear == true);
            var baseItems = equipments.Where(eq => (eq.MaxLevel == 1 || eq.MaxLevel == 0) && eq.TierInit != 1);
            return baseItems.Concat(wornItems).DistinctBy(eq => eq.Id).ToList();
        }
        
        public static List<EquipmentExcelT> GetEquipmentExcelByCategory(this List<EquipmentExcelT> equipments, EquipmentCategory equipmentCategory) =>
            equipments.Where(eq => eq.EquipmentCategory == equipmentCategory).ToList();

        public static List<EquipmentExcelT> GetEquipmentByTierUpgrade(this List<EquipmentExcelT> equipments, long currentTier, long afterTier) =>
            equipments.Where(eq => eq.TierInit > currentTier && eq.TierInit <= afterTier).ToList();
    }
}


