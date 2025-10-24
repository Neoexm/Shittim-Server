using Plana.FlatData;

namespace Plana.Excel
{
    public static class WorldRaidStageRewardExcelExt
    {
        public static List<WorldRaidStageRewardExcelT> GetWorldRaidStageRewardByGroupId(
            this List<WorldRaidStageRewardExcelT> worldRaidStageRewardExcels, long groupId)
        {
            return worldRaidStageRewardExcels.Where(x => x.GroupId == groupId).ToList();
        }
    }
}