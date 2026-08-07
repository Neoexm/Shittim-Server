using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Managers;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ConquestHandler : ProtocolHandlerBase
{
    private const int MaxManagePerRequest = 100;

    private readonly ISessionKeyService _sessionService;
    private readonly ConquestManager _conquestManager;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;

    public ConquestHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConquestManager conquestManager,
        ExcelTableService excelService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _conquestManager = conquestManager;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Conquest_GetInfo)]
    public async Task<ConquestGetInfoResponse> GetInfo(
        SchaleDataContext db,
        ConquestGetInfoRequest request,
        ConquestGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = _conquestManager.GetOrCreate(db, account, request.EventContentId);

        response.IsFirstEnter = !info.FirstEnterDone;
        if (!info.FirstEnterDone)
        {
            info.FirstEnterDone = true;
            db.ConquestInfos.Update(info);
            await db.SaveChangesAsync();
        }

        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
        response.ConquestedTileDBs = info.Tiles;
        response.ConquestEchelonDBs = info.Echelons;
        response.DifficultyToStepDict = info.StepByDifficulty;

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryGetInfo)]
    public async Task<ConquestMainStoryGetInfoResponse> MainStoryGetInfo(
        SchaleDataContext db,
        ConquestMainStoryGetInfoRequest request,
        ConquestMainStoryGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = _conquestManager.GetOrCreate(db, account, request.EventContentId);

        response.IsFirstEnter = !info.FirstEnterDone;
        if (!info.FirstEnterDone)
        {
            info.FirstEnterDone = true;
            db.ConquestInfos.Update(info);
            await db.SaveChangesAsync();
        }

        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
        response.ConquestedTileDBs = info.Tiles;
        response.DifficultyToStepDict = info.StepByDifficulty;

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_Check)]
    public async Task<ConquestCheckResponse> Check(
        SchaleDataContext db,
        ConquestCheckRequest request,
        ConquestCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.GetAccountConquestInfos(account.ServerId)
            .FirstOrDefault(x => x.EventContentId == request.EventContentId);
        if (info == null)
            return response;

        var conditionAmount = _conquestManager.CalculateConditionAmount(info.EventContentId);
        response.ParcelConsumeCumulatedAmount = info.CumulatedConditionValue;
        response.CanReceiveCalculateReward = conditionAmount > 0
            && info.CumulatedConditionValue - info.ReceivedCalculateRewardConditionAmount >= conditionAmount;
        response.ConquestSummary = BuildSummary(info);

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryCheck)]
    public async Task<ConquestMainStoryCheckResponse> MainStoryCheck(
        SchaleDataContext db,
        ConquestMainStoryCheckRequest request,
        ConquestMainStoryCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.GetAccountConquestInfos(account.ServerId)
            .FirstOrDefault(x => x.EventContentId == request.EventContentId);
        if (info == null)
            return response;

        var summary = BuildSummary(info);
        response.ConquestMainStorySummary = new ConquestMainStorySummary
        {
            EventContentId = info.EventContentId,
            Difficulty = StageDifficulty.Normal,
            ConquestStepSummaryDict = summary.ConquestStepSummaryDict?
                .ToDictionary(kv => kv.Key, kv => new ConquestMainStoryStepSummary
                {
                    ConqueredTileCount = kv.Value.ConqueredTileCount,
                    AllTileCount = kv.Value.AllTileCount,
                    IsStepOpen = kv.Value.IsStepOpen
                })
        };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_Conquer)]
    public async Task<ConquestConquerResponse> Conquer(
        SchaleDataContext db,
        ConquestConquerRequest request,
        ConquestConquerResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        var (tile, parcelResult, _) = await _conquestManager.Conquer(
            db, account, info, request.Difficulty, request.TileUniqueId, throughBattle: false);

        response.ParcelResultDB = parcelResult;
        response.ConquestTileDB = tile;
        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryConquer)]
    public async Task<ConquestMainStoryConquerResponse> MainStoryConquer(
        SchaleDataContext db,
        ConquestMainStoryConquerRequest request,
        ConquestMainStoryConquerResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        var (tile, parcelResult, _) = await _conquestManager.Conquer(
            db, account, info, request.Difficulty, request.TileUniqueId, throughBattle: false);

        response.ParcelResultDB = parcelResult;
        response.ConquestTileDB = tile;
        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ConquerWithBattleStart)]
    public async Task<ConquestConquerWithBattleStartResponse> ConquerWithBattleStart(
        SchaleDataContext db,
        ConquestConquerWithBattleStartRequest request,
        ConquestConquerWithBattleStartResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        response.ConquestStageSaveDB = StartTileBattle(db, account, info, request.Difficulty, request.TileUniqueId);
        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryConquerWithBattleStart)]
    public async Task<ConquestMainStoryConquerWithBattleStartResponse> MainStoryConquerWithBattleStart(
        SchaleDataContext db,
        ConquestMainStoryConquerWithBattleStartRequest request,
        ConquestMainStoryConquerWithBattleStartResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        response.ConquestStageSaveDB = StartTileBattle(db, account, info, request.Difficulty, request.TileUniqueId);
        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ConquerWithBattleResult)]
    public async Task<ConquestConquerWithBattleResultResponse> ConquerWithBattleResult(
        SchaleDataContext db,
        ConquestConquerWithBattleResultRequest request,
        ConquestConquerWithBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        _conquestManager.RequireAndClearOpenBattle(db, info, request.Difficulty, request.TileUniqueId);

        if (!ConquestManager.IsWin(request.BattleSummary))
        {
            response.ParcelResultDB = new ParcelResultDB();
            response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
            response.StepAfterBattle = _conquestManager.CurrentStep(info, request.Difficulty);
            await db.SaveChangesAsync();
            return response;
        }

        var (tile, parcelResult, byTag) = await _conquestManager.Conquer(
            db, account, info, request.Difficulty, request.TileUniqueId, throughBattle: true);

        response.ParcelResultDB = parcelResult;
        response.ConquestTileDB = tile;
        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
        response.StepAfterBattle = _conquestManager.CurrentStep(info, request.Difficulty);
        response.DisplayParcelByRewardTag = byTag;

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryConquerWithBattleResult)]
    public async Task<ConquestMainStoryConquerWithBattleResultResponse> MainStoryConquerWithBattleResult(
        SchaleDataContext db,
        ConquestMainStoryConquerWithBattleResultRequest request,
        ConquestMainStoryConquerWithBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        _conquestManager.RequireAndClearOpenBattle(db, info, request.Difficulty, request.TileUniqueId);

        if (!ConquestManager.IsWin(request.BattleSummary))
        {
            response.ParcelResultDB = new ParcelResultDB();
            response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
            response.StepAfterBattle = _conquestManager.CurrentStep(info, request.Difficulty);
            await db.SaveChangesAsync();
            return response;
        }

        var (tile, parcelResult, byTag) = await _conquestManager.Conquer(
            db, account, info, request.Difficulty, request.TileUniqueId, throughBattle: true);

        response.ParcelResultDB = parcelResult;
        response.ConquestTileDB = tile;
        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
        response.StepAfterBattle = _conquestManager.CurrentStep(info, request.Difficulty);
        response.DisplayParcelByRewardTag = byTag;

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ManageBase)]
    public async Task<ConquestManageBaseResponse> ManageBase(
        SchaleDataContext db,
        ConquestManageBaseRequest request,
        ConquestManageBaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        var tileExcel = _conquestManager.RequireTile(info.EventContentId, request.TileUniqueId);
        if (tileExcel.TileType != ConquestTileType.Base)
            throw new WebAPIException(WebAPIErrorCode.ConquestInvalidTileType, $"Tile {request.TileUniqueId} is not a base");

        var tile = _conquestManager.StoredTile(info, request.Difficulty, request.TileUniqueId);
        if (tile == null || tile.TileState != TileState.FullyConquested)
            throw new WebAPIException(WebAPIErrorCode.ConquestNotFullyConquested, $"Base {request.TileUniqueId} not conquered");

        // No excel column caps a manage batch, so the ceiling is fixed here; a free tile manages once.
        var manageCount = Math.Clamp(request.ManageCount, 1, MaxManagePerRequest);
        if (tileExcel.ManageCostType == ParcelType.None || tileExcel.ManageCostAmount <= 0)
            manageCount = 1;

        if (tileExcel.ManageCostType != ParcelType.None && tileExcel.ManageCostAmount > 0)
        {
            await _parcelHandler.BuildParcel(db, account,
                new ParcelResult(tileExcel.ManageCostType, tileExcel.ManageCostId, (long)tileExcel.ManageCostAmount * manageCount),
                isConsume: true);
            _conquestManager.TrackConditionSpend(info, tileExcel.ManageCostType, tileExcel.ManageCostId,
                (long)tileExcel.ManageCostAmount * manageCount);
        }

        var all = new List<ParcelResult>();
        response.ClearParcels = [];
        for (int i = 0; i < manageCount; i++)
        {
            var run = new List<ParcelResult>();
            _conquestManager.RollRewardGroup(tileExcel.ConquestRewardId, run);
            all.AddRange(run);
            response.ClearParcels.Add(run.Select(x => new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = x.Type, Id = x.Id },
                Amount = x.Amount
            }).ToList());
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, all);
        db.ConquestInfos.Update(info);
        await db.SaveChangesAsync();

        response.ConquerBonusParcels = [];
        response.BonusParcels = [];
        response.ParcelResultDB = resolver.ParcelResult;
        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_UpgradeBase)]
    public async Task<ConquestUpgradeBaseResponse> UpgradeBase(
        SchaleDataContext db,
        ConquestUpgradeBaseRequest request,
        ConquestUpgradeBaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = _conquestManager.Require(db, account, request.EventContentId);

        var tileExcel = _conquestManager.RequireTile(info.EventContentId, request.TileUniqueId);
        if (tileExcel.TileType != ConquestTileType.Base)
            throw new WebAPIException(WebAPIErrorCode.ConquestInvalidTileType, $"Tile {request.TileUniqueId} is not a base");

        var tile = _conquestManager.StoredTile(info, request.Difficulty, request.TileUniqueId);
        if (tile == null || tile.TileState != TileState.FullyConquested)
            throw new WebAPIException(WebAPIErrorCode.ConquestNotFullyConquested, $"Base {request.TileUniqueId} not conquered");

        var (costType, costId, costAmount) = tile.Level switch
        {
            1 => (tileExcel.Upgrade2CostType, tileExcel.Upgrade2CostId, tileExcel.Upgrade2CostAmount),
            2 => (tileExcel.Upgrade3CostType, tileExcel.Upgrade3CostId, tileExcel.Upgrade3CostAmount),
            _ => throw new WebAPIException(WebAPIErrorCode.ConquestMaxUpgrade, $"Base {request.TileUniqueId} is max level")
        };

        var resolver = default(ParcelResolver);
        if (costType != ParcelType.None && costAmount > 0)
        {
            resolver = await _parcelHandler.BuildParcel(db, account,
                new ParcelResult(costType, costId, costAmount), isConsume: true);
            _conquestManager.TrackConditionSpend(info, costType, costId, costAmount);
        }

        tile.Level++;
        db.ConquestInfos.Update(info);
        await db.SaveChangesAsync();

        response.UpgradeRewards = [];
        response.ParcelResultDB = resolver?.ParcelResult ?? new ParcelResultDB();
        response.ConquestTileDB = tile;
        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));

        return response;
    }

    private ConquestStageSaveDB StartTileBattle(
        SchaleDataContext db, AccountDBServer account, ConquestInfoDBServer info,
        StageDifficulty difficulty, long tileUniqueId)
    {
        var tileExcel = _conquestManager.RequireTile(info.EventContentId, tileUniqueId);

        if (tileExcel.Step > _conquestManager.CurrentStep(info, difficulty))
            throw new WebAPIException(WebAPIErrorCode.ConquestStepNotOpened, $"Tile {tileUniqueId} is on step {tileExcel.Step}");
        if (_conquestManager.StoredTile(info, difficulty, tileUniqueId) != null)
            throw new WebAPIException(WebAPIErrorCode.ConquestAlreadyConquested, $"Tile {tileUniqueId} already taken");
        if (tileExcel.TileType != ConquestTileType.Battle)
            throw new WebAPIException(WebAPIErrorCode.ConquestInvalidTileType, $"Tile {tileUniqueId} has no battle");

        return _conquestManager.OpenTileBattle(db, account, info, difficulty, tileUniqueId, tileExcel.TileType);
    }

    private ConquestSummary BuildSummary(ConquestInfoDBServer info)
    {
        var tileExcels = _excelService.GetTable<ConquestTileExcelT>()
            .Where(x => x.EventId == info.EventContentId && x.Playable)
            .ToList();
        var currentStep = _conquestManager.CurrentStep(info, StageDifficulty.Normal);
        var conquered = info.Tiles
            .Where(x => x.Difficulty == StageDifficulty.Normal)
            .Select(x => x.TileUniqueId)
            .ToHashSet();

        return new ConquestSummary
        {
            EventContentId = info.EventContentId,
            Difficulty = StageDifficulty.Normal,
            ConquestStepSummaryDict = tileExcels
                .GroupBy(x => x.Step)
                .ToDictionary(g => g.Key, g => new ConquestStepSummary
                {
                    ConqueredTileCount = g.Count(x => conquered.Contains(x.Id)),
                    AllTileCount = g.Count(),
                    IsStepOpen = g.Key <= currentStep
                })
        };
    }
}
