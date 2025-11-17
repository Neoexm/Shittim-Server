using Schale.FlatData;

namespace Schale.Excel
{
    public static class WeekDungeonExcelExt
    {
        public static WeekDungeonExcelT GetDungeonByStageId(
            this List<WeekDungeonExcelT> dungeons, long stageId) =>
            dungeons.First(d => d.StageId == stageId);
    }
}


