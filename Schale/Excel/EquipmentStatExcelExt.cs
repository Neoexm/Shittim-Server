using Schale.FlatData;

namespace Schale.Excel
{
    public static class EquipmentStatExcelExt
    {
        public static EquipmentStatExcelT GetEquipmentStatExcelById(
            this List<EquipmentStatExcelT> stats, long id) =>
            stats.First(stat => stat.EquipmentId == id);
    }
}


