using Plana.FlatData;

namespace Plana.Excel
{
    public static class ScenarioModeRewardExcelExt
    {
        public static List<ScenarioModeRewardExcelT> GetScenarioRewardsById(
            this List<ScenarioModeRewardExcelT> scenarioModeRewardExcels, long id)
        {
            return scenarioModeRewardExcels.Where(x => x.ScenarioModeRewardId == id).ToList();
        }
    }
}