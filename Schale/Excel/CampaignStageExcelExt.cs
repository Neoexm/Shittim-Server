using Schale.FlatData;

namespace Schale.Excel
{
    public static class CampaignStageExcelExt
    {
        public static CampaignStageExcelT GetCampaignStageId(this List<CampaignStageExcelT> stages, long stageId) =>
            stages.First(stage => stage.Id == stageId);
    }
}


