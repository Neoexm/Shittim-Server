using Plana.FlatData;

namespace Plana.Excel
{
    public static class EventContentStageRewardExcelExt
    {
        public static IEnumerable<EventContentStageRewardExcelT> GetAllRewardsByGroupId(this IEnumerable<EventContentStageRewardExcelT> eventContentStageExcels, long groupId)
        {
            return eventContentStageExcels.Where(x => x.GroupId == groupId);
        }
    }
}