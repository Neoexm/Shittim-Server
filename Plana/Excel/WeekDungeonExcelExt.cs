using Plana.FlatData;

namespace Plana.Excel
{
    public static class WeekDungeonExcelExt
    {
        public static WeekDungeonExcelT GetDungeonByStageId(this List<WeekDungeonExcelT> weekDungeonExcels, long stageId)
        {
            return weekDungeonExcels.Where(x => x.StageId == stageId).First();
        }
    }
}