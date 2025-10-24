using Plana.FlatData;

namespace Plana.Excel
{
    public static class WorldRaidStageExcelTableExt
    {
        public static WorldRaidStageExcelT GetWorldRaidStageExcelById(this List<WorldRaidStageExcelT> worldRaidStageExcels, long id)
        {
            return worldRaidStageExcels.First(x => x.Id == id);
        }

        public static List<WorldRaidStageExcelT> GetWorldRaidStageExcelsById(this List<WorldRaidStageExcelT> worldRaidStageExcels, long id)
        {
            return worldRaidStageExcels.Where(x => x.Id == id).ToList();
        }

        public static List<WorldRaidStageExcelT> GetWorldRaidStageExcelsByGroupId(this List<WorldRaidStageExcelT> worldRaidStageExcels, long id)
        {
            return worldRaidStageExcels.Where(x => x.WorldRaidBossGroupId == id).ToList();
        }
    }
}