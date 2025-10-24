using Plana.FlatData;

namespace Plana.Excel
{
    public static class EventContentScenarioExcelExt
    {
        public static EventContentScenarioExcelT GetScenarioExcelByScenarioGroupId(this List<EventContentScenarioExcelT> eventContentScenarioExcels, long id)
        {
            return eventContentScenarioExcels.First(x => x.ScenarioGroupId.Contains(id));
        }
    }
}