using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class ConcentrateCampaignManager
{
    private readonly ExcelTableService _excelService;
    private readonly HexaMapService _hexaMapService;
    private readonly IMapper _mapper;

    public ConcentrateCampaignManager(
        ExcelTableService excelService,
        HexaMapService hexaMapService,
        IMapper mapper)
    {
        _excelService = excelService;
        _hexaMapService = hexaMapService;
        _mapper = mapper;
    }

    public async Task<CampaignMainStageSaveDBServer?> GetConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        return await context.CampaignMainStageSaves
            .Where(x => x.AccountServerId == account.ServerId && x.StageUniqueId == stageUniqueId)
            .OrderByDescending(x => x.ServerId)
            .FirstOrDefaultAsync();
    }

    public async Task<CampaignMainStageSaveDBServer> CreateConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account,
        long stageUniqueId)
    {
        var hexaData = await _hexaMapService.LoadState(stageUniqueId);
        var dateTime = account.GameSettings.ServerDateTime();

        var stageSave = new CampaignMainStageSaveDBServer
        {
            AccountServerId = account.ServerId,
            LastEnemyEntityId = hexaData.LastEntityId,
            EnemyInfos = HexaMapService.AddHexaUnitList(hexaData.HexaUnitList ?? new List<HexaUnit>()),
            StrategyObjects = HexaMapService.AddHexaStrategyList(hexaData.HexaStrageyList ?? new List<Strategy>()),
            ActivatedHexaEventsAndConditions = new Dictionary<long, List<long>>(),
            HexaEventDelayedExecutions = new Dictionary<long, List<long>>(),
            TileMapStates = HexaMapService.AddHexaTileList(hexaData),
            CreateTime = dateTime,
            StageUniqueId = stageUniqueId,
            EchelonInfos = new Dictionary<long, HexaUnit>(),
            WithdrawInfos = new Dictionary<long, List<long>>(),
            DeployedEchelonInfos = new List<HexaUnit>(),
            DisplayInfos = new List<HexaDisplayInfo>(),
            StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
            StrategyObjectHistory = new List<long>(),
            CampaignState = CampaignState.BeforeStart,
            CurrentTurn = 0,
            TacticRankSCount = 0
        };

        context.CampaignMainStageSaves.Add(stageSave);
        await context.SaveChangesAsync();

        return stageSave;
    }

    public async Task<CampaignMainStageSaveDBServer> DeployEchelon(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignDeployEchelonRequest deployReq)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, deployReq.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {deployReq.StageUniqueId}");

        stageSaveData.EchelonInfos ??= new Dictionary<long, HexaUnit>();

        stageSaveData.EchelonInfos = await DeployConcentratedEchelon(
            context, account, stageSaveData.EchelonInfos, deployReq.DeployedEchelons);

        context.Entry(stageSaveData).Property(x => x.EchelonInfos).IsModified = true;
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    private async Task<Dictionary<long, HexaUnit>> DeployConcentratedEchelon(
        SchaleDataContext context,
        AccountDBServer account,
        Dictionary<long, HexaUnit> existHexaUnitData,
        List<HexaUnit> hexaUnitData)
    {
        foreach (var unit in hexaUnitData)
        {
            existHexaUnitData.Remove(unit.EntityId);
        }

        var movementOrder = existHexaUnitData.Count;

        foreach (var hexaUnit in hexaUnitData)
        {
            movementOrder++;
            var echelonData = await EchelonService.GetConcentratedCampaignEchelon(
                context, account.ServerId, hexaUnit.EntityId);

            if (echelonData == null)
                continue;

            var modified = new HexaUnit
            {
                EntityId = hexaUnit.EntityId,
                HpInfos = CreateHpInfos(echelonData.MainSlotServerIds, echelonData.SupportSlotServerIds),
                DyingInfos = new Dictionary<long, long>(),
                ActionCountMax = 1,
                Mobility = 1,
                StrategySightRange = 1,
                Id = hexaUnit.Id,
                Location = hexaUnit.Location,
                Rotate = hexaUnit.Rotate,
                IsPlayer = hexaUnit.IsPlayer,
                MovementOrder = movementOrder
            };

            existHexaUnitData.Add(hexaUnit.EntityId, modified);
        }

        return existHexaUnitData;
    }

    private Dictionary<long, long> CreateHpInfos(List<long> mainSlotServerIds, List<long> supportSlotServerIds)
    {
        var hpInfos = new Dictionary<long, long>();

        foreach (var mainSlotServerId in mainSlotServerIds)
        {
            hpInfos[mainSlotServerId] = 10000L;
        }

        foreach (var supportSlotServerId in supportSlotServerIds)
        {
            hpInfos[supportSlotServerId] = 10000L;
        }

        return hpInfos;
    }

    public async Task<CampaignMainStageSaveDBServer> StartConcentrateCampaign(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignConfirmMainStageRequest stageReq)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, stageReq.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {stageReq.StageUniqueId}");

        var deployedEchelonInfos = stageSaveData.EchelonInfos?.Values.ToList() ?? new List<HexaUnit>();

        stageSaveData.CampaignState = CampaignState.BeginPlayerPhase;
        stageSaveData.CurrentTurn = 1;
        stageSaveData.DeployedEchelonInfos = HexaMapService.DeployHexaUnitList(deployedEchelonInfos);

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    public async Task<CampaignMainStageSaveDBServer> MoveTarget(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignMapMoveRequest moveReq)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, moveReq.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {moveReq.StageUniqueId}");

        stageSaveData.CampaignState = CampaignState.PlayerPhase;
        stageSaveData.DisplayInfos ??= new List<HexaDisplayInfo>();
        stageSaveData.DisplayInfos.Add(
            HexaMapService.AddHexaDisplayInfo(moveReq.EchelonEntityId, moveReq.DestPosition));

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
    }

    public async Task<(CampaignMainStageSaveDBServer, CampaignStageHistoryDB)> TacticResult(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignTacticResultRequest req)
    {
        var dateTime = account.GameSettings.ServerDateTime();
        var stageSaveData = await GetConcentrateCampaign(context, account, req.Summary.StageId);

        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.Summary.StageId}");

        var campaignChapterExcels = _excelService.GetTable<CampaignChapterExcelT>();
        var chapterUniqueId = campaignChapterExcels.GetChapterIdFromStageId(req.Summary.StageId);

        var historyDb = new CampaignStageHistoryDBServer
        {
            AccountServerId = req.AccountId,
            StageUniqueId = req.Summary.StageId,
            ChapterUniqueId = chapterUniqueId,
            LastPlay = dateTime,
            TodayPlayCount = 0,
            ClearTurnRecord = 0,
            Star1Flag = false,
            Star2Flag = false,
            Star3Flag = false
        };

        stageSaveData.TacticClearTimeMscSum += (long)Math.Floor(req.Summary.EndFrame / 30f) * 1000;
        stageSaveData.EchelonInfos = ChangeConcentratedEchelon(stageSaveData.EchelonInfos, req.Summary);

        if (!CheckIfCleared(req.Summary))
        {
            if (stageSaveData.EchelonInfos != null)
                stageSaveData.EchelonInfos.Remove(req.Summary.Group01Summary.TeamId);
        }
        else
        {
            CalcStrategySkipStarGoals(historyDb, req.Summary);
        }

        var existHistory = await context.CampaignStageHistories
            .FirstOrDefaultAsync(x =>
                x.AccountServerId == req.AccountId &&
                x.StageUniqueId == req.Summary.StageId);

        if (existHistory != null)
        {
            MergeExistHistoryWithNew(context, existHistory, historyDb, dateTime);
            historyDb = existHistory;
        }
        else
        {
            context.CampaignStageHistories.Add(historyDb);
        }

        stageSaveData.DisplayInfos = null;
        stageSaveData.EchelonInfos = ChangeConcentratedEchelon(stageSaveData.EchelonInfos, req.Summary);

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        var historyMap = historyDb.ToMap(_mapper);
        return (stageSaveData, historyMap);
    }

    private Dictionary<long, HexaUnit>? ChangeConcentratedEchelon(
        Dictionary<long, HexaUnit>? existHexaUnitData,
        BattleSummary battleSummary)
    {
        if (existHexaUnitData == null)
            return null;

        foreach (var kvp in existHexaUnitData.Where(x => x.Value.EntityId == battleSummary.Group01Summary.TeamId))
        {
            kvp.Value.HpInfos = ChangeHpInfos(
                battleSummary.Group01Summary.Heroes,
                battleSummary.Group02Summary.Supporters
            );

            kvp.Value.DyingInfos = ChangeHpInfos(
                battleSummary.Group01Summary.Heroes,
                battleSummary.Group02Summary.Supporters,
                isDying: true
            );
        }

        return existHexaUnitData;
    }

    private Dictionary<long, long> ChangeHpInfos(
        HeroSummaryCollection mainHeroSummary,
        HeroSummaryCollection supportHeroSummary,
        bool isDying = false)
    {
        var hpInfos = new Dictionary<long, long>();

        foreach (var mainHeroChar in mainHeroSummary)
        {
            if (mainHeroChar.HPRateAfter == 0 && !isDying)
                continue;
            hpInfos[mainHeroChar.ServerId] = mainHeroChar.HPRateAfter;
        }

        foreach (var supportHeroChar in supportHeroSummary)
        {
            if (supportHeroChar.HPRateAfter == 0 && !isDying)
                continue;
            hpInfos[supportHeroChar.ServerId] = supportHeroChar.HPRateAfter;
        }

        return hpInfos;
    }

    public async Task<CampaignMainStageSaveDBServer> EndTurn(
        SchaleDataContext context,
        AccountDBServer account,
        CampaignEndTurnRequest req)
    {
        var stageSaveData = await GetConcentrateCampaign(context, account, req.StageUniqueId);
        if (stageSaveData == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {req.StageUniqueId}");

        if (stageSaveData.CampaignState == CampaignState.PlayerPhase)
        {
            stageSaveData.CampaignState = CampaignState.EnemyPhase;
        }
        else if (stageSaveData.CampaignState == CampaignState.EnemyPhase)
        {
            stageSaveData.CampaignState = CampaignState.PlayerPhase;

            if (stageSaveData.EchelonInfos != null)
            {
                foreach (var echelon in stageSaveData.EchelonInfos.Values)
                {
                    echelon.ActionCount += 1;
                }
            }

            stageSaveData.CurrentTurn++;
        }

        context.CampaignMainStageSaves.Update(stageSaveData);
        await context.SaveChangesAsync();

        return stageSaveData;
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

    private bool CalcAllEnemiesDefeated(BattleSummary summary)
    {
        if (summary.Group02Summary == null)
            return false;

        foreach (var enemy in summary.Group02Summary.Heroes)
        {
            if (enemy.DeadFrame == -1)
                return false;
        }

        return true;
    }

    private bool CalcAllEnemiesDefeatedInTime(BattleSummary summary)
    {
        if (summary.Group02Summary == null || summary.Group02Summary.Heroes.Count == 0)
            return false;

        var lastEnemyDeadFrame = summary.Group02Summary.Heroes
            .Where(x => x.DeadFrame != -1)
            .Max(x => x.DeadFrame);

        return lastEnemyDeadFrame <= 120 * 30;
    }

    private bool CalcAllAlive(BattleSummary summary)
    {
        if (summary.Group01Summary == null)
            return false;

        foreach (var hero in summary.Group01Summary.Heroes)
        {
            if (hero.DeadFrame != -1)
                return false;
        }

        return true;
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
}
