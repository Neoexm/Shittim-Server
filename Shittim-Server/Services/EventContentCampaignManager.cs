using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services
{
    public class EventContentCampaignManager
    {
        private readonly ExcelTableService _excelService;
        private readonly ParcelHandler _parcelHandler;
        private readonly IMapper _mapper;

        public EventContentCampaignManager(
            ExcelTableService excelService,
            ParcelHandler parcelHandler,
            IMapper mapper)
        {
            _excelService = excelService;
            _parcelHandler = parcelHandler;
            _mapper = mapper;
        }

        public async Task<(List<ParcelInfo>, ParcelResultDB)> EventContentEnterStage(
            SchaleDataContext context,
            AccountDBServer account,
            long stageUniqueId)
        {
            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(stageUniqueId);

            var parcelInfo = new ParcelResult(
                campaignStageExcel.StageEnterCostType,
                campaignStageExcel.StageEnterCostId,
                campaignStageExcel.StageEnterCostAmount);

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo, isConsume: true);

            return (parcelResolver.ParcelInfos, parcelResolver.ParcelResult);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB, Dictionary<long, List<MissionProgressDB>>)> 
            EventContentStoryStageResult(
                SchaleDataContext context,
                AccountDBServer account,
                EventContentStoryStageResultRequest req)
        {
            var dateTime = account.GameSettings.ServerDateTime();

            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(req.StageUniqueId);

            var historyDb = new CampaignStageHistoryDBServer
            {
                AccountServerId = req.AccountId,
                StageUniqueId = req.StageUniqueId,
                TodayPlayCount = 0,
                LastPlay = dateTime
            };

            var existingHistory = await context.CampaignStageHistories
                .FirstOrDefaultAsync(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.StageUniqueId);

            if (existingHistory == null)
            {
                context.CampaignStageHistories.Add(historyDb);
            }
            else
            {
                MergeExistHistoryWithNew(context, existingHistory, historyDb, dateTime);
                historyDb = existingHistory;
            }

            var missionExcels = _excelService.GetTable<EventContentMissionExcelT>();
            var missionExcel = missionExcels
                .GetMissionExcelByEventContentId(req.EventContentId)
                .GetMissionExcelFromConditionParameter(req.StageUniqueId);

            Dictionary<long, List<MissionProgressDB>> eventMissionProgressDBDict = new();

            if (missionExcel.Count != 0)
            {
                var missionExcelId = missionExcel.GetMissionExcelByCompleteExtensionTime().Id;
                var existingMission = await context.MissionProgresses
                    .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == missionExcelId);

                if (existingMission != null)
                {
                    if (existingMission.ProgressParameters == null)
                    {
                        existingMission.ProgressParameters = new Dictionary<long, long>();
                    }
                    existingMission.ProgressParameters[req.StageUniqueId] = 1;
                }
                else
                {
                    var newMission = new MissionProgressDBServer
                    {
                        AccountServerId = account.ServerId,
                        MissionUniqueId = missionExcelId,
                        StartTime = account.GameSettings.ServerDateTime(),
                        ProgressParameters = new Dictionary<long, long> { { req.StageUniqueId, 1 } }
                    };
                    context.MissionProgresses.Add(newMission);
                    existingMission = newMission;
                }

                eventMissionProgressDBDict = new Dictionary<long, List<MissionProgressDB>>
                {
                    { req.EventContentId, new List<MissionProgressDB> { existingMission.ToMap(_mapper) } }
                };
            }

            var rewardExcels = _excelService.GetTable<EventContentStageRewardExcelT>();
            var campaignRewardExcel = rewardExcels.GetAllRewardsByGroupId(campaignStageExcel.Id).ToList();

            var parcelInfos = campaignRewardExcel
                .Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount))
                .ToList();

            parcelInfos.Add(new ParcelResult(
                ParcelType.AccountExp,
                campaignStageExcel.StageEnterCostId,
                campaignStageExcel.StageEnterCostAmount));

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);
            await context.SaveChangesAsync();

            return (historyDb, parcelResolver.ParcelResult, eventMissionProgressDBDict);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB, Dictionary<long, List<MissionProgressDB>>)>
            EventContentMainGroundStageResult(
                SchaleDataContext context,
                AccountDBServer account,
                EventContentMainGroundStageResultRequest req)
        {
            var dateTime = account.GameSettings.ServerDateTime();

            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(req.Summary.StageId);

            CampaignStageHistoryDBServer historyDb = new();

            if (CheckIfCleared(req.Summary))
            {
                historyDb = new CampaignStageHistoryDBServer
                {
                    AccountServerId = req.AccountId,
                    StageUniqueId = req.Summary.StageId,
                    TodayPlayCount = 0,
                    LastPlay = dateTime
                };

                CalcStrategySkipStarGoals(historyDb, req.Summary);

                var existingHistory = await context.CampaignStageHistories
                    .FirstOrDefaultAsync(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.Summary.StageId);

                if (existingHistory != null)
                {
                    MergeExistHistoryWithNew(context, existingHistory, historyDb, dateTime);
                    historyDb = existingHistory;
                }
                else
                {
                    context.CampaignStageHistories.Add(historyDb);
                }
            }
            else
            {
                var amount = (long)(campaignStageExcel.StageEnterCostAmount * 0.9);
                var parcelInfo = new ParcelResult(campaignStageExcel.StageEnterCostType, 0, amount);
                var parcelRetreat = await _parcelHandler.BuildParcel(context, account, parcelInfo);
                return (historyDb, parcelRetreat.ParcelResult, new Dictionary<long, List<MissionProgressDB>>());
            }

            var missionExcels = _excelService.GetTable<EventContentMissionExcelT>();
            var missionExcel = missionExcels
                .GetMissionExcelByEventContentId(req.EventContentId)
                .GetMissionExcelFromConditionParameter(req.Summary.StageId);

            Dictionary<long, List<MissionProgressDB>> eventMissionProgressDBDict = new();

            if (missionExcel.Count != 0)
            {
                var missionExcelId = missionExcel.GetMissionExcelByCompleteExtensionTime().Id;
                var existingMission = await context.MissionProgresses
                    .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == missionExcelId);

                if (existingMission != null)
                {
                    if (existingMission.ProgressParameters == null)
                    {
                        existingMission.ProgressParameters = new Dictionary<long, long>();
                    }
                    existingMission.ProgressParameters[req.Summary.StageId] = 1;
                }
                else
                {
                    var newMission = new MissionProgressDBServer
                    {
                        AccountServerId = account.ServerId,
                        MissionUniqueId = missionExcelId,
                        StartTime = account.GameSettings.ServerDateTime(),
                        ProgressParameters = new Dictionary<long, long> { { req.Summary.StageId, 1 } }
                    };
                    context.MissionProgresses.Add(newMission);
                    existingMission = newMission;
                }

                eventMissionProgressDBDict = new Dictionary<long, List<MissionProgressDB>>
                {
                    { req.EventContentId, new List<MissionProgressDB> { existingMission.ToMap(_mapper) } }
                };
            }

            var rewardExcels = _excelService.GetTable<EventContentStageRewardExcelT>();
            var campaignRewardExcel = rewardExcels.GetAllRewardsByGroupId(campaignStageExcel.Id).ToList();

            var parcelInfos = campaignRewardExcel
                .Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount))
                .ToList();

            parcelInfos.Add(new ParcelResult(ParcelType.AccountExp, 0, campaignStageExcel.StageEnterCostAmount));

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);
            await context.SaveChangesAsync();

            return (historyDb, parcelResolver.ParcelResult, eventMissionProgressDBDict);
        }

        private void MergeExistHistoryWithNew(
            SchaleDataContext context,
            CampaignStageHistoryDBServer existHistoryDb,
            CampaignStageHistoryDBServer newHistoryDb,
            DateTime dateTime)
        {
            existHistoryDb.Star1Flag = existHistoryDb.Star1Flag || newHistoryDb.Star1Flag;
            existHistoryDb.Star2Flag = existHistoryDb.Star2Flag || newHistoryDb.Star2Flag;
            existHistoryDb.Star3Flag = existHistoryDb.Star3Flag || newHistoryDb.Star3Flag;

            existHistoryDb.TodayPlayCount += 1;
            existHistoryDb.LastPlay = dateTime;

            context.CampaignStageHistories.Update(existHistoryDb);
        }

        private bool CheckIfCleared(BattleSummary summary)
        {
            return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
        }

        private void CalcStrategySkipStarGoals(CampaignStageHistoryDBServer historyDB, BattleSummary summary)
        {
            historyDB.Star1Flag = CalcAllEnemiesDefeated(summary);
            historyDB.Star2Flag = CalcAllEnemiesDefeatedInTime(summary);
            historyDB.Star3Flag = CalcAllAlive(summary);
            historyDB.ClearTurnRecord = 1;
        }

        private bool CalcAllEnemiesDefeated(BattleSummary battleSummary)
        {
            if (battleSummary.Group02Summary == null) return false;

            foreach (var enemy in battleSummary.Group02Summary.Heroes)
            {
                if (enemy.DeadFrame == -1) return false;
            }

            return true;
        }

        private bool CalcAllEnemiesDefeatedInTime(BattleSummary battleSummary)
        {
            if (battleSummary.Group02Summary == null) return false;

            var lastEnemy = battleSummary.Group02Summary.Heroes.LastOrDefault();
            if (lastEnemy == null) return false;

            return lastEnemy.DeadFrame <= 120 * 30;
        }

        private bool CalcAllAlive(BattleSummary battleSummary)
        {
            if (battleSummary.Group01Summary == null) return false;

            foreach (var hero in battleSummary.Group01Summary.Heroes)
            {
                if (hero.DeadFrame != -1) return false;
            }

            return true;
        }
    }
}
