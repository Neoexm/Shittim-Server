using Schale.FlatData;

namespace Schale.Excel
{
    public static class EventContentMissionExcelExt
    {
        public static List<EventContentMissionExcelT> GetMissionExcelByEventContentId(
            this List<EventContentMissionExcelT> missions, long eventContentId) =>
            missions.Where(m => m.EventContentId == eventContentId).ToList();

        public static List<EventContentMissionExcelT> GetMissionExcelFromConditionParameter(
            this List<EventContentMissionExcelT> missions, long stageUniqueId)
        {
            var filtered = missions.Where(m => m.CompleteConditionParameter.Contains(stageUniqueId));
            return filtered.ToList();
        }

        public static EventContentMissionExcelT GetMissionExcelByCompleteExtensionTime(
            this List<EventContentMissionExcelT> missions) =>
            missions.First(m => m.IsCompleteExtensionTime == true);
    }
}


