using Plana.FlatData;

namespace Plana.Excel
{
    public static class EquipmentLevelExcelExt
    {
        public static EquipmentLevelExcelT GetEquipmentLevelExcelByLevel(this List<EquipmentLevelExcelT> equipmentLevels, long level)
        {
            return equipmentLevels.FirstOrDefault(x => x.Level == level);
        }
    }
}