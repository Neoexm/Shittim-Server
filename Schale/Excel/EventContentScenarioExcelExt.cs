using Schale.FlatData;

namespace Schale.Excel
{
    public static class EventContentScenarioExcelExt
    {
        public static EventContentScenarioExcelT GetScenarioExcelByScenarioGroupId(
            this List<EventContentScenarioExcelT> scenarios, long id) =>
            scenarios.First(scenario => scenario.ScenarioGroupId.Contains(id));
    }
}


