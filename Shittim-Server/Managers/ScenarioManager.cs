using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Shittim_Server.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Managers
{
    public class ScenarioManager
    {
        private readonly ExcelTableService excelTableService;
        private readonly ParcelHandler parcelHandler;

        private readonly List<ScenarioModeRewardExcelT> scenarioModeRewardExcels;

        public ScenarioManager(ExcelTableService _excelTableService, ParcelHandler _parcelHandler)
        {
            excelTableService = _excelTableService;
            parcelHandler = _parcelHandler;

            scenarioModeRewardExcels = excelTableService.GetTable<ScenarioModeRewardExcelT>();
        }

        public async Task<ScenarioGroupHistoryDBServer> ScenarioGroupHistoryUpdate(
            SchaleDataContext context, AccountDBServer account, ScenarioGroupHistoryUpdateRequest req)
        {
            var scenarioGroup = await context.ScenarioGroupHistories
                .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.ScenarioGroupUqniueId == req.ScenarioGroupHistoryDB.ScenarioGroupUqniueId);

            if (scenarioGroup == null)
            {
                scenarioGroup = new()
                {
                    AccountServerId = account.ServerId,
                    ScenarioGroupUqniueId = req.ScenarioGroupHistoryDB.ScenarioGroupUqniueId,
                    ScenarioType = req.ScenarioGroupHistoryDB.ScenarioType,
                    ClearDateTime = account.GameSettings.ServerDateTime()
                };
                context.ScenarioGroupHistories.Add(scenarioGroup);
                await context.SaveChangesAsync();
            }
            
            return scenarioGroup;
        }
        
        public async Task<(ScenarioHistoryDBServer, ParcelResultDB?)> ScenarioClear(
            SchaleDataContext context, AccountDBServer account, ScenarioClearRequest req)
        {
            var scenario = await context.ScenarioHistories
                .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.ScenarioUniqueId == req.ScenarioId);

            ParcelResultDB parcelResultDB = null;
            if (scenario == null)
            {
                scenario = new ScenarioHistoryDBServer()
                {
                    AccountServerId = account.ServerId,
                    ScenarioUniqueId = req.ScenarioId,
                    ClearDateTime = account.GameSettings.ServerDateTime(),
                };
                context.ScenarioHistories.Add(scenario);
                await context.SaveChangesAsync();

                var scenarioRewards = scenarioModeRewardExcels.GetScenarioRewardsById(req.ScenarioId);
                if (scenarioRewards.Count != 0)
                {
                    List<ParcelResult> parcelResult = [];
                    foreach (var reward in scenarioRewards)
                        parcelResult.Add(new ParcelResult(reward.RewardParcelType, reward.RewardParcelId, reward.RewardParcelAmount));
                    var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult);
                    parcelResultDB = parcelResolver.ParcelResult;
                }
            }

            return (scenario, parcelResultDB);
        }
    }
}
