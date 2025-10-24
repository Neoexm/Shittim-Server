using Plana.FlatData;

namespace Plana.Excel
{
    public static class EquipmentExcelExt
    {
        public static EquipmentExcelT GetEquipmentExcelById(this List<EquipmentExcelT> equipments, long id)
        {
            return equipments.First(x => x.Id == id);
        }

        public static EquipmentExcelT GetEquipmentExcelByTier(this List<EquipmentExcelT> equipments, long tier)
        {
            return equipments.First(x => x.TierInit == tier);
        }

        public static EquipmentExcelT GetEquipmentExcelByLevel(this List<EquipmentExcelT> equipments, long level)
        {
            return equipments.Where(x => x.MaxLevel <= level).OrderByDescending(x => x.TierInit).Last();
        }

        public static List<EquipmentExcelT> GetCharacterEquipment(this List<EquipmentExcelT> equipments)
        {
            return equipments.Where(x => x.MaxLevel != 1).ToList();
        }

        public static List<EquipmentExcelT> GetItemEquipment(this List<EquipmentExcelT> equipments)
        {
            var wearedItems = equipments.GetCharacterEquipment().Where(x => x.Wear == true);
            return equipments.Where(x => (x.MaxLevel == 1 || x.MaxLevel == 0) && x.TierInit != 1)
                .Concat(wearedItems).DistinctBy(x => x.Id).ToList();
        }
        
        public static List<EquipmentExcelT> GetEquipmentExcelByCategory(this List<EquipmentExcelT> equipments, EquipmentCategory equipmentCategory)
        {
            return equipments.Where(x => x.EquipmentCategory == equipmentCategory).ToList();
        }

        public static List<EquipmentExcelT> GetEquipmentByTierUpgrade(this List<EquipmentExcelT> equipments, long currentTier, long afterTier)
        {
            return equipments.Where(x => x.TierInit > currentTier && x.TierInit <= afterTier).ToList();
        }
    }
}