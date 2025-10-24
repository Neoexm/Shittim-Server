using Plana.FlatData;

namespace Plana.Excel
{
    public static class EventContentMissionExcelExt
    {
        public static List<EventContentMissionExcelT> GetMissionExcelByEventContentId(this List<EventContentMissionExcelT> eventContentMissionExcels, long eventContentId)
        {
            return eventContentMissionExcels.Where(x => x.EventContentId == eventContentId).ToList();
        }

        public static List<EventContentMissionExcelT> GetMissionExcelFromConditionParameter(this List<EventContentMissionExcelT> eventContentMissionExcels, long stageUniqueId)
        {
            return eventContentMissionExcels.Where(x => x.CompleteConditionParameter.Contains(stageUniqueId)).ToList();
        }

        public static EventContentMissionExcelT GetMissionExcelByCompleteExtensionTime(this List<EventContentMissionExcelT> eventContentMissionExcels)
        {
            return eventContentMissionExcels.First(x => x.IsCompleteExtensionTime == true);
        }
    }
}