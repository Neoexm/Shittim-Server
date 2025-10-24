using Plana.FlatData;

namespace Plana.Excel
{
    public static class SchoolDungeonExcelExt
    {
        public static SchoolDungeonStageExcelT GetDungeonByStageId(this List<SchoolDungeonStageExcelT> schoolDungeonExcels, long stageId)
        {
            return schoolDungeonExcels.Where(x => x.StageId == stageId).First();
        }
    }
}