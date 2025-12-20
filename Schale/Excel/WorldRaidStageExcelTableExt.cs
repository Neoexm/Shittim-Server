using Schale.FlatData;

namespace Schale.Excel
{
    public static class WorldRaidStageExcelTableExt
    {
        public static WorldRaidStageExcelT GetWorldRaidStageExcelById(
            this List<WorldRaidStageExcelT> stages, long id) =>
            stages.First(stage => stage.Id == id);

        public static List<WorldRaidStageExcelT> GetWorldRaidStageExcelsById(
            this List<WorldRaidStageExcelT> stages, long id)
        {
            var filtered = stages.Where(stage => stage.Id == id);
            return filtered.ToList();
        }

        public static List<WorldRaidStageExcelT> GetWorldRaidStageExcelsByGroupId(
            this List<WorldRaidStageExcelT> stages, long id) =>
            stages.Where(stage => stage.WorldRaidBossGroupId == id).ToList();
    }
}


