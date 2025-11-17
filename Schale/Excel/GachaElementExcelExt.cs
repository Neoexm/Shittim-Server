using Schale.FlatData;

namespace Schale.Excel
{
    public static class GachaElementExcelExt
    {
        public static List<GachaElementExcelT> GetGachaElementsByGroupId(
            this List<GachaElementExcelT> elements, long gachaGroupId) =>
            elements.Where(el => el.GachaGroupID == gachaGroupId).ToList();
    }
}


