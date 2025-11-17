using Schale.FlatData;

namespace Schale.Excel
{
    public static class ScenarioModeRewardExcelExt
    {
        public static List<ScenarioModeRewardExcelT> GetScenarioRewardsById(
            this List<ScenarioModeRewardExcelT> rewards, long id)
        {
            var filtered = rewards.Where(reward => reward.ScenarioModeRewardId == id);
            return filtered.ToList();
        }
    }
}


