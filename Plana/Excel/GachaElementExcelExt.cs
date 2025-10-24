using Plana.FlatData;

namespace Plana.Excel
{
    public static class GachaElementExcelExt
    {
        public static List<GachaElementExcelT> GetGachaElementsByGroupId(this List<GachaElementExcelT> gachaElementExcels, long gachaGroupId)
        {
            return gachaElementExcels.Where(x => x.GachaGroupID == gachaGroupId).ToList();
        }
    }
}