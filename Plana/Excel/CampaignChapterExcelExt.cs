using Plana.FlatData;

namespace Plana.Excel
{
    public static class CampaignChapterExcelExt
    {
        public static long GetChapterIdFromStageId(this List<CampaignChapterExcelT> campaignChapterExcels, long stageId)
        {
            var campaignChapterExcel = campaignChapterExcels
                .Where(x =>
                    x.NormalCampaignStageId.Contains(stageId) ||
                    x.HardCampaignStageId.Contains(stageId) ||
                    x.NormalExtraStageId.Contains(stageId) ||
                    x.VeryHardCampaignStageId.Contains(stageId))
                .ToList()
                .First();

            return campaignChapterExcel.Id;
        }
    }
}