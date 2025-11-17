using Schale.FlatData;

namespace Schale.Excel
{
    public static class ItemExcelExt
    {
        public static ItemExcelT GetItemExcelById(this List<ItemExcelT> items, long id) =>
            items.First(item => item.Id == id);
    }
}


