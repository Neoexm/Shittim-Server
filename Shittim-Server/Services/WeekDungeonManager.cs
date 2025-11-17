using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class WeekDungeonManager
{
    private readonly ExcelTableService _excelTableService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    public WeekDungeonManager(
        ExcelTableService excelTableService,
        ParcelHandler parcelHandler,
        IMapper mapper)
    {
        _excelTableService = excelTableService;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
    }

    public async Task<ParcelResultDB> WeekDungeonEnterStage(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        var dungeonStages = _excelTableService.GetTable<WeekDungeonExcelT>();
        var dungeonStage = dungeonStages.GetDungeonByStageId(stageUniqueId);

        var parcelInfos = ParcelResult.ConvertParcelResult(
            dungeonStage.StageEnterCostType,
            dungeonStage.StageEnterCostId,
            dungeonStage.StageEnterCostAmount.Select(x => (long)x).ToList());

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos, isConsume: true);

        return parcelResolver.ParcelResult;
    }

    public async Task<(WeekDungeonStageHistoryDBServer, ParcelResultDB)> WeekDungeonBattleResult(
        SchaleDataContext context,
        AccountDBServer account,
        WeekDungeonBattleResultRequest req)
    {
        var dungeonStages = _excelTableService.GetTable<WeekDungeonExcelT>();
        var weekDungeonExcel = dungeonStages.GetDungeonByStageId(req.StageUniqueId);

        var historyDb = await context.WeekDungeonStageHistories
            .FirstOrDefaultAsync(x => x.AccountServerId == req.AccountId && x.StageUniqueId == req.StageUniqueId);

        if (!CheckIfCleared(req.Summary))
        {
            var retreatParcel = await WeekDungeonRetreat(context, account, req.StageUniqueId);
            return (historyDb, retreatParcel);
        }

        if (historyDb == null)
        {
            historyDb = new WeekDungeonStageHistoryDBServer
            {
                AccountServerId = req.AccountId,
                StageUniqueId = req.StageUniqueId,
                StarGoalRecord = CalcStarGoals(weekDungeonExcel, req.Summary)
            };
            context.WeekDungeonStageHistories.Add(historyDb);
        }
        else
        {
            var newStarGoals = CalcStarGoals(weekDungeonExcel, req.Summary);

            foreach (var goal in newStarGoals)
            {
                if (!historyDb.StarGoalRecord.TryGetValue(goal.Key, out var currentValue))
                {
                    historyDb.StarGoalRecord[goal.Key] = goal.Value;
                }
                else
                {
                    historyDb.StarGoalRecord[goal.Key] = Math.Max(currentValue, goal.Value);
                }
            }

            context.Entry(historyDb).State = EntityState.Modified;
        }

        var dungeonRewards = _excelTableService.GetTable<WeekDungeonRewardExcelT>();
        var rewardDatas = dungeonRewards.GetAllRewardsByGroupId(weekDungeonExcel.StageRewardId);
        var parcelInfo = GetCalcProbability(rewardDatas);

        AddExperience(parcelInfo, weekDungeonExcel);

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);
        var parcelResultDb = parcelResolver.ParcelResult;

        await context.SaveChangesAsync();

        return (historyDb, parcelResultDb);
    }

    public async Task<ParcelResultDB> WeekDungeonRetreat(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        var dungeonStages = _excelTableService.GetTable<WeekDungeonExcelT>();
        var weekDungeonExcel = dungeonStages.GetDungeonByStageId(stageUniqueId);

        var parcelInfo = ParcelResult.ConvertParcelResult(
            weekDungeonExcel.StageEnterCostType,
            weekDungeonExcel.StageEnterCostId,
            weekDungeonExcel.StageEnterCostAmount.Select(x => (long)x).ToList());

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo);

        return parcelResolver.ParcelResult;
    }

    private static bool CheckIfCleared(BattleSummary summary)
    {
        return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
    }

    private static Dictionary<StarGoalType, long> CalcStarGoals(WeekDungeonExcelT excel, BattleSummary battleSummary)
    {
        var starGoalRecord = new Dictionary<StarGoalType, long>();
        for (int i = 0; i < excel.StarGoal.Count; i++)
        {
            switch (excel.StarGoal[i])
            {
                case StarGoalType.Clear:
                    starGoalRecord[excel.StarGoal[i]] = CalcClear(battleSummary);
                    break;
                case StarGoalType.AllAlive:
                    starGoalRecord[excel.StarGoal[i]] = CalcAllAlive(battleSummary);
                    break;
                case StarGoalType.GetBoxes:
                    starGoalRecord[excel.StarGoal[i]] = CalcGetBoxes(battleSummary);
                    break;
                case StarGoalType.ClearTimeInSec:
                    starGoalRecord[excel.StarGoal[i]] = CalcClearTime(battleSummary, excel.StarGoalAmount[i]);
                    break;
            }
        }
        return starGoalRecord;
    }

    private static long CalcClear(BattleSummary summary)
    {
        return CheckIfCleared(summary) ? 1 : 0;
    }

    private static long CalcAllAlive(BattleSummary battleSummary)
    {
        if (battleSummary.Group01Summary == null) return 0;
        foreach (var hero in battleSummary.Group01Summary.Heroes)
        {
            if (hero.DeadFrame != -1) return 0;
        }
        return 1;
    }

    private static long CalcGetBoxes(BattleSummary battleSummary)
    {
        if (battleSummary.WeekDungeonSummary == null) return 0;
        return battleSummary.WeekDungeonSummary.FindGifts.First().ClearCount;
    }

    private static long CalcClearTime(BattleSummary battleSummary, long goalSeconds)
    {
        if (battleSummary.EndFrame <= goalSeconds * 30) return 1;
        return 0;
    }

    private static List<ParcelResult> GetCalcProbability(IEnumerable<WeekDungeonRewardExcelT> rewardExcels)
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

    private static void AddExperience(List<ParcelResult> parcelResults, WeekDungeonExcelT excel)
    {
        if (excel.WeekDungeonType == WeekDungeonType.FindGift || excel.WeekDungeonType == WeekDungeonType.Blood)
            parcelResults.Add(new ParcelResult(ParcelType.AccountExp, 0, excel.StageEnterCostAmount.Sum()));
    }
}
