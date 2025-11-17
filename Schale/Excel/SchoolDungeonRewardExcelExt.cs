using Schale.FlatData;

namespace Schale.Excel
{
    public static class SchoolDungeonRewardExcelExt
    {
        public static IEnumerable<SchoolDungeonRewardExcelT> GetAllRewardsByGroupId(
            this IEnumerable<SchoolDungeonRewardExcelT> rewards, long groupId) =>
            rewards.Where(reward => reward.GroupId == groupId);
    }
}


