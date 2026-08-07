using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.MX.Core.Math;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EventContentPlayHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly EventContentCollectionService _collectionService;
    private readonly MiniGameMissionService _missionService;

    public EventContentPlayHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        EventContentCollectionService collectionService,
        MiniGameMissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
        _collectionService = collectionService;
        _missionService = missionService;
    }

    private List<ParcelResult> GoodsCost(long goodsId, long multiplier)
    {
        var goods = _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == goodsId)
            ?? throw new WebAPIException(WebAPIErrorCode.ShopGoodsNotFound, $"Goods {goodsId} not found");

        var cost = new List<ParcelResult>();
        for (int i = 0; i < (goods.ConsumeParcelType?.Count ?? 0); i++)
            cost.Add(new ParcelResult(goods.ConsumeParcelType![i], goods.ConsumeParcelId![i], goods.ConsumeParcelAmount![i] * multiplier));

        return cost;
    }

    private static EventContentDiceRaceDB DiceRaceDB(EventContentPlayDBServer play)
    {
        return new EventContentDiceRaceDB
        {
            EventContentId = play.EventContentId,
            Node = play.DiceRaceNode,
            LapCount = play.DiceRaceLapCount,
            DiceRollCount = play.DiceRaceRollCount,
            ReceiveRewardLapCount = play.DiceRaceReceivedLapCount
        };
    }

    private List<EventContentDiceRaceNodeExcelT> TrackNodes(long eventContentId)
    {
        var nodes = _excelService.GetTable<EventContentDiceRaceNodeExcelT>()
            .Where(x => x.EventContentId == eventContentId)
            .OrderBy(x => x.NodeId)
            .ToList();

        if (nodes.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentDiceRaceDataNotFound, $"Event {eventContentId} has no dice race track");

        return nodes;
    }

    // One entry per stop, in the order the pawn makes them: MoveAmount is how far it walks and Rewards is what the node it lands on pays.
    // The client stops reading at the first entry carrying rewards, which is why a MoveForwardNode has to be the only kind with an empty list.
    private static List<EventContentDiceResult> WalkTrack(EventContentPlayDBServer play, List<EventContentDiceRaceNodeExcelT> nodes, int move, List<ParcelResult> grants)
    {
        var results = new List<EventContentDiceResult>();

        while (true)
        {
            var next = play.DiceRaceNode + move;
            if (next >= nodes.Count)
                play.DiceRaceLapCount++;
            play.DiceRaceNode = next % nodes.Count;

            var node = nodes[(int)play.DiceRaceNode];
            var rewards = new List<ParcelInfo>();
            var rewardCount = new[] { node.RewardParcelType?.Count ?? 0, node.RewardParcelId?.Count ?? 0, node.RewardAmount?.Count ?? 0 }.Min();
            for (int i = 0; i < rewardCount; i++)
            {
                grants.Add(new ParcelResult(node.RewardParcelType![i], node.RewardParcelId![i], node.RewardAmount![i]));
                rewards.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = node.RewardParcelType[i], Id = node.RewardParcelId[i] },
                    Amount = node.RewardAmount![i],
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One
                });
            }

            results.Add(new EventContentDiceResult { Index = results.Count, MoveAmount = move, Rewards = rewards });

            if (node.EventContentDiceRaceNodeType != EventContentDiceRaceNodeType.MoveForwardNode)
                return results;

            move = node.MoveForwardTypeArg;
        }
    }

    [ProtocolHandler(Protocol.EventContent_DiceRaceLobby)]
    public async Task<EventContentDiceRaceLobbyResponse> DiceRaceLobby(
        SchaleDataContext db,
        EventContentDiceRaceLobbyRequest request,
        EventContentDiceRaceLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        await db.SaveChangesAsync();

        response.DiceRaceDB = DiceRaceDB(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_DiceRaceRoll)]
    public async Task<EventContentDiceRaceRollResponse> DiceRaceRoll(
        SchaleDataContext db,
        EventContentDiceRaceRollRequest request,
        EventContentDiceRaceRollResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var race = _excelService.GetTable<EventContentDiceRaceExcelT>().FirstOrDefault(x => x.EventContentId == request.EventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentDiceRaceDataNotFound, $"Event {request.EventContentId} has no dice race");

        var nodes = TrackNodes(request.EventContentId);

        var faces = _excelService.GetTable<EventContentDiceRaceProbExcelT>()
            .Where(x => x.EventContentId == request.EventContentId
                && x.EventContentDiceRaceResultType >= EventContentDiceRaceResultType.DiceResult1
                && x.EventContentDiceRaceResultType <= EventContentDiceRaceResultType.DiceResult6)
            .ToList();

        var roll = Random.Shared.Next(faces.Sum(x => x.Prob));
        var face = faces[^1];
        foreach (var candidate in faces)
        {
            roll -= candidate.Prob;
            if (roll < 0)
            {
                face = candidate;
                break;
            }
        }

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        var lapsBefore = play.DiceRaceLapCount;
        play.DiceRaceRollCount++;

        var grants = new List<ParcelResult>();
        response.DiceResults = WalkTrack(play, nodes, face.DiceResult, grants);

        var consumed = (await _parcelHandler.BuildParcel(db, account, GoodsCost(race.DiceCostGoodsId, 1), isConsume: true)).ParcelResult;
        response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants, consumed)).ParcelResult;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DiceRaceUseDiceCount);
        if (play.DiceRaceLapCount > lapsBefore)
            _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DiceRaceFinishLapCount, play.DiceRaceLapCount - lapsBefore);

        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.DiceRaceConsumeDiceCount);

        await db.SaveChangesAsync();

        response.DiceRaceDB = DiceRaceDB(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_DiceRaceUseItem)]
    public async Task<EventContentDiceRaceUseItemResponse> DiceRaceUseItem(
        SchaleDataContext db,
        EventContentDiceRaceUseItemRequest request,
        EventContentDiceRaceUseItemResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // only the Fixed faces are item rolls - the DiceResult1-6 rows belong to DiceRaceRoll and carry no CostItem, so matching one here walks the track for free
        if (request.DiceRaceResultType < EventContentDiceRaceResultType.DiceResultFixed1 || request.DiceRaceResultType > EventContentDiceRaceResultType.DiceResultFixed6)
            throw new WebAPIException(WebAPIErrorCode.EventContentDiceRaceInvalidDiceRaceResultType, $"{request.DiceRaceResultType} is not an item roll");

        var nodes = TrackNodes(request.EventContentId);

        var fixedFace = _excelService.GetTable<EventContentDiceRaceProbExcelT>()
            .FirstOrDefault(x => x.EventContentId == request.EventContentId && x.EventContentDiceRaceResultType == request.DiceRaceResultType)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentDiceRaceInvalidDiceRaceResultType, $"Event {request.EventContentId} has no {request.DiceRaceResultType} roll");

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        var lapsBefore = play.DiceRaceLapCount;
        play.DiceRaceRollCount++;

        var grants = new List<ParcelResult>();
        response.DiceResults = WalkTrack(play, nodes, fixedFace.DiceResult, grants);

        ParcelResultDB? consumed = null;
        if (fixedFace.CostItemAmount > 0)
            consumed = (await _parcelHandler.BuildParcel(db, account, [new ParcelResult(ParcelType.Item, fixedFace.CostItemId, fixedFace.CostItemAmount)], isConsume: true)).ParcelResult;

        response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants, consumed)).ParcelResult;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DiceRaceUseDiceCount);
        if (play.DiceRaceLapCount > lapsBefore)
            _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_DiceRaceFinishLapCount, play.DiceRaceLapCount - lapsBefore);

        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.DiceRaceConsumeDiceCount);

        await db.SaveChangesAsync();

        response.DiceRaceDB = DiceRaceDB(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_DiceRaceLapReward)]
    public async Task<EventContentDiceRaceLapRewardResponse> DiceRaceLapReward(
        SchaleDataContext db,
        EventContentDiceRaceLapRewardRequest request,
        EventContentDiceRaceLapRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);

        var ladder = _excelService.GetTable<EventContentDiceRaceTotalRewardExcelT>()
            .Where(x => x.EventContentId == request.EventContentId
                && x.RequiredLapFinishCount > play.DiceRaceReceivedLapCount
                && x.RequiredLapFinishCount <= play.DiceRaceLapCount)
            .OrderBy(x => x.RequiredLapFinishCount)
            .ToList();

        if (ladder.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentDiceRaceAlreadyReceiveLapRewardAll, $"Event {request.EventContentId} lap rewards are up to date");

        var grants = new List<ParcelResult>();
        foreach (var rung in ladder)
        {
            var rewardCount = new[] { rung.RewardParcelType?.Count ?? 0, rung.RewardParcelId?.Count ?? 0, rung.RewardParcelAmount?.Count ?? 0 }.Min();
            for (int i = 0; i < rewardCount; i++)
                grants.Add(new ParcelResult(rung.RewardParcelType![i], rung.RewardParcelId![i], rung.RewardParcelAmount![i]));
        }

        play.DiceRaceReceivedLapCount = ladder[^1].RequiredLapFinishCount;

        var resolver = await _parcelHandler.BuildParcel(db, account, grants);
        response.ParcelResultDB = resolver.ParcelResult;

        await db.SaveChangesAsync();

        response.DiceRaceDB = DiceRaceDB(play);

        return response;
    }

    private EventContentTreasureExcelT TreasureExcel(long eventContentId)
    {
        return _excelService.GetTable<EventContentTreasureExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentTreasureDataNotFound, $"Event {eventContentId} has no treasure board");
    }

    private EventContentTreasureRoundExcelT TreasureRound(long eventContentId, int metaRound)
    {
        return _excelService.GetTable<EventContentTreasureRoundExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId && x.TreasureRound == metaRound)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentTreasureDataNotFound, $"Event {eventContentId} has no treasure round {metaRound}");
    }

    // Random placement of the round's treasures on the board. Rotation is an index of quarter turns, so odd values swap the footprint - the cells and the sprite orientation have to agree or a found treasure draws over the wrong squares.
    private List<EventContentTreasureObject> LayoutBoard(EventContentTreasureRoundExcelT round, long variationId)
    {
        var width = round.TreasureRoundSize![0];
        var height = round.TreasureRoundSize[1];
        var rewardTable = _excelService.GetTable<EventContentTreasureRewardExcelT>();

        var pending = new List<EventContentTreasureRewardExcelT>();
        for (int i = 0; i < round.RewardID!.Count; i++)
        {
            var reward = rewardTable.FirstOrDefault(x => x.Id == round.RewardID[i]);
            if (reward == null) continue;

            for (int n = 0; n < round.RewardAmount![i]; n++)
                pending.Add(reward);
        }

        // Biggest first, otherwise the 3x2 has nowhere left to sit once the singles have scattered.
        pending = pending.OrderByDescending(x => x.CellUnderImageWidth * x.CellUnderImageHeight).ToList();

        var rng = new Random((int)variationId);

        for (int board = 0; board < 64; board++)
        {
            var occupied = new bool[width, height];
            var objects = new List<EventContentTreasureObject>();

            foreach (var reward in pending)
            {
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    var rotation = rng.Next(4);
                    var w = rotation % 2 == 0 ? reward.CellUnderImageWidth : reward.CellUnderImageHeight;
                    var h = rotation % 2 == 0 ? reward.CellUnderImageHeight : reward.CellUnderImageWidth;
                    if (w > width || h > height) continue;

                    var left = rng.Next(width - w + 1);
                    var top = rng.Next(height - h + 1);

                    var free = true;
                    for (int x = left; x < left + w && free; x++)
                        for (int y = top; y < top + h && free; y++)
                            free = !occupied[x, y];
                    if (!free) continue;

                    var cells = new List<EventContentTreasureCell>();
                    for (int x = left; x < left + w; x++)
                        for (int y = top; y < top + h; y++)
                        {
                            occupied[x, y] = true;
                            cells.Add(new EventContentTreasureCell { X = x, Y = y });
                        }

                    objects.Add(new EventContentTreasureObject
                    {
                        ServerId = objects.Count + 1,
                        RewardId = reward.Id,
                        Rotation = rotation,
                        IsHiddenImage = reward.HiddenImage,
                        Cells = cells
                    });
                    break;
                }
            }

            if (objects.Count == pending.Count)
                return objects;
        }

        throw new WebAPIException(WebAPIErrorCode.EventContentTreasureDataNotFound, $"Treasure round {round.TreasureRound} does not fit on a {width}x{height} board");
    }

    private void EnsureBoard(EventContentPlayDBServer play)
    {
        var treasure = TreasureExcel(play.EventContentId);

        if (play.TreasureRound == 0)
            play.TreasureRound = 1;

        var metaRound = Math.Min(treasure.LoopRound, play.TreasureRound);
        if (play.TreasureLayout.Count > 0)
            return;

        var common = _excelService.GetTable<ConstEventCommonExcelT>().FirstOrDefault();
        var variationSpace = play.TreasureRound >= treasure.LoopRound
            ? common?.TreasureLoopVariationAmount ?? 2000
            : common?.TreasureNormalVariationAmount ?? 300;

        play.TreasureVariationId = Random.Shared.NextInt64(1, variationSpace + 1);
        play.TreasureMetaRound = metaRound;
        play.TreasureFlippedCells = [];
        play.TreasureLayout = LayoutBoard(TreasureRound(play.EventContentId, metaRound), play.TreasureVariationId);
    }

    private static bool Flipped(EventContentPlayDBServer play, EventContentTreasureCell cell)
    {
        return play.TreasureFlippedCells.Any(x => x.X == cell.X && x.Y == cell.Y);
    }

    private static EventContentTreasureHistoryDB BuildHistory(EventContentPlayDBServer play)
    {
        var treasures = new List<EventContentTreasureObject>();
        var hints = new List<EventContentTreasureObject>();
        var treasureIds = new List<long>();
        var complete = true;

        foreach (var treasure in play.TreasureLayout)
        {
            var found = treasure.Cells!.Where(x => Flipped(play, x)).ToList();
            if (found.Count < treasure.Cells.Count)
                complete = false;

            if (found.Count == 0)
                continue;

            treasures.Add(new EventContentTreasureObject
            {
                ServerId = treasure.ServerId,
                RewardId = treasure.RewardId,
                Rotation = treasure.Rotation,
                IsHiddenImage = treasure.IsHiddenImage,
                Cells = found
            });

            if (found.Count == treasure.Cells.Count)
            {
                if (!treasure.IsHiddenImage)
                    treasureIds.Add(treasure.RewardId);
            }
            else
            {
                // Hitting one square reveals the whole outline; the client draws it over the unflipped cells as the hint.
                hints.Add(treasure);
            }
        }

        var normal = play.TreasureFlippedCells
            .Where(cell => !play.TreasureLayout.Any(t => t.Cells!.Any(c => c.X == cell.X && c.Y == cell.Y)))
            .ToList();

        return new EventContentTreasureHistoryDB
        {
            EventContentId = play.EventContentId,
            Round = play.TreasureRound,
            MetaRound = play.TreasureMetaRound,
            IsComplete = complete,
            CanFlip = !complete,
            CanComplete = !complete && treasureIds.Count > 0,
            HintTreasures = hints,
            Board = new EventContentTreasureBoardHistory
            {
                TreasureIds = treasureIds,
                NormalCells = normal,
                Treasures = treasures
            }
        };
    }

    [ProtocolHandler(Protocol.EventContent_TreasureLobby)]
    public async Task<EventContentTreasureLobbyResponse> TreasureLobby(
        SchaleDataContext db,
        EventContentTreasureLobbyRequest request,
        EventContentTreasureLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureBoard(play);
        await db.SaveChangesAsync();

        response.BoardHistoryDB = BuildHistory(play);
        response.VariationId = play.TreasureVariationId;
        response.HiddenImage = play.TreasureLayout.FirstOrDefault(x => x.IsHiddenImage)?.Cells?.FirstOrDefault();

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_TreasureFlip)]
    public async Task<EventContentTreasureFlipResponse> TreasureFlip(
        SchaleDataContext db,
        EventContentTreasureFlipRequest request,
        EventContentTreasureFlipResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureBoard(play);

        if (request.Round != play.TreasureRound)
            throw new WebAPIException(WebAPIErrorCode.EventContentTreasureFlipFailed, $"Round {request.Round} is not the open round");

        var cells = request.Cells ?? [];
        if (cells.Count == 0 || cells.Any(x => Flipped(play, x)))
            throw new WebAPIException(WebAPIErrorCode.EventContentTreasureFlipFailed, "Nothing to flip");

        var round = TreasureRound(play.EventContentId, play.TreasureMetaRound);

        var wasFound = play.TreasureLayout.Where(t => t.Cells!.All(c => Flipped(play, c))).Select(t => t.ServerId).ToHashSet();
        play.TreasureFlippedCells.AddRange(cells);

        var grants = new List<ParcelResult>();
        var found = 0;

        foreach (var treasure in play.TreasureLayout)
        {
            if (wasFound.Contains(treasure.ServerId) || !treasure.Cells!.All(c => Flipped(play, c)))
                continue;

            found++;
            var reward = _excelService.GetTable<EventContentTreasureRewardExcelT>().FirstOrDefault(x => x.Id == treasure.RewardId);
            if (reward == null) continue;

            var rewardCount = new[] { reward.RewardParcelType?.Count ?? 0, reward.RewardParcelId?.Count ?? 0, reward.RewardParcelAmount?.Count ?? 0 }.Min();
            for (int i = 0; i < rewardCount; i++)
                grants.Add(new ParcelResult(reward.RewardParcelType![i], reward.RewardParcelId![i], reward.RewardParcelAmount![i]));
        }

        // Digging up nothing still pays the round's cell reward, which is the only thing that keeps a badly aimed board from being a dead loss.
        var blanks = cells.Count(cell => !play.TreasureLayout.Any(t => t.Cells!.Any(c => c.X == cell.X && c.Y == cell.Y)));
        if (blanks > 0)
        {
            var cellReward = _excelService.GetTable<EventContentTreasureCellRewardExcelT>().FirstOrDefault(x => x.Id == round.CellRewardId);
            if (cellReward != null)
            {
                var rewardCount = new[] { cellReward.RewardParcelType?.Count ?? 0, cellReward.RewardParcelId?.Count ?? 0, cellReward.RewardParcelAmount?.Count ?? 0 }.Min();
                for (int i = 0; i < rewardCount; i++)
                    grants.Add(new ParcelResult(cellReward.RewardParcelType![i], cellReward.RewardParcelId![i], cellReward.RewardParcelAmount![i] * blanks));
            }
        }

        var consumed = (await _parcelHandler.BuildParcel(db, account, GoodsCost(round.CellCheckGoodsId, cells.Count), isConsume: true)).ParcelResult;
        response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants, consumed)).ParcelResult;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_TreasureCheckedCellCount, cells.Count);
        if (found > 0)
            _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_TreasureGetTreasureCount, found);

        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.BoardHistoryDB = BuildHistory(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_TreasureNextRound)]
    public async Task<EventContentTreasureNextRoundResponse> TreasureNextRound(
        SchaleDataContext db,
        EventContentTreasureNextRoundRequest request,
        EventContentTreasureNextRoundResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureBoard(play);

        if (request.Round != play.TreasureRound)
            throw new WebAPIException(WebAPIErrorCode.EventContentTreasureNotComplete, $"Round {request.Round} is not the open round");

        var history = BuildHistory(play);
        if (!history.IsComplete && !history.CanComplete)
            throw new WebAPIException(WebAPIErrorCode.EventContentTreasureNotComplete, $"Round {request.Round} has nothing dug up yet");

        play.TreasureRound++;
        play.TreasureLayout = [];
        EnsureBoard(play);

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_TreasureRoundRefreshCount);

        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.BoardHistoryDB = BuildHistory(play);
        response.HiddenImage = play.TreasureLayout.FirstOrDefault(x => x.IsHiddenImage)?.Cells?.FirstOrDefault();

        return response;
    }

    private EventContentConcentrationExcelT ConcentrationExcel(long eventContentId)
    {
        return _excelService.GetTable<EventContentConcentrationExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentNotOpen, $"Event {eventContentId} has no concentration board");
    }

    private void DealConcentrationBoard(EventContentPlayDBServer play)
    {
        var conc = ConcentrationExcel(play.EventContentId);
        var pool = _excelService.GetTable<EventContentConcentrationCardExcelT>()
            .Where(x => x.EventContentId == play.EventContentId)
            .Select(x => x.CardId)
            .ToList();

        var picked = new List<long>();
        while (picked.Count < conc.MaxCardPairCount && pool.Count > 0)
        {
            var i = Random.Shared.Next(pool.Count);
            picked.Add(pool[i]);
            pool.RemoveAt(i);
        }

        var deck = picked.Concat(picked).ToList();
        var cards = new List<EventContentConcentrationCardDB>();
        while (deck.Count > 0)
        {
            var i = Random.Shared.Next(deck.Count);
            cards.Add(new EventContentConcentrationCardDB { Index = cards.Count, CardId = deck[i] });
            deck.RemoveAt(i);
        }

        play.ConcentrationCards = cards;
        play.ConcentrationFlipCount = 0;
    }

    private void EnsureConcentration(EventContentPlayDBServer play)
    {
        if (play.ConcentrationRound == 0)
            play.ConcentrationRound = 1;
        if (play.ConcentrationCards.Count == 0)
            DealConcentrationBoard(play);
    }

    // The full deck stays server-side; the save the client gets carries zeroed CardIds for everything still face down, so a reconnect cannot be used to peek at the board.
    private static EventContentConcentrationSaveDB ConcentrationSave(EventContentPlayDBServer play)
    {
        return new EventContentConcentrationSaveDB
        {
            FlipCount = play.ConcentrationFlipCount,
            Round = play.ConcentrationRound,
            CardDBs = play.ConcentrationCards
                .Select(x => new EventContentConcentrationCardDB { Index = x.Index, CardId = x.IsMatched ? x.CardId : 0, IsMatched = x.IsMatched })
                .ToList()
        };
    }

    private List<ParcelResult> ConcentrationRewards(long eventContentId, ConcentrationRewardType type, Rarity rarity, int round)
    {
        var rows = _excelService.GetTable<EventContentConcentrationRewardExcelT>()
            .Where(x => x.EventContentId == eventContentId && x.ConcentrationRewardType == type && x.Rarity == rarity)
            .ToList();
        var row = rows.FirstOrDefault(x => x.Round == round) ?? rows.FirstOrDefault(x => x.IsLoop) ?? rows.FirstOrDefault();

        var grants = new List<ParcelResult>();
        for (int i = 0; i < (row?.RewardParcelType?.Count ?? 0); i++)
            grants.Add(new ParcelResult(row!.RewardParcelType![i], row.RewardParcelId![i], row.RewardParcelAmount![i]));
        return grants;
    }

    [ProtocolHandler(Protocol.EventContent_ConcentrationGetInfo)]
    public async Task<EventContentConcentrationGetInfoResponse> ConcentrationGetInfo(
        SchaleDataContext db,
        EventContentConcentrationGetInfoRequest request,
        EventContentConcentrationGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureConcentration(play);
        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ConcentrationSave(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ConcentrationFlipCard)]
    public async Task<EventContentConcentrationFlipCardResponse> ConcentrationFlipCard(
        SchaleDataContext db,
        EventContentConcentrationFlipCardRequest request,
        EventContentConcentrationFlipCardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureConcentration(play);

        if (request.FirstIndex == request.SecondIndex)
            throw new WebAPIException(WebAPIErrorCode.EventContentConcentrationRequestSameIndex, $"Flip {request.FirstIndex} twice");

        var first = play.ConcentrationCards.First(x => x.Index == request.FirstIndex);
        var second = play.ConcentrationCards.First(x => x.Index == request.SecondIndex);
        if (first.IsMatched || second.IsMatched)
            throw new WebAPIException(WebAPIErrorCode.EventContentConcentrationAlreadyMatchedIndex, $"Card {(first.IsMatched ? first.Index : second.Index)} is already matched");

        play.ConcentrationFlipCount++;

        var grants = new List<ParcelResult>();
        if (first.CardId == second.CardId)
        {
            first.IsMatched = true;
            second.IsMatched = true;

            var rarity = _excelService.GetTable<EventContentConcentrationCardExcelT>()
                .First(x => x.EventContentId == play.EventContentId && x.CardId == first.CardId).Rarity;
            grants = ConcentrationRewards(play.EventContentId, ConcentrationRewardType.PairMatch, rarity, play.ConcentrationRound);
        }

        var conc = ConcentrationExcel(play.EventContentId);
        var consumed = conc.CostGoodsId != 0
            ? (await _parcelHandler.BuildParcel(db, account, GoodsCost(conc.CostGoodsId, 1), isConsume: true)).ParcelResult
            : null;
        response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants, consumed)).ParcelResult;

        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ConcentrationSave(play);
        response.First = first;
        response.Second = second;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ConcentrationRoundComplete)]
    public async Task<EventContentConcentrationRoundCompleteResponse> ConcentrationRoundComplete(
        SchaleDataContext db,
        EventContentConcentrationRoundCompleteRequest request,
        EventContentConcentrationRoundCompleteResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureConcentration(play);

        if (play.ConcentrationCards.Any(x => !x.IsMatched))
            throw new WebAPIException(WebAPIErrorCode.EventContentConcentrationCannotCompleteRound, $"Round {play.ConcentrationRound} still has face-down cards");

        response.SaveDBBefore = ConcentrationSave(play);

        var grants = ConcentrationRewards(play.EventContentId, ConcentrationRewardType.RoundRenewal, Rarity.N, play.ConcentrationRound);
        if (grants.Count > 0)
            response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants)).ParcelResult;

        play.ConcentrationRound++;
        DealConcentrationBoard(play);
        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ConcentrationSave(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ConcentrationRoundSkip)]
    public async Task<EventContentConcentrationRoundSkipResponse> ConcentrationRoundSkip(
        SchaleDataContext db,
        EventContentConcentrationRoundSkipRequest request,
        EventContentConcentrationRoundSkipResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureConcentration(play);

        var conc = ConcentrationExcel(play.EventContentId);
        if (play.ConcentrationRound < conc.InstantClearRound)
            throw new WebAPIException(WebAPIErrorCode.EventContentConcentrationCannotSkipRound, $"Skipping opens at round {conc.InstantClearRound}");

        response.SaveDBBefore = ConcentrationSave(play);

        // A skip forfeits the pair-match payouts but still takes the round renewal reward; that is the whole point of the loop-round shortcut.
        var grants = ConcentrationRewards(play.EventContentId, ConcentrationRewardType.RoundRenewal, Rarity.N, play.ConcentrationRound);
        if (grants.Count > 0)
            response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants)).ParcelResult;

        play.ConcentrationRound++;
        DealConcentrationBoard(play);
        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ConcentrationSave(play);

        return response;
    }

    private EventContentClueSearchRoundExcelT ClueSearchRound(long eventContentId, int round)
    {
        var rows = _excelService.GetTable<EventContentClueSearchRoundExcelT>()
            .Where(x => x.EventContentId == eventContentId)
            .OrderBy(x => x.Round)
            .ToList();
        if (rows.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.EventContentNotOpen, $"Event {eventContentId} has no clue search rounds");

        return rows.FirstOrDefault(x => x.Round == round) ?? rows.Last();
    }

    private void EnsureClueSearch(EventContentPlayDBServer play)
    {
        if (play.ClueSearchRound == 0)
            play.ClueSearchRound = 1;
        if (play.ClueSearchSlots.Count == 0)
        {
            var round = ClueSearchRound(play.EventContentId, play.ClueSearchRound);
            play.ClueSearchSlots = (round.ClueSlotNumber ?? [])
                .Select((slot, i) => new ClueSearchSlotDB { SlotNumber = slot, ClueId = round.ClueId![i] })
                .ToList();
        }
    }

    private static ClueSearchSaveDB ClueSearchSave(EventContentPlayDBServer play)
    {
        return new ClueSearchSaveDB
        {
            EventContentId = play.EventContentId,
            Round = play.ClueSearchRound,
            SlotDBs = play.ClueSearchSlots
        };
    }

    [ProtocolHandler(Protocol.EventContent_ClueSearchGetInfo)]
    public async Task<EventContentClueSearchGetInfoResponse> ClueSearchGetInfo(
        SchaleDataContext db,
        EventContentClueSearchGetInfoRequest request,
        EventContentClueSearchGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureClueSearch(play);
        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ClueSearchSave(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ClueSearchSubmit)]
    public async Task<EventContentClueSearchSubmitResponse> ClueSearchSubmit(
        SchaleDataContext db,
        EventContentClueSearchSubmitRequest request,
        EventContentClueSearchSubmitResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureClueSearch(play);

        var slot = play.ClueSearchSlots.FirstOrDefault(x => x.ClueId == request.ClueId && !x.IsSubmitted)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentClueSearchCannotSubmit, $"Clue {request.ClueId} is not waiting in round {play.ClueSearchRound}");

        // The clue itself is an inventory item keyed by its ClueId; submitting hands it over.
        var round = ClueSearchRound(play.EventContentId, play.ClueSearchRound);
        var cost = round.ClueCostAmount![round.ClueId!.IndexOf(request.ClueId)];
        response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account,
            new List<ParcelResult> { new(ParcelType.Item, request.ClueId, cost) }, isConsume: true)).ParcelResult;

        slot.IsSubmitted = true;
        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ClueSearchSave(play);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ClueSearchRoundComplete)]
    public async Task<EventContentClueSearchRoundCompleteResponse> ClueSearchRoundComplete(
        SchaleDataContext db,
        EventContentClueSearchRoundCompleteRequest request,
        EventContentClueSearchRoundCompleteResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var play = db.GetOrAddEventContentPlay(account.ServerId, request.EventContentId);
        EnsureClueSearch(play);

        if (play.ClueSearchSlots.Any(x => !x.IsSubmitted))
            throw new WebAPIException(WebAPIErrorCode.EventContentClueSearchCannotCompleteRound, $"Round {play.ClueSearchRound} still has open slots");

        var round = ClueSearchRound(play.EventContentId, play.ClueSearchRound);
        var reward = _excelService.GetTable<EventContentClueSearchRewardExcelT>().FirstOrDefault(x => x.Id == round.RewardId);
        var grants = new List<ParcelResult>();
        for (int i = 0; i < (reward?.RewardParcelType?.Count ?? 0); i++)
            grants.Add(new ParcelResult(reward!.RewardParcelType![i], reward.RewardParcelId![i], reward.RewardParcelAmount![i]));
        if (grants.Count > 0)
            response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants)).ParcelResult;

        play.ClueSearchRound++;
        play.ClueSearchSlots = [];
        EnsureClueSearch(play);
        db.EventContentPlays.Update(play);
        await db.SaveChangesAsync();

        response.SaveDB = ClueSearchSave(play);

        return response;
    }

    private Dictionary<long, List<VisitingCharacterDB>> RollVisitors(SchaleDataContext db, AccountDBServer account, long locationId)
    {
        var owned = db.GetAccountCharacters(account.ServerId).Select(x => new VisitingCharacterDB { UniqueId = x.UniqueId, ServerId = x.ServerId }).ToList();

        return _excelService.GetTable<EventContentZoneExcelT>()
            .Where(x => x.LocationId == locationId)
            .ToDictionary(zone => zone.Id, zone =>
            {
                var pool = new List<VisitingCharacterDB>(owned);
                var visiting = new List<VisitingCharacterDB>();

                foreach (var prob in zone.StudentVisitProb ?? [])
                {
                    if (pool.Count == 0)
                        break;
                    if (Random.Shared.Next(10000) >= prob)
                        continue;

                    var index = Random.Shared.Next(pool.Count);
                    visiting.Add(pool[index]);
                    pool.RemoveAt(index);
                }

                return visiting;
            });
    }

    private EventContentLocationDBServer EnsureLocation(SchaleDataContext db, AccountDBServer account, long eventContentId, EventContentLocationExcelT excel)
    {
        var location = db.GetAccountEventContentLocations(account.ServerId).FirstOrDefault(x => x.EventContentId == eventContentId);
        if (location != null)
            return location;

        location = new EventContentLocationDBServer
        {
            AccountServerId = account.ServerId,
            EventContentId = eventContentId,
            LocationId = excel.Id,
            Rank = 1,
            ZoneVisitCharacterDBs = RollVisitors(db, account, excel.Id)
        };

        db.EventContentLocations.Add(location);

        return location;
    }

    private static EventContentLocationDB LocationDB(EventContentLocationDBServer location)
    {
        return new EventContentLocationDB
        {
            LocationId = location.LocationId,
            Rank = location.Rank,
            Exp = location.Exp,
            ScheduleCount = location.ScheduleCount,
            ZoneVisitCharacterDBs = location.ZoneVisitCharacterDBs
        };
    }

    private EventContentLocationExcelT LocationExcel(long eventContentId)
    {
        return _excelService.GetTable<EventContentLocationExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentLocationNotFound, $"Event {eventContentId} has no location");
    }

    [ProtocolHandler(Protocol.EventContent_LocationGetInfo)]
    public async Task<EventContentLocationGetInfoResponse> LocationGetInfo(
        SchaleDataContext db,
        EventContentLocationGetInfoRequest request,
        EventContentLocationGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var location = EnsureLocation(db, account, request.EventContentId, LocationExcel(request.EventContentId));
        await db.SaveChangesAsync();

        response.EventContentLocationDB = LocationDB(location);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_LocationAttendSchedule)]
    public async Task<EventContentLocationAttendScheduleResponse> LocationAttendSchedule(
        SchaleDataContext db,
        EventContentLocationAttendScheduleRequest request,
        EventContentLocationAttendScheduleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var excel = LocationExcel(request.EventContentId);
        var location = EnsureLocation(db, account, request.EventContentId, excel);

        var zones = _excelService.GetTable<EventContentZoneExcelT>().Where(x => x.LocationId == location.LocationId).OrderBy(x => x.LocationRank).ToList();
        var zone = zones.FirstOrDefault(x => x.Id == request.ZoneId)
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentLocationNotFound, $"Zone {request.ZoneId} is not part of location {location.LocationId}");

        if (location.Rank < zone.LocationRank)
            throw new WebAPIException(WebAPIErrorCode.EventContentLocationScheduleCanNotAttend, $"Zone {request.ZoneId} opens at rank {zone.LocationRank}");

        // GetOpenSchedule: the last step of the zone's group the location has the rank for.
        var schedule = _excelService.GetTable<EventContentLocationRewardExcelT>()
            .Where(x => x.ScheduleGroupId == zone.RewardGroupId && x.LocationRank <= location.Rank)
            .OrderBy(x => x.OrderInGroup)
            .LastOrDefault()
            ?? throw new WebAPIException(WebAPIErrorCode.EventContentLocationScheduleCanNotAttend, $"Zone {request.ZoneId} has no open schedule");

        if (request.Count > 1000)
            throw new WebAPIException(WebAPIErrorCode.EventContentLocationScheduleCanNotAttend, $"Attend count {request.Count} is out of range");

        var count = Math.Max(request.Count, 1);
        var visitors = location.ZoneVisitCharacterDBs.TryGetValue(request.ZoneId, out var visiting) ? visiting : [];
        var characters = _excelService.GetTable<CharacterExcelT>();

        var grants = new List<ParcelResult>();
        response.ExtraRewards = [];

        for (int attend = 0; attend < count; attend++)
        {
            foreach (var visitor in visitors)
            {
                var favorExp = schedule.FavorExp;
                if (schedule.ExtraFavorExpProb > 0 && Random.Shared.Next(10000) < schedule.ExtraFavorExpProb)
                    favorExp += schedule.ExtraFavorExp;
                if (favorExp > 0)
                    grants.Add(new ParcelResult(ParcelType.FavorExp, visitor.UniqueId, favorExp));

                if (schedule.SecretStoneAmount > 0 && Random.Shared.Next(10000) < schedule.SecretStoneProb)
                {
                    var student = characters.FirstOrDefault(x => x.Id == visitor.UniqueId);
                    if (student != null && student.SecretStoneItemId > 0)
                        grants.Add(new ParcelResult(ParcelType.Item, student.SecretStoneItemId, schedule.SecretStoneAmount));
                }
            }

            var rewardCount = new[] { schedule.RewardParcelType?.Count ?? 0, schedule.RewardParcelId?.Count ?? 0, schedule.RewardAmount?.Count ?? 0 }.Min();
            for (int i = 0; i < rewardCount; i++)
                grants.Add(new ParcelResult(schedule.RewardParcelType![i], schedule.RewardParcelId![i], schedule.RewardAmount![i]));

            var extraCount = new[] { schedule.ExtraRewardParcelType?.Count ?? 0, schedule.ExtraRewardParcelId?.Count ?? 0, schedule.ExtraRewardAmount?.Count ?? 0, schedule.ExtraRewardProb?.Count ?? 0 }.Min();
            for (int i = 0; i < extraCount; i++)
            {
                if (Random.Shared.Next(10000) >= schedule.ExtraRewardProb![i])
                    continue;

                grants.Add(new ParcelResult(schedule.ExtraRewardParcelType![i], schedule.ExtraRewardParcelId![i], schedule.ExtraRewardAmount![i]));
                response.ExtraRewards.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = schedule.ExtraRewardParcelType[i], Id = schedule.ExtraRewardParcelId[i] },
                    Amount = schedule.ExtraRewardAmount![i],
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One
                });
            }
        }

        var consumed = (await _parcelHandler.BuildParcel(db, account, [new ParcelResult(excel.ScheduleEventPointCostParcelType, excel.ScheduleEventPointCostParcelId, excel.ScheduleEventPointCostParcelAmount * count)], isConsume: true)).ParcelResult;
        response.ParcelResultDB = (await _parcelHandler.BuildParcel(db, account, grants, consumed)).ParcelResult;

        location.ScheduleCount += count;

        // Rank track is the event points spent here: every zone threshold is an exact multiple of one schedule's cost.
        location.Exp += excel.ScheduleEventPointCostParcelAmount * count;
        var topRank = zones.Count > 0 ? zones[^1].LocationRank : 1;
        while (location.Rank < topRank)
        {
            var step = zones.FirstOrDefault(x => x.LocationRank == location.Rank);
            if (step == null || step.EventPointForLocationRank <= 0 || location.Exp < step.EventPointForLocationRank)
                break;

            location.Exp -= step.EventPointForLocationRank;
            location.Rank++;
        }

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CompleteScheduleCount, count);
        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CompleteScheduleGroupCount, count, zone.RewardGroupId);
        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.SpecificEventLocationRank);

        db.EventContentLocations.Update(location);
        await db.SaveChangesAsync();

        response.EventContentLocationDB = LocationDB(location);

        return response;
    }
}
