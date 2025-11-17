using Schale.FlatData;

namespace Schale.Excel
{
    public static class CampaignStageRewardExcelExt
    {
        public static IEnumerable<CampaignStageRewardExcelT> GetAllRewardsByGroupId(this IEnumerable<CampaignStageRewardExcelT> rewards, long groupId) =>
            rewards.Where(reward => reward.GroupId == groupId);
    }
}


