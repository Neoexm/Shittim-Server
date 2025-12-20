using Schale.FlatData;

namespace Schale.Excel
{
    public static class WeekDungeonRewardExcelExt
    {
        public static IEnumerable<WeekDungeonRewardExcelT> GetAllRewardsByGroupId(
            this IEnumerable<WeekDungeonRewardExcelT> rewards, long groupId) =>
            rewards.Where(reward => reward.GroupId == groupId);
    }
}


