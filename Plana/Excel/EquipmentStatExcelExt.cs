using Plana.FlatData;

namespace Plana.Excel
{
    public static class EquipmentStatExcelExt
    {
        public static EquipmentStatExcelT GetEquipmentStatExcelById(this List<EquipmentStatExcelT> equipments, long id)
        {
            return equipments.First(x => x.EquipmentId == id);
        }
    }
}