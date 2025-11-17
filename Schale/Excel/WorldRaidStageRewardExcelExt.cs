using Schale.FlatData;

namespace Schale.Excel
{
    public static class WorldRaidStageRewardExcelExt
    {
        public static List<WorldRaidStageRewardExcelT> GetWorldRaidStageRewardByGroupId(
            this List<WorldRaidStageRewardExcelT> rewards, long groupId)
        {
            var filtered = rewards.Where(reward => reward.GroupId == groupId);
            return filtered.ToList();
        }
    }
}


