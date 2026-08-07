using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Core.Math;
using Schale.MX.Logic.Battles;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ConquestHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ParcelHandler _parcelHandler;

    private readonly List<ConquestTileExcelT> _tileExcels;
    private readonly List<ConquestMapExcelT> _mapExcels;
    private readonly List<ConquestRewardExcelT> _rewardExcels;
    private readonly List<GachaElementExcelT> _gachaElementExcels;

    public ConquestHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _parcelHandler = parcelHandler;

        _tileExcels = excelService.GetTable<ConquestTileExcelT>();
        _mapExcels = excelService.GetTable<ConquestMapExcelT>();
        _rewardExcels = excelService.GetTable<ConquestRewardExcelT>();
        _gachaElementExcels = excelService.GetTable<GachaElementExcelT>();
    }

    [ProtocolHandler(Protocol.Conquest_GetInfo)]
    public async Task<ConquestGetInfoResponse> GetInfo(SchaleDataContext db, ConquestGetInfoRequest request, ConquestGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.IsFirstEnter = info == null;
        if (info == null)
        {
            info = new ConquestInfoDBServer { AccountServerId = account.ServerId, EventContentId = request.EventContentId };
            db.ConquestInfos.Add(info);
            await db.SaveChangesAsync();
        }

        response.ConquestInfoDB = ToInfoDB(info);
        response.ConquestedTileDBs = db.GetAccountConquestTiles(account.ServerId, request.EventContentId).AsEnumerable().Select(ToTileDB).ToList();
        response.ConquestEchelonDBs = info.Echelons;
        // every step opens at once - the StepOpenConditions in ConquestMapExcel gate on event-cycle progress that never advances here
        response.DifficultyToStepDict = _mapExcels.Where(x => x.EventContentId == request.EventContentId)
            .GroupBy(x => x.MapDifficulty)
            .ToDictionary(g => g.Key, g => g.Max(x => x.StepIndex));

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_Conquer)]
    public async Task<ConquestConquerResponse> Conquer(SchaleDataContext db, ConquestConquerRequest request, ConquestConquerResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (_, resolver, tile) = await ConquerTile(db, account, request.EventContentId, request.Difficulty, request.TileUniqueId, payCost: true);

        response.ParcelResultDB = resolver?.ParcelResult;
        response.ConquestTileDB = ToTileDB(tile);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ConquerWithBattleStart)]
    public async Task<ConquestConquerWithBattleStartResponse> ConquerWithBattleStart(SchaleDataContext db, ConquestConquerWithBattleStartRequest request, ConquestConquerWithBattleStartResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var tileExcel = _tileExcels.First(x => x.Id == request.TileUniqueId);

        // the cost goes at battle start; a defeat forfeits it, same as main-stage AP
        if (tileExcel.ConquestCostAmount > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, new ParcelResult(tileExcel.ConquestCostType, tileExcel.ConquestCostId, tileExcel.ConquestCostAmount), isConsume: true);
            response.ParcelResultDB = resolver.ParcelResult;
        }

        response.ConquestStageSaveDB = new ConquestStageSaveDB
        {
            AccountServerId = account.ServerId,
            EventContentId = request.EventContentId,
            Difficulty = request.Difficulty,
            TileUniqueId = request.TileUniqueId,
            ConquestTileType = tileExcel.TileType,
            LastEnterStageEchelonNumber = request.EchelonNumber ?? 0,
            CreateTime = account.GameSettings.ServerDateTime()
        };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ConquerWithBattleResult)]
    public async Task<ConquestConquerWithBattleResultResponse> ConquerWithBattleResult(SchaleDataContext db, ConquestConquerWithBattleResultRequest request, ConquestConquerWithBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.BattleSummary?.EndType == BattleEndType.Clear)
        {
            var (drops, resolver, tile) = await ConquerTile(db, account, request.EventContentId, request.Difficulty, request.TileUniqueId, payCost: false);

            response.ParcelResultDB = resolver?.ParcelResult;
            response.ConquestTileDB = ToTileDB(tile);
            response.StepAfterBattle = _tileExcels.First(x => x.Id == request.TileUniqueId).Step;
            response.DisplayParcelByRewardTag = new Dictionary<RewardTag, List<ParcelInfo>> { [RewardTag.Default] = ToParcelInfos(drops) };
        }

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_DeployEchelon)]
    public async Task<ConquestConquerDeployEchelonResponse> DeployEchelon(SchaleDataContext db, ConquestConquerDeployEchelonRequest request, ConquestConquerDeployEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        if (info == null)
        {
            info = new ConquestInfoDBServer { AccountServerId = account.ServerId, EventContentId = request.EventContentId };
            db.ConquestInfos.Add(info);
        }

        var echelon = info.Echelons.FirstOrDefault(x => x.Difficulty == request.Difficulty && x.TileUniqueId == request.TileUniqueId);
        if (echelon == null)
        {
            echelon = new ConquestEchelonDB { EventContentId = request.EventContentId, Difficulty = request.Difficulty, TileUniqueId = request.TileUniqueId };
            info.Echelons.Add(echelon);
        }
        else
        {
            info.EchelonChangeCount++;
        }

        echelon.EchelonDB = request.EchelonDB;
        echelon.AssistUseInfo = request.ClanAssistUseInfo;
        echelon.AssistCharacterUniqueId = request.ClanAssistUseInfo?.CharacterDBId ?? 0;

        await db.SaveChangesAsync();

        response.ConquestEchelonDBs = info.Echelons;
        response.ConquestInfoDB = ToInfoDB(info);

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_NormalizeEchelon)]
    public async Task<ConquestNormalizeEchelonResponse> NormalizeEchelon(SchaleDataContext db, ConquestNormalizeEchelonRequest request, ConquestNormalizeEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestEchelonDB = info?.Echelons.FirstOrDefault(x => x.Difficulty == request.Difficulty && x.TileUniqueId == request.TileUniqueId);

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ManageBase)]
    public async Task<ConquestManageBaseResponse> ManageBase(SchaleDataContext db, ConquestManageBaseRequest request, ConquestManageBaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var tileExcel = _tileExcels.First(x => x.Id == request.TileUniqueId);

        ParcelResolver? resolver = null;
        if (tileExcel.ManageCostAmount > 0)
            resolver = await _parcelHandler.BuildParcel(db, account, new ParcelResult(tileExcel.ManageCostType, tileExcel.ManageCostId, tileExcel.ManageCostAmount * request.ManageCount), isConsume: true);

        var clearParcels = new List<List<ParcelInfo>>();
        for (int i = 0; i < request.ManageCount; i++)
        {
            var drops = RollTileRewards(tileExcel.ConquestRewardId);
            if (drops.Count > 0)
                resolver = await _parcelHandler.BuildParcel(db, account, drops, resolver?.ParcelResult);
            clearParcels.Add(ToParcelInfos(drops));
        }

        response.ClearParcels = clearParcels;
        response.ConquerBonusParcels = [];
        response.BonusParcels = [];
        response.ParcelResultDB = resolver?.ParcelResult;

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_UpgradeBase)]
    public async Task<ConquestUpgradeBaseResponse> UpgradeBase(SchaleDataContext db, ConquestUpgradeBaseRequest request, ConquestUpgradeBaseResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var tile = db.GetAccountConquestTiles(account.ServerId, request.EventContentId).First(x => x.Difficulty == request.Difficulty && x.TileUniqueId == request.TileUniqueId);
        var tileExcel = _tileExcels.First(x => x.Id == request.TileUniqueId);

        if (tile.Level < 3)
        {
            var costType = tile.Level == 1 ? tileExcel.Upgrade2CostType : tileExcel.Upgrade3CostType;
            var costId = tile.Level == 1 ? tileExcel.Upgrade2CostId : tileExcel.Upgrade3CostId;
            var costAmount = tile.Level == 1 ? tileExcel.Upgrade2CostAmount : tileExcel.Upgrade3CostAmount;
            if (costAmount > 0)
            {
                var resolver = await _parcelHandler.BuildParcel(db, account, new ParcelResult(costType, costId, costAmount), isConsume: true);
                response.ParcelResultDB = resolver.ParcelResult;
            }
            tile.Level++;
            await db.SaveChangesAsync();
        }

        response.UpgradeRewards = [];
        response.ConquestTileDB = ToTileDB(tile);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_TakeEventObject)]
    public async Task<ConquestTakeEventObjectResponse> TakeEventObject(SchaleDataContext db, ConquestTakeEventObjectRequest request, ConquestTakeEventObjectResponse response)
    {
        // event objects are never spawned here, so the client has no object id it could legitimately send
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Conquest_EventObjectBattleStart)]
    public async Task<ConquestEventObjectBattleStartResponse> EventObjectBattleStart(SchaleDataContext db, ConquestEventObjectBattleStartRequest request, ConquestEventObjectBattleStartResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Conquest_EventObjectBattleResult)]
    public async Task<ConquestEventObjectBattleResultResponse> EventObjectBattleResult(SchaleDataContext db, ConquestEventObjectBattleResultRequest request, ConquestEventObjectBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ErosionBattleStart)]
    public async Task<ConquestErosionBattleStartResponse> ErosionBattleStart(SchaleDataContext db, ConquestErosionBattleStartRequest request, ConquestErosionBattleStartResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ErosionBattleResult)]
    public async Task<ConquestErosionBattleResultResponse> ErosionBattleResult(SchaleDataContext db, ConquestErosionBattleResultRequest request, ConquestErosionBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_ReceiveCalculateRewards)]
    public async Task<ConquestReceiveRewardsResponse> ReceiveCalculateRewards(SchaleDataContext db, ConquestReceiveRewardsRequest request, ConquestReceiveRewardsResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // calculate rewards pay out of the event-wide consumption gauge on official; nothing feeds the gauge here, so there is never anything to hand over
        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };
        response.ConquestTileDBs = db.GetAccountConquestTiles(account.ServerId, request.EventContentId).AsEnumerable().Select(ToTileDB).ToList();

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_Check)]
    public async Task<ConquestCheckResponse> Check(SchaleDataContext db, ConquestCheckRequest request, ConquestCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var tiles = db.GetAccountConquestTiles(account.ServerId, request.EventContentId).ToList();
        var difficulty = tiles.Count > 0
            ? tiles.OrderByDescending(x => x.CreateTime).First().Difficulty
            : _mapExcels.Where(x => x.EventContentId == request.EventContentId).Select(x => x.MapDifficulty).FirstOrDefault();

        var stepDict = new Dictionary<int, ConquestStepSummary>();
        foreach (var step in _tileExcels.Where(x => x.EventId == request.EventContentId && x.Playable).GroupBy(x => x.Step))
        {
            stepDict[step.Key] = new ConquestStepSummary
            {
                AllTileCount = step.Count(),
                ConqueredTileCount = tiles.Count(t => t.Difficulty == difficulty && step.Any(s => s.Id == t.TileUniqueId)),
                IsStepOpen = true
            };
        }

        response.ConquestSummary = new ConquestSummary
        {
            EventContentId = request.EventContentId,
            Difficulty = difficulty,
            ConquestStepSummaryDict = stepDict
        };
        response.CanReceiveCalculateReward = false;

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryGetInfo)]
    public async Task<ConquestMainStoryGetInfoResponse> MainStoryGetInfo(SchaleDataContext db, ConquestMainStoryGetInfoRequest request, ConquestMainStoryGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.IsFirstEnter = info == null;
        if (info == null)
        {
            info = new ConquestInfoDBServer { AccountServerId = account.ServerId, EventContentId = request.EventContentId };
            db.ConquestInfos.Add(info);
            await db.SaveChangesAsync();
        }

        response.ConquestInfoDB = ToInfoDB(info);
        response.ConquestedTileDBs = db.GetAccountConquestTiles(account.ServerId, request.EventContentId).AsEnumerable().Select(ToTileDB).ToList();
        // every step opens at once - the StepOpenConditions in ConquestMapExcel gate on event-cycle progress that never advances here
        response.DifficultyToStepDict = _mapExcels.Where(x => x.EventContentId == request.EventContentId)
            .GroupBy(x => x.MapDifficulty)
            .ToDictionary(g => g.Key, g => g.Max(x => x.StepIndex));

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryConquer)]
    public async Task<ConquestMainStoryConquerResponse> MainStoryConquer(SchaleDataContext db, ConquestMainStoryConquerRequest request, ConquestMainStoryConquerResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (_, resolver, tile) = await ConquerTile(db, account, request.EventContentId, request.Difficulty, request.TileUniqueId, payCost: true);

        response.ParcelResultDB = resolver?.ParcelResult;
        response.ConquestTileDB = ToTileDB(tile);

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryConquerWithBattleStart)]
    public async Task<ConquestMainStoryConquerWithBattleStartResponse> MainStoryConquerWithBattleStart(SchaleDataContext db, ConquestMainStoryConquerWithBattleStartRequest request, ConquestMainStoryConquerWithBattleStartResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var tileExcel = _tileExcels.First(x => x.Id == request.TileUniqueId);

        // the cost goes at battle start; a defeat forfeits it, same as main-stage AP
        if (tileExcel.ConquestCostAmount > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, new ParcelResult(tileExcel.ConquestCostType, tileExcel.ConquestCostId, tileExcel.ConquestCostAmount), isConsume: true);
            response.ParcelResultDB = resolver.ParcelResult;
        }

        response.ConquestStageSaveDB = new ConquestStageSaveDB
        {
            AccountServerId = account.ServerId,
            EventContentId = request.EventContentId,
            Difficulty = request.Difficulty,
            TileUniqueId = request.TileUniqueId,
            ConquestTileType = tileExcel.TileType,
            LastEnterStageEchelonNumber = request.EchelonNumber ?? 0,
            CreateTime = account.GameSettings.ServerDateTime()
        };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryConquerWithBattleResult)]
    public async Task<ConquestMainStoryConquerWithBattleResultResponse> MainStoryConquerWithBattleResult(SchaleDataContext db, ConquestMainStoryConquerWithBattleResultRequest request, ConquestMainStoryConquerWithBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.BattleSummary?.EndType == BattleEndType.Clear)
        {
            var (drops, resolver, tile) = await ConquerTile(db, account, request.EventContentId, request.Difficulty, request.TileUniqueId, payCost: false);

            response.ParcelResultDB = resolver?.ParcelResult;
            response.ConquestTileDB = ToTileDB(tile);
            response.StepAfterBattle = _tileExcels.First(x => x.Id == request.TileUniqueId).Step;
            response.DisplayParcelByRewardTag = new Dictionary<RewardTag, List<ParcelInfo>> { [RewardTag.Default] = ToParcelInfos(drops) };
        }

        var info = db.ConquestInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        response.ConquestInfoDB = info != null ? ToInfoDB(info) : new ConquestInfoDB { EventContentId = request.EventContentId };

        return response;
    }

    [ProtocolHandler(Protocol.Conquest_MainStoryCheck)]
    public async Task<ConquestMainStoryCheckResponse> MainStoryCheck(SchaleDataContext db, ConquestMainStoryCheckRequest request, ConquestMainStoryCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var tiles = db.GetAccountConquestTiles(account.ServerId, request.EventContentId).ToList();
        var difficulty = tiles.Count > 0
            ? tiles.OrderByDescending(x => x.CreateTime).First().Difficulty
            : _mapExcels.Where(x => x.EventContentId == request.EventContentId).Select(x => x.MapDifficulty).FirstOrDefault();

        var stepDict = new Dictionary<int, ConquestMainStoryStepSummary>();
        foreach (var step in _tileExcels.Where(x => x.EventId == request.EventContentId && x.Playable).GroupBy(x => x.Step))
        {
            stepDict[step.Key] = new ConquestMainStoryStepSummary
            {
                AllTileCount = step.Count(),
                ConqueredTileCount = tiles.Count(t => t.Difficulty == difficulty && step.Any(s => s.Id == t.TileUniqueId)),
                IsStepOpen = true
            };
        }

        response.ConquestMainStorySummary = new ConquestMainStorySummary
        {
            EventContentId = request.EventContentId,
            Difficulty = difficulty,
            ConquestStepSummaryDict = stepDict
        };

        return response;
    }

    private async Task<(List<ParcelResult>, ParcelResolver?, ConquestTileDBServer)> ConquerTile(SchaleDataContext db, AccountDBServer account, long eventContentId, StageDifficulty difficulty, long tileUniqueId, bool payCost)
    {
        var tileExcel = _tileExcels.First(x => x.Id == tileUniqueId);

        ParcelResolver? resolver = null;
        if (payCost && tileExcel.ConquestCostAmount > 0)
            resolver = await _parcelHandler.BuildParcel(db, account, new ParcelResult(tileExcel.ConquestCostType, tileExcel.ConquestCostId, tileExcel.ConquestCostAmount), isConsume: true);

        var drops = RollTileRewards(tileExcel.ConquestRewardId);
        if (drops.Count > 0)
            resolver = await _parcelHandler.BuildParcel(db, account, drops, resolver?.ParcelResult);

        var tile = db.GetAccountConquestTiles(account.ServerId, eventContentId).FirstOrDefault(x => x.Difficulty == difficulty && x.TileUniqueId == tileUniqueId);
        if (tile == null)
        {
            tile = new ConquestTileDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = eventContentId,
                Difficulty = difficulty,
                TileUniqueId = tileUniqueId,
                Level = 1,
                CreateTime = account.GameSettings.ServerDateTime()
            };
            db.ConquestTiles.Add(tile);
        }
        await db.SaveChangesAsync();

        return (drops, resolver, tile);
    }

    // gacha group rows are IsDisplayed=false in the excel and would show up as blank cells, so they are rolled to concrete items before anything renders them
    private List<ParcelResult> RollTileRewards(long rewardGroupId)
    {
        var rolled = new List<ParcelResult>();
        foreach (var reward in _rewardExcels.Where(x => x.GroupId == rewardGroupId))
        {
            if (reward.RewardProb < 10000 && Random.Shared.Next(10000) >= reward.RewardProb) continue;
            rolled.Add(new ParcelResult(reward.RewardParcelType, reward.RewardId, reward.RewardAmount));
        }
        return rolled.GenerateGachaGroup(_gachaElementExcels);
    }

    private static ConquestTileDB ToTileDB(ConquestTileDBServer tile) => new()
    {
        EventContentId = tile.EventContentId,
        Difficulty = tile.Difficulty,
        TileUniqueId = tile.TileUniqueId,
        TileState = TileState.FullyConquested,
        Level = tile.Level,
        CreateTime = tile.CreateTime,
        // no star bookkeeping server-side; a conquered tile always reports a full record
        IsAnyStarClear = true,
        IsThreeStarClear = true,
        BestStarRecord = 3,
        StarFlags = [true, true, true]
    };

    private static ConquestInfoDB ToInfoDB(ConquestInfoDBServer info) => new()
    {
        EventContentId = info.EventContentId,
        EchelonChangeCount = info.EchelonChangeCount
    };

    private static List<ParcelInfo> ToParcelInfos(List<ParcelResult> parcels)
        => parcels.Select(r => new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = r.Type, Id = r.Id },
            Amount = r.Amount,
            Multiplier = BasisPoint.One,
            Probability = BasisPoint.One
        }).ToList();
}
