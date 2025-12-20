using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class SchoolDungeonManager
{
    private readonly ExcelTableService _excelTableService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    public SchoolDungeonManager(
        ExcelTableService excelTableService,
        ParcelHandler parcelHandler,
        IMapper mapper)
    {
        _excelTableService = excelTableService;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
    }

    public async Task<ParcelResultDB> SchoolDungeonEnterStage(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        var dungeonStages = _excelTableService.GetTable<SchoolDungeonStageExcelT>();
        var dungeonStage = dungeonStages.GetDungeonByStageId(stageUniqueId);

        var parcelInfos = ParcelResult.ConvertParcelResult(
            dungeonStage.StageEnterCostType,
            dungeonStage.StageEnterCostId,
            dungeonStage.StageEnterCostAmount);

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos, isConsume: true);

        return parcelResolver.ParcelResult;
    }

    public async Task<(SchoolDungeonStageHistoryDBServer, ParcelResultDB)> SchoolDungeonBattleResult(
        SchaleDataContext context,
        AccountDBServer account,
        SchoolDungeonBattleResultRequest req)
    {
        var dungeonStages = _excelTableService.GetTable<SchoolDungeonStageExcelT>();
        var schoolDungeonExcel = dungeonStages.GetDungeonByStageId(req.StageUniqueId);

        var historyDb = await context.SchoolDungeonStageHistories
            .FirstOrDefaultAsync(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.StageUniqueId);

        if (!CheckIfCleared(req.Summary))
        {
            var retreatParcel = await SchoolDungeonRetreat(context, account, req.StageUniqueId);
            return (historyDb, retreatParcel);
        }

        if (historyDb == null)
        {
            historyDb = new SchoolDungeonStageHistoryDBServer
            {
                AccountServerId = req.AccountId,
                StageUniqueId = req.StageUniqueId
            };
            CalcStarGoals(schoolDungeonExcel, historyDb, req.Summary);
            context.SchoolDungeonStageHistories.Add(historyDb);
        }
        else
        {
            var newStarFlags = new bool[schoolDungeonExcel.StarGoal.Count];
            CalcStarGoalsIntoArray(schoolDungeonExcel, newStarFlags, req.Summary);

            for (var i = 0; i < historyDb.StarFlags.Length && i < newStarFlags.Length; i++)
            {
                historyDb.StarFlags[i] = historyDb.StarFlags[i] || newStarFlags[i];
            }

            context.Entry(historyDb).State = EntityState.Modified;
        }

        var dungeonRewards = _excelTableService.GetTable<SchoolDungeonRewardExcelT>();
        var rewardDatas = dungeonRewards.GetAllRewardsByGroupId(schoolDungeonExcel.StageRewardId);
        var parcelInfo = GetCalcProbability(rewardDatas);

        var parcelAccount = ParcelResult.ConvertParcelResult(
            schoolDungeonExcel.StageEnterCostType,
            schoolDungeonExcel.StageEnterCostId,
            schoolDungeonExcel.StageEnterCostAmount);
        parcelInfo.Add(parcelAccount.ConvertToAccountExp());

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);
        var parcelResultDb = parcelResolver.ParcelResult;

        await context.SaveChangesAsync();

        return (historyDb, parcelResultDb);
    }

    public async Task<ParcelResultDB> SchoolDungeonRetreat(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        var dungeonStages = _excelTableService.GetTable<SchoolDungeonStageExcelT>();
        var schoolDungeonExcel = dungeonStages.GetDungeonByStageId(stageUniqueId);

        var parcelInfo = ParcelResult.ConvertParcelResult(
            schoolDungeonExcel.StageEnterCostType,
            schoolDungeonExcel.StageEnterCostId,
            schoolDungeonExcel.StageEnterCostAmount);

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);

        return parcelResolver.ParcelResult;
    }

    private static bool CheckIfCleared(BattleSummary summary)
    {
        return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
    }

    private static void CalcStarGoals(SchoolDungeonStageExcelT excel, SchoolDungeonStageHistoryDBServer historyDB, BattleSummary battleSummary)
    {
        historyDB.StarFlags = new bool[excel.StarGoal.Count];

        var starGoalTypes = excel.StarGoal;
        var starGoalAmounts = excel.StarGoalAmount;

        for (int i = 0; i < starGoalTypes.Count; i++)
        {
            var targetGoalType = starGoalTypes[i];
            var targetGoalAmount = starGoalAmounts[i];

            historyDB.StarFlags[i] = IsStarGoalCleared(targetGoalType, targetGoalAmount, battleSummary);
        }
    }

    private static void CalcStarGoalsIntoArray(SchoolDungeonStageExcelT excel, bool[] starFlags, BattleSummary battleSummary)
    {
        var starGoalTypes = excel.StarGoal;
        var starGoalAmounts = excel.StarGoalAmount;

        for (int i = 0; i < starGoalTypes.Count && i < starFlags.Length; i++)
        {
            var targetGoalType = starGoalTypes[i];
            var targetGoalAmount = starGoalAmounts[i];

            starFlags[i] = IsStarGoalCleared(targetGoalType, targetGoalAmount, battleSummary);
        }
    }

    private static bool IsStarGoalCleared(StarGoalType goalType, int goalAmount, BattleSummary battleSummary)
    {
        switch (goalType)
        {
            case StarGoalType.Clear:
                return battleSummary.EndType == BattleEndType.Clear;

            case StarGoalType.AllAlive:
                foreach (var hero in battleSummary.Group01Summary.Heroes)
                {
                    if (hero.DeadFrame != -1)
                        return false;
                }
                return true;

            case StarGoalType.ClearTimeInSec:
                return battleSummary.EndFrame <= goalAmount * 30;

            default:
                return false;
        }
    }

    private static List<ParcelResult> GetCalcProbability(IEnumerable<SchoolDungeonRewardExcelT> rewardExcels)
    {
        var result = new List<ParcelResult>();
        foreach (var rewardExcel in rewardExcels)
        {
            if (!GenerateProbability(rewardExcel.RewardParcelProbability))
                continue;

            var parcelInfo = new ParcelResult(
                rewardExcel.RewardParcelType,
                rewardExcel.RewardParcelId,
                rewardExcel.RewardParcelAmount);
            result.Add(parcelInfo);
        }
        return result;
    }

    private static bool GenerateProbability(long probability)
    {
        if (probability == 0) return true;
        return Random.Shared.Next(10000) < probability;
    }
}
