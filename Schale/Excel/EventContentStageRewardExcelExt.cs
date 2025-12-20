using Schale.FlatData;

namespace Schale.Excel
{
    public static class EventContentStageRewardExcelExt
    {
        public static IEnumerable<EventContentStageRewardExcelT> GetAllRewardsByGroupId(
            this IEnumerable<EventContentStageRewardExcelT> rewards, long groupId) =>
            rewards.Where(reward => reward.GroupId == groupId);
    }
}


