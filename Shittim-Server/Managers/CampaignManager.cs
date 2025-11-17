using AutoMapper;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.Battles;
using BlueArchiveAPI.Services;
using Shittim_Server.Services;

namespace Shittim_Server.Managers
{
    public class CampaignManager
    {
        private readonly ExcelTableService _excelService;
        private readonly ParcelHandler _parcelHandler;
        private readonly IMapper _mapper;

        private readonly List<CampaignStageExcelT> _campaignStageExcels;
        private readonly List<CampaignStageRewardExcelT> _campaignStageRewardExcels;
        private readonly List<CampaignChapterExcelT> _campaignChapterExcels;
        private readonly List<CampaignChapterRewardExcelT> _campaignChapterRewardExcels;

        public CampaignManager(
            ExcelTableService excelService, 
            ParcelHandler parcelHandler, 
            IMapper mapper)
        {
            _excelService = excelService;
            _parcelHandler = parcelHandler;
            _mapper = mapper;

            _campaignStageExcels = _excelService.GetTable<CampaignStageExcelT>();
            _campaignStageRewardExcels = _excelService.GetTable<CampaignStageRewardExcelT>();
            _campaignChapterExcels = _excelService.GetTable<CampaignChapterExcelT>();
            _campaignChapterRewardExcels = _excelService.GetTable<CampaignChapterRewardExcelT>();
        }
        
        public List<ParcelInfo> TemporaryCampaignParcelInit(
            SchaleDataContext context, AccountDBServer account, long stageUniqueId)
        {
            var campaignExcel = _campaignStageExcels.GetCampaignStageId(stageUniqueId);
            var parcelInfos = ParcelInfo.CreateParcelInfo(
                campaignExcel.StageEnterCostType, 
                campaignExcel.StageEnterCostId, 
                campaignExcel.StageEnterCostAmount);
            return parcelInfos;
        }

        public async Task<(List<ParcelInfo>, ParcelResultDB)> CampaignEnterStage(
            SchaleDataContext context, AccountDBServer account, long stageUniqueId)
        {
            var campaignExcel = _campaignStageExcels.GetCampaignStageId(stageUniqueId);
            var parcelInfos = new ParcelResult(
                campaignExcel.StageEnterCostType, 
                campaignExcel.StageEnterCostId, 
                campaignExcel.StageEnterCostAmount);
            var parcelResult = await _parcelHandler.BuildParcel(context, account, parcelInfos, isConsume: true);

            return (parcelResult.ParcelInfos, parcelResult.ParcelResult);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB)> CampaignTutorialStageResult(
            SchaleDataContext context, AccountDBServer account, CampaignTutorialStageResultRequest req)
        {
            var campaignExcel = _campaignStageExcels.GetCampaignStageId(req.Summary.StageId);
            CampaignStageHistoryDBServer historyDb = new();

            if (CheckIfCleared(req.Summary))
            {
                var dateTime = account.GameSettings.ServerDateTime();
                var chapterId = _campaignChapterExcels.GetChapterIdFromStageId(req.Summary.StageId);
                historyDb = new CampaignStageHistoryDBServer(req.AccountId, req.Summary.StageId, chapterId, dateTime)
                {
                    ClearTurnRecord = 1
                };

                if (!context.CampaignStageHistories.Any(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.Summary.StageId))
                    context.CampaignStageHistories.Add(historyDb);
            }
            else
            {
                var retreatParcel = await CampaignRetreat(context, account, req.Summary.StageId);
                return (historyDb, retreatParcel);
            }
            
            var rewardDatas = _campaignStageRewardExcels.GetAllRewardsByGroupId(campaignExcel.CampaignStageRewardId);
            var parcelInfo = GetCalcProbability(rewardDatas);
            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);
            var parcelResultDb = parcelResolver.ParcelResult;
            
            await context.SaveChangesAsync();

            return (historyDb, parcelResultDb);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB)> CampaignSubStageResult(
            SchaleDataContext context, AccountDBServer account, CampaignSubStageResultRequest req)
        {
            var campaignExcel = _campaignStageExcels.GetCampaignStageId(req.Summary.StageId);
            var dateTime = account.GameSettings.ServerDateTime();
            CampaignStageHistoryDBServer historyDb = new();

            if (CheckIfCleared(req.Summary))
            {
                var chapterId = _campaignChapterExcels.GetChapterIdFromStageId(req.Summary.StageId);
                historyDb = new CampaignStageHistoryDBServer(req.AccountId, req.Summary.StageId, chapterId, dateTime);
                CalcStrategySkipStarGoals(historyDb, req.Summary);

                if (context.CampaignStageHistories.Any(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.Summary.StageId))
                {
                    var existHistory = context.GetAccountCampaignStageHistories(req.AccountId)
                        .Where(x => x.StageUniqueId == req.Summary.StageId).First();
                    MergeExistHistoryWithNew(context, existHistory, historyDb, dateTime);

                    historyDb = existHistory;
                }
                else
                    context.CampaignStageHistories.Add(historyDb);
            }
            else
            {
                var retreatParcel = await CampaignRetreat(context, account, req.Summary.StageId);
                return (historyDb, retreatParcel);
            }
            
            var rewardDatas = _campaignStageRewardExcels.GetAllRewardsByGroupId(campaignExcel.CampaignStageRewardId);
            var parcelInfo = GetCalcProbability(rewardDatas);
            parcelInfo.Add(new ParcelResult(ParcelType.AccountExp, 0, campaignExcel.StageEnterCostAmount));
            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);
            var parcelResultDb = parcelResolver.ParcelResult;

            await context.SaveChangesAsync();

            return (historyDb, parcelResultDb);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB)> CampaignMainStageStrategySkipResult(
            SchaleDataContext context, AccountDBServer account, CampaignMainStageStrategySkipResultRequest req)
        {
            var campaignExcel = _campaignStageExcels.GetCampaignStageId(req.Summary.StageId);
            var dateTime = account.GameSettings.ServerDateTime();

            CampaignStageHistoryDBServer historyDb = new();

            if (CheckIfCleared(req.Summary))
            {
                var chapterId = _campaignChapterExcels.GetChapterIdFromStageId(req.Summary.StageId);
                historyDb = new CampaignStageHistoryDBServer(req.AccountId, req.Summary.StageId, chapterId, dateTime);
                CalcStrategySkipStarGoals(historyDb, req.Summary);

                if (context.CampaignStageHistories.Any(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.Summary.StageId))
                {
                    var existHistory = context.GetAccountCampaignStageHistories(req.AccountId)
                        .Where(x => x.StageUniqueId == req.Summary.StageId).First();
                    MergeExistHistoryWithNew(context, existHistory, historyDb, dateTime);

                    historyDb = existHistory;
                }
                else
                    context.CampaignStageHistories.Add(historyDb);
            }
            else
            {
                var retreatParcel = await CampaignRetreat(context, account, req.Summary.StageId);
                return (historyDb, retreatParcel);
            }
            
            var rewardDatas = _campaignStageRewardExcels.GetAllRewardsByGroupId(campaignExcel.CampaignStageRewardId);
            var parcelInfo = GetCalcProbability(rewardDatas);
            parcelInfo.Add(new ParcelResult(ParcelType.AccountExp, 0, campaignExcel.StageEnterCostAmount));
            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);
            var parcelResultDb = parcelResolver.ParcelResult;

            await context.SaveChangesAsync();

            return (historyDb, parcelResultDb);
        }

        public async Task<(CampaignChapterClearRewardHistoryDBServer, ParcelResultDB)> CampaignChapterClearReward(
            SchaleDataContext context, AccountDBServer account, CampaignChapterClearRewardRequest req)
        {
            var campaignExcel = _campaignChapterExcels.First(x => x.Id == req.CampaignChapterUniqueId);

            var rewardHistory = new CampaignChapterClearRewardHistoryDBServer()
            {
                AccountServerId = account.ServerId,
                ChapterUniqueId = req.CampaignChapterUniqueId,
                RewardType = req.StageDifficulty,
                ReceiveDate = account.GameSettings.ServerDateTime()
            };
            context.CampaignChapterClearRewardHistories.Add(rewardHistory);
            await context.SaveChangesAsync();

            var parcelResult = CreateClearRewardParcel(_campaignChapterRewardExcels, campaignExcel, req.StageDifficulty);
            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelResult);

            return (rewardHistory, parcelResolver.ParcelResult);
        }

        public async Task<ParcelResultDB> CampaignRetreat(
            SchaleDataContext context, AccountDBServer account, long stageUniqueId)
        {
            var campaignExcel = _campaignStageExcels.GetCampaignStageId(stageUniqueId);
            var amount = (long)(campaignExcel.StageEnterCostAmount * 0.9);
            var parcelInfos = new ParcelResult(campaignExcel.StageEnterCostType, campaignExcel.StageEnterCostId, amount);

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);
            return parcelResolver.ParcelResult;
        }

        private static List<ParcelResult> GetCalcProbability(IEnumerable<CampaignStageRewardExcelT> rewardExcels)
        {
            var result = new List<ParcelResult>();
            foreach (var rewardExcel in rewardExcels)
            {
                if (!GenerateProbability(rewardExcel.StageRewardProb)) continue;
                var parcelInfos = new ParcelResult(
                    rewardExcel.StageRewardParcelType, 
                    rewardExcel.StageRewardId, 
                    rewardExcel.StageRewardAmount);
                result.Add(parcelInfos);
            }
            return result;
        }

        private static void MergeExistHistoryWithNew(
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

        private static bool CheckIfCleared(BattleSummary summary)
        {
            return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
        }

        private static List<ParcelResult> CreateClearRewardParcel(
            List<CampaignChapterRewardExcelT> campaignChapterRewardExcels, 
            CampaignChapterExcelT campaignExcel, 
            StageDifficulty stage)
        {
            var parcelInfos = new List<ParcelResult>();
            CampaignChapterRewardExcelT stageData;
            switch (stage)
            {
                case StageDifficulty.Normal:
                    stageData = campaignChapterRewardExcels.FirstOrDefault(x => x.Id == campaignExcel.ChapterRewardId);
                    parcelInfos = ParcelResult.ConvertParcelResult(
                        stageData.ChapterRewardParcelType, 
                        stageData.ChapterRewardId, 
                        stageData.ChapterRewardAmount.Select(x => (long)x).ToList());
                    break;
                case StageDifficulty.Hard:
                    stageData = campaignChapterRewardExcels.FirstOrDefault(x => x.Id == campaignExcel.ChapterHardRewardId);
                    parcelInfos = ParcelResult.ConvertParcelResult(
                        stageData.ChapterRewardParcelType, 
                        stageData.ChapterRewardId, 
                        stageData.ChapterRewardAmount.Select(x => (long)x).ToList());
                    break;
            }
            return parcelInfos;
        }

        private static void CalcStrategySkipStarGoals(CampaignStageHistoryDBServer historyDB, BattleSummary summary)
        {
            historyDB.Star1Flag = CalcAllEnemiesDefeated(summary);
            historyDB.Star2Flag = CalcAllEnemiesDefeatedInTime(summary);
            historyDB.Star3Flag = CalcAllAlive(summary);
            historyDB.ClearTurnRecord = 1;
        }

        private static bool CalcAllEnemiesDefeated(BattleSummary battleSummary)
        {
            if (battleSummary.Group02Summary == null) return false;
            foreach (var enemy in battleSummary.Group02Summary.Heroes)
            {
                if (enemy.DeadFrame == -1) return false;
            }
            return true;
        }

        private static bool CalcAllEnemiesDefeatedInTime(BattleSummary battleSummary)
        {
            if (battleSummary.Group02Summary == null) return false;
            return battleSummary.Group02Summary.Heroes.Last().DeadFrame <= 120 * 30;
        }

        private static bool CalcAllAlive(BattleSummary battleSummary)
        {
            if (battleSummary.Group01Summary == null) return false;
            foreach (var hero in battleSummary.Group01Summary.Heroes)
            {
                if (hero.DeadFrame != -1) return false;
            }
            return true;
        }

        private static bool GenerateProbability(long probability)
        {
            if (probability == 0) return true;
            return Random.Shared.Next(10000) < probability;
        }
    }
}
