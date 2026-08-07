using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MiniGameRoadPuzzleHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;

    public MiniGameRoadPuzzleHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.MiniGame_RoadPuzzleGetInfo)]
    public async Task<MiniGameRoadPuzzleGetInfoResponse> GetInfo(
        SchaleDataContext db,
        MiniGameRoadPuzzleGetInfoRequest request,
        MiniGameRoadPuzzleGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        ResolveInfo(request.EventContentId);

        var row = await db.MiniGameRoadPuzzleSaves.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        if (row == null)
        {
            row = new MiniGameRoadPuzzleSaveDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId,
                SaveDB = StartBoard(request.EventContentId, StartRound(request.EventContentId))
            };

            db.MiniGameRoadPuzzleSaves.Add(row);
            await db.SaveChangesAsync();
        }

        response.SaveDB = row.SaveDB;

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_RoadPuzzleTilePlace)]
    public async Task<MiniGameRoadPuzzleTilePlaceResponse> TilePlace(
        SchaleDataContext db,
        MiniGameRoadPuzzleTilePlaceRequest request,
        MiniGameRoadPuzzleTilePlaceResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var board = await LoadBoard(db, account.ServerId, request.EventContentId, request.UniqueId, request.Round, WebAPIErrorCode.MiniGameRoadPuzzleInvalidTilePlacement);

        var tile = request.RailTileToPlace;
        if (tile == null || tile.Type == RoadPuzzleRailTileType.None)
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleInvalidTilePlacement, "Tile placement carried no rail tile");

        if (board.remainingTiles == null || !board.remainingTiles.TryGetValue(tile.Type, out var left) || left <= 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleInvalidTilePlacement, $"No {tile.Type} rails left on map {board.UniqueId}");

        board.placedRailTiles ??= [];
        if (board.placedRailTiles.Any(x => SameHex(x.Location, tile.Location)))
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleInvalidTilePlacement, $"A rail is already placed at {tile.Location.x},{tile.Location.y},{tile.Location.z}");

        var goods = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == info.CostGoodsId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"GoodsExcel {info.CostGoodsId} not found");

        var costParcels = ParcelResult.ConvertParcelResult(goods.ConsumeParcelType, goods.ConsumeParcelId, goods.ConsumeParcelAmount);
        var costResolver = await _parcelHandler.BuildParcel(db, account, costParcels, isConsume: true);

        tile.Uid = board.placedRailTiles.Count == 0 ? 1 : board.placedRailTiles.Max(x => x.Uid) + 1;
        board.placedRailTiles.Add(tile);

        if (left <= 1)
            board.remainingTiles.Remove(tile.Type);
        else
            board.remainingTiles[tile.Type] = left - 1;

        board.recentRandomRailTile = Draw(board.remainingTiles, tile.Uid + 1);
        board.isTrainReadyToDepart = IsConnectedChain(board.placedRailTiles);

        await db.SaveChangesAsync();

        response.RailTileToPlace = tile;
        response.SaveDB = board;
        response.ParcelResultDB = costResolver.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_RoadPuzzleSaveStage)]
    public async Task<MiniGameRoadPuzzleSaveStageResponse> SaveStage(
        SchaleDataContext db,
        MiniGameRoadPuzzleSaveStageRequest request,
        MiniGameRoadPuzzleSaveStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var board = await LoadBoard(db, account.ServerId, request.EventContentId, request.UniqueId, request.Round, WebAPIErrorCode.MiniGameRoadPuzzleCannotSave);

        var submitted = request.placeRailTiles ?? [];
        var stored = board.placedRailTiles ?? [];

        // the save is a commit of rotations and repositions, never an acquisition - the tile set has to be the one already paid for at 35030, matched by type since the whole point of the save is that locations moved
        if (submitted.Count != stored.Count)
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleCannotSave, $"Save carried {submitted.Count} rails against {stored.Count} paid for");

        var storedByType = stored.GroupBy(x => x.Type).ToDictionary(g => g.Key, g => g.Count());
        foreach (var group in submitted.GroupBy(x => x.Type))
        {
            if (!storedByType.TryGetValue(group.Key, out var count) || count != group.Count())
                throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleCannotSave, $"Save carried {group.Count()} {group.Key} rails that were never bought");
        }

        if (submitted.Select(x => (x.Location.x, x.Location.y, x.Location.z)).Distinct().Count() != submitted.Count)
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleCannotSave, "Save stacked two rails on one hex");

        board.placedRailTiles = submitted;
        board.isTrainReadyToDepart = IsConnectedChain(submitted);

        await db.SaveChangesAsync();

        response.SaveDB = board;

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_RoadPuzzleClearStage)]
    public async Task<MiniGameRoadPuzzleClearStageResponse> ClearStage(
        SchaleDataContext db,
        MiniGameRoadPuzzleClearStageRequest request,
        MiniGameRoadPuzzleClearStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = ResolveInfo(request.EventContentId);
        var board = await LoadBoard(db, account.ServerId, request.EventContentId, request.UniqueId, request.Round, WebAPIErrorCode.MiniGameRoadPuzzleAlreadyCleared);

        var round = _excelService.GetTable<MinigameRoadPuzzleRoadRoundExcelT>().FirstOrDefault(x => x.EventContentId == request.EventContentId && x.Round == board.Round)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MinigameRoadPuzzleRoadRoundExcel round {board.Round} of event {request.EventContentId} not found");

        if (!round.IsLoop && await db.MiniGameRoadPuzzleHistories.AnyAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId && x.UniqueId == request.UniqueId && x.Round == request.Round))
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleAlreadyCleared, $"Round {request.Round} of event {request.EventContentId} is already cleared");

        var remaining = board.remainingTiles?.Values.Sum() ?? 0;

        var goods = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == info.CostGoodsId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"GoodsExcel {info.CostGoodsId} not found");

        if (request.IsSkip)
        {
            if (info.InstantClearRound > 0 && request.Round < info.InstantClearRound)
                throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleCannotTrainDeparture, $"Round {request.Round} is below the instant clear round {info.InstantClearRound}");

            // skipping buys the rest of the board outright, so it costs exactly what laying every remaining rail would have
            var skipCost = ParcelResult.ConvertParcelResult(goods.ConsumeParcelType, goods.ConsumeParcelId, goods.ConsumeParcelAmount.Select(x => x * remaining).ToList());
            await _parcelHandler.BuildParcel(db, account, skipCost, isConsume: true);
        }
        else if (!board.isTrainReadyToDepart)
        {
            throw new WebAPIException(WebAPIErrorCode.MiniGameRoadPuzzleCannotTrainDeparture, $"The rails on map {board.UniqueId} do not form a track");
        }

        var rewards = new List<ParcelResult>();

        var roundReward = _excelService.GetTable<MiniGameRoadPuzzleRewardExcelT>().FirstOrDefault(x => x.EventContentId == request.EventContentId && x.UniqueId == round.RoundReward);
        if (roundReward != null)
            rewards.AddRange(ParcelResult.ConvertParcelResult(roundReward.RewardParcelType, roundReward.RewardParcelId, roundReward.RewardParcelAmount));

        var additional = _excelService.GetTable<MiniGameRoadPuzzleAdditionalRewardExcelT>();
        var additionalIds = round.AdditionalRewardID ?? [];
        var additionalAmounts = round.AdditionalRewardAmount ?? [];
        for (int i = 0; i < additionalIds.Count && i < additionalAmounts.Count; i++)
        {
            var row = additional.FirstOrDefault(x => x.EventContentId == request.EventContentId && x.UniqueId == additionalIds[i]);
            if (row != null)
                rewards.Add(new ParcelResult(row.RewardParcelType, row.RewardParcelId, row.RewardParcelAmount * additionalAmounts[i]));
        }

        // unspent rails convert rather than being lost, which is what makes laying the shortest track worth more than laying every rail
        if (remaining > 0)
        {
            var railSet = _excelService.GetTable<MiniGameRoadPuzzleRailSetRewardExcelT>().FirstOrDefault(x => x.EventContentId == request.EventContentId && x.UniqueId == info.RailSetRewardId);
            if (railSet != null)
                rewards.AddRange(ParcelResult.ConvertParcelResult(railSet.RewardParcelType, railSet.RewardParcelId, railSet.RewardParcelAmount.Select(x => x * remaining).ToList()));
        }

        var summarized = rewards
            .GroupBy(x => (x.Type, x.Id))
            .Select(g => new ParcelResult(g.Key.Type, g.Key.Id, g.Sum(x => x.Amount)))
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Id)
            .ToList();

        if (summarized.Count > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, summarized);
            response.ParcelResultDB = resolver.ParcelResult;
        }
        else
        {
            response.ParcelResultDB = new ParcelResultDB
            {
                AccountDB = account.ToMap(_mapper),
                AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
            };
        }

        db.MiniGameRoadPuzzleHistories.Add(new MiniGameRoadPuzzleHistoryDBServer
        {
            AccountServerId = account.ServerId,
            EventContentId = request.EventContentId,
            UniqueId = request.UniqueId,
            Round = request.Round,
            ClearDate = account.GameSettings.ServerDateTime()
        });

        var save = await db.MiniGameRoadPuzzleSaves.FirstAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        save.SaveDB = StartBoard(request.EventContentId, NextRound(request.EventContentId, round));

        await db.SaveChangesAsync();

        response.IsSkip = request.IsSkip;

        return response;
    }

    private MiniGameRoadPuzzleInfoExcelT ResolveInfo(long eventContentId)
        => _excelService.GetTable<MiniGameRoadPuzzleInfoExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentNotOpen, $"MiniGameRoadPuzzleInfoExcel {eventContentId} not found");

    private async Task<RoadPuzzleBoardSaveDB> LoadBoard(SchaleDataContext db, long accountServerId, long eventContentId, long uniqueId, long round, WebAPIErrorCode code)
    {
        var row = await db.MiniGameRoadPuzzleSaves.FirstOrDefaultAsync(x => x.AccountServerId == accountServerId && x.EventContentId == eventContentId);
        if (row?.SaveDB == null)
            throw new WebAPIException(code, $"No road puzzle board for event {eventContentId}");

        // the client fills UniqueId and Round off its own live SaveDB, so drift here means it is looking at a different map or round than the one that has been paid into
        if (row.SaveDB.UniqueId != uniqueId || row.SaveDB.Round != round)
            throw new WebAPIException(code, $"Board is on map {row.SaveDB.UniqueId} round {row.SaveDB.Round}, request claimed {uniqueId}/{round}");

        return row.SaveDB;
    }

    private MinigameRoadPuzzleRoadRoundExcelT StartRound(long eventContentId)
    {
        var rounds = _excelService.GetTable<MinigameRoadPuzzleRoadRoundExcelT>().Where(x => x.EventContentId == eventContentId).ToList();
        if (rounds.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentNotOpen, $"Event {eventContentId} has no road puzzle rounds");

        var start = _excelService.GetTable<ConstMinigameRoadPuzzleExcelT>().FirstOrDefault()?.StartStageIndex ?? 1;
        return rounds.FirstOrDefault(x => x.Round == start) ?? rounds.OrderBy(x => x.Round).First();
    }

    private MinigameRoadPuzzleRoadRoundExcelT NextRound(long eventContentId, MinigameRoadPuzzleRoadRoundExcelT current)
    {
        var rounds = _excelService.GetTable<MinigameRoadPuzzleRoadRoundExcelT>().Where(x => x.EventContentId == eventContentId).ToList();
        var next = rounds.FirstOrDefault(x => x.Round == current.Round + 1);
        if (next != null)
            return next;

        // the last authored round is flagged IsLoop and its map group carries five maps instead of one, so the tail repeats on a fresh draw rather than ending
        if (current.IsLoop)
            return current;

        var loop = _excelService.GetTable<ConstMinigameRoadPuzzleExcelT>().FirstOrDefault()?.LoopStageIndex ?? 0;
        return rounds.FirstOrDefault(x => x.Round == loop) ?? current;
    }

    private RoadPuzzleBoardSaveDB StartBoard(long eventContentId, MinigameRoadPuzzleRoadRoundExcelT round)
    {
        var maps = _excelService.GetTable<MinigameRoadPuzzleMapExcelT>().Where(x => x.EventContentId == eventContentId && x.MapGroupId == round.MapGroupId).ToList();
        if (maps.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"MinigameRoadPuzzleMapExcel group {round.MapGroupId} of event {eventContentId} not found");

        var map = maps[Random.Shared.Next(maps.Count)];
        var railTiles = _excelService.GetTable<MinigameRoadPuzzleRailTileExcelT>().Where(x => x.EventContentId == eventContentId).ToList();

        var remaining = new Dictionary<RoadPuzzleRailTileType, int>();
        var available = map.AvailableRailTile ?? [];
        var amounts = map.AvailableRailTileAmount ?? [];
        for (int i = 0; i < available.Count && i < amounts.Count; i++)
        {
            var rail = railTiles.FirstOrDefault(x => x.UniqueId == available[i]);
            if (rail == null)
                continue;

            remaining.TryGetValue(rail.RailTileType, out var have);
            remaining[rail.RailTileType] = have + (int)amounts[i];
        }

        var board = new RoadPuzzleBoardSaveDB
        {
            UniqueId = map.UniqueId,
            Round = round.Round,
            remainingTiles = remaining,
            placedRailTiles = [],
            rewardTiles = [],
            isTrainReadyToDepart = false
        };

        board.recentRandomRailTile = Draw(remaining, 1);

        return board;
    }

    // the draw is server-side so the tile the player is holding is not something the client can pick for itself
    private static RoadPuzzleRailTileData? Draw(Dictionary<RoadPuzzleRailTileType, int>? remaining, long uid)
    {
        if (remaining == null)
            return null;

        var total = remaining.Values.Sum();
        if (total <= 0)
            return null;

        var roll = Random.Shared.Next(total);
        foreach (var pair in remaining)
        {
            roll -= pair.Value;
            if (roll < 0)
                return new RoadPuzzleRailTileData { Type = pair.Key, Uid = uid };
        }

        return null;
    }

    private static bool SameHex(Schale.MX.Campaign.HexLocation a, Schale.MX.Campaign.HexLocation b)
        => a.x == b.x && a.y == b.y && a.z == b.z;

    // the map itself - start tile, end tile and the rails already laid on it - is an addressable the client loads locally and no excel row exposes, so the server cannot verify a start-to-end route. What it can verify is that the paid rails form one unbranched run, which is what a track looks like and what a scattering of speculatively placed tiles does not.
    private static bool IsConnectedChain(List<RoadPuzzleRailTileData> tiles)
    {
        // a lone rail passes every shape test there is, so it cannot count as a track
        if (tiles.Count < 2)
            return false;

        var neighbours = tiles.Select(_ => new List<int>()).ToList();
        for (int i = 0; i < tiles.Count; i++)
        {
            for (int j = i + 1; j < tiles.Count; j++)
            {
                var a = tiles[i].Location;
                var b = tiles[j].Location;
                if ((Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) + Math.Abs(a.z - b.z)) / 2 != 1)
                    continue;

                neighbours[i].Add(j);
                neighbours[j].Add(i);
            }
        }

        if (neighbours.Any(x => x.Count > 2))
            return false;

        var seen = new HashSet<int> { 0 };
        var stack = new Stack<int>();
        stack.Push(0);
        while (stack.Count > 0)
        {
            foreach (var next in neighbours[stack.Pop()])
            {
                if (seen.Add(next))
                    stack.Push(next);
            }
        }

        return seen.Count == tiles.Count;
    }
}
