using Schale.FlatData;

namespace Schale.Excel
{
    public static class SchoolDungeonExcelExt
    {
        public static SchoolDungeonStageExcelT GetDungeonByStageId(
            this List<SchoolDungeonStageExcelT> dungeons, long stageId) =>
            dungeons.First(d => d.StageId == stageId);
    }
}


