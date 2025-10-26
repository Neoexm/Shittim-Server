using Schale.FlatData;

namespace Schale.Excel
{
    public static class CampaignChapterExcelExt
    {
        public static long GetChapterIdFromStageId(this List<CampaignChapterExcelT> chapters, long stageId)
        {
            var matchingChapter = chapters
                .Where(chapter =>
                    chapter.NormalCampaignStageId.Contains(stageId) ||
                    chapter.HardCampaignStageId.Contains(stageId) ||
                    chapter.NormalExtraStageId.Contains(stageId) ||
                    chapter.VeryHardCampaignStageId.Contains(stageId))
                .First();

            return matchingChapter.Id;
        }
    }
}


