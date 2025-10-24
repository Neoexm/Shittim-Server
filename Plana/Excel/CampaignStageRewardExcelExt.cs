using Plana.FlatData;

namespace Plana.Excel
{
    public static class CampaignStageRewardExcelExt
    {
        public static IEnumerable<CampaignStageRewardExcelT> GetAllRewardsByGroupId(this IEnumerable<CampaignStageRewardExcelT> campaignStageRewards, long groupId)
        {
            return campaignStageRewards.Where(x => x.GroupId == groupId);
        }
    }
}