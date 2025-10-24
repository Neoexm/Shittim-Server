using Plana.FlatData;

namespace Plana.Excel
{
    public static class EventContentStageExcelExt
    {
        public static EventContentStageExcelT GetEventContentStageId(this List<EventContentStageExcelT> campaignStageDB, long stageId)
        {
            return campaignStageDB.First(x => x.Id == stageId);
        }
    }
}