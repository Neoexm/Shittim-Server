using Plana.FlatData;

namespace Plana.Excel
{
    public static class CampaignStageExcelExt
    {
        public static CampaignStageExcelT GetCampaignStageId(this List<CampaignStageExcelT> campaignStageDB, long stageId)
        {
            return campaignStageDB.First(x => x.Id == stageId);
        }
    }
}