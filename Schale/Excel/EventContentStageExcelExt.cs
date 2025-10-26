using Schale.FlatData;

namespace Schale.Excel
{
    public static class EventContentStageExcelExt
    {
        public static EventContentStageExcelT GetEventContentStageId(
            this List<EventContentStageExcelT> stages, long stageId) =>
            stages.First(stage => stage.Id == stageId);
    }
}


