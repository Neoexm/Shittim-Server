using Plana.FlatData;

namespace Plana.Excel
{
    public static class ItemExcelExt
    {
        public static ItemExcelT GetItemExcelById(this List<ItemExcelT> items, long id)
        {
            return items.First(x => x.Id == id);
        }
    }
}