using Schale.FlatData;

namespace Schale.Excel
{
    public static class EquipmentLevelExcelExt
    {
        public static EquipmentLevelExcelT GetEquipmentLevelExcelByLevel(
            this List<EquipmentLevelExcelT> levels, long level) =>
            levels.FirstOrDefault(lvl => lvl.Level == level);
    }
}


