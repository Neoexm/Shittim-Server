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

public class MiniGameCCGHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;
    private readonly MiniGameMissionService _missionService;
    private readonly EventContentCollectionService _collectionService;

    public MiniGameCCGHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler,
        MiniGameMissionService missionService,
        EventContentCollectionService collectionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
        _missionService = missionService;
        _collectionService = collectionService;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGLobby)]
    public async Task<MiniGameCCGLobbyResponse> Lobby(
        SchaleDataContext db,
        MiniGameCCGLobbyRequest request,
        MiniGameCCGLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await db.MiniGameCCGSaves.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        if (row == null)
        {
            row = NewRow(account.ServerId, Info(request.EventContentId));
            db.MiniGameCCGSaves.Add(row);
            await db.SaveChangesAsync();
        }

        response.CCGSaveDB = row.SaveDB;
        response.Perks = row.Perks;
        response.RewardPoint = row.RewardPoint;
        response.CanSweep = row.ClearCount > 0;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGCreateGame)]
    public async Task<MiniGameCCGCreateGameResponse> CreateGame(
        SchaleDataContext db,
        MiniGameCCGCreateGameRequest request,
        MiniGameCCGCreateGameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = Info(request.EventContentId);

        var row = await db.MiniGameCCGSaves.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        if (row == null)
        {
            row = NewRow(account.ServerId, info);
            db.MiniGameCCGSaves.Add(row);
        }
        else if (row.SaveDB != null && !row.SaveDB.GameOver && !request.ForceDiscardSave)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGPlayingSaveAlreadyExists, $"A run of CCG {info.CCGId} is still open");

        // there is no ParcelResultDB on the create response, so the entry fee leaves the client's counter stale until its next sync - the alternative is a free run
        await _parcelHandler.BuildParcel(db, account, ParcelInfo.CreateParcelInfo(info.CostParcelType, info.CostParcelId, info.CostParcelAmount), isConsume: true);

        var constants = Constants();
        var floor = Levels(info.CCGId, 1);
        if (floor.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"CCG {info.CCGId} has no first floor");

        var level = floor[Random.Shared.Next(floor.Count)];

        var save = new MiniGameCCGSaveDB
        {
            EventContentId = request.EventContentId,
            CCGId = info.CCGId,
            Strikers = [],
            Specials = [],
            OverflowedStrikerIds = [],
            OverflowedSpecialIds = [],
            Deck = [],
            LevelId = level.LevelId,
            CurrentNodeId = 0,
            LastStageIndex = TotalStages(info.CCGId),
            ClearedHistoryDBs = [],
            Perks = request.DisablePerk ? [] : new List<long>(row.Perks),
            TotalSkillCount = new Dictionary<long, int>()
        };

        foreach (var card in _excelService.GetTable<MinigameCCGStartDeckCardExcelT>().Where(x => x.CCGId == info.CCGId))
            save.Deck.Add(new MiniGameCCGCardDB { CardDBId = save.CardDBIdProvider++, CardId = card.CardId });

        foreach (var member in _excelService.GetTable<MinigameCCGStartDeckCharacterExcelT>().Where(x => x.CCGId == info.CCGId))
        {
            var excel = CharacterInfo(member.CharacterId);
            var roster = excel != null && excel.Type == CCGCharacterType.Special ? save.Specials : save.Strikers;
            roster.Add(new MiniGameCCGCharacterDB { SlotIndex = roster.Count, CharacterId = member.CharacterId, Health = excel?.MaxHealth ?? 0 });
        }

        row.CCGId = info.CCGId;
        row.SaveDB = save;
        row.RewardPoint = 0;
        row.ReceivedRewardItemIds = [];
        await db.SaveChangesAsync();

        response.CCGSaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGEnterStage)]
    public async Task<MiniGameCCGEnterStageResponse> EnterStage(
        SchaleDataContext db,
        MiniGameCCGEnterStageRequest request,
        MiniGameCCGEnterStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);

        if (save.GameOver)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, "The run is already over");

        if (save.CurrentStageDB != null)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGPlayingStageAlreadyExists, $"Node {save.CurrentNodeId} has not been resolved");

        var node = Node(save.LevelId, request.NodeId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Node {request.NodeId} is not on level {save.LevelId}");

        var walked = save.ClearedHistoryDBs!.Where(x => x.LevelId == save.LevelId).ToList();
        if (walked.Count == 0)
        {
            if (request.NodeId != StartNode(save.LevelId))
                throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Level {save.LevelId} does not start at node {request.NodeId}");
        }
        else
        {
            var reachable = Node(save.LevelId, walked[^1].NodeId)?.NextNodeId ?? [];
            if (!reachable.Contains(request.NodeId))
                throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Node {request.NodeId} does not follow node {walked[^1].NodeId}");
        }

        var candidates = _excelService.GetTable<MinigameCCGLevelStageExcelT>().Where(x => x.GroupId == node.StageGroupId).ToList();
        if (candidates.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Stage group {node.StageGroupId} is empty");

        var stage = candidates[Random.Shared.Next(candidates.Count)];
        var enemies = stage.EnemyGroupId ?? [];

        save.CurrentNodeId = request.NodeId;
        save.CurrentStageDB = new MiniGameCCGStagePlayDB
        {
            StageId = stage.Id,
            EnemyGroupId = enemies.Count > 0 ? enemies[Random.Shared.Next(enemies.Count)] : 0,
            RewardDBs = [],
            RerollPoint = RerollPoints(save)
        };

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.StageDB = save.CurrentStageDB;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGEndStageDual)]
    public async Task<MiniGameCCGEndStageDualResponse> EndStageDual(
        SchaleDataContext db,
        MiniGameCCGEndStageDualRequest request,
        MiniGameCCGEndStageDualResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);
        var play = Playing(save);
        var stage = StageInfo(play.StageId);

        if (stage.StageType != CCGStageType.Battle)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Stage {stage.Id} is not a battle");

        var summary = request.Summary
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, "No battle summary");

        // the whole card battle is client side, so the striker health it hands back is the only record of what the fight cost
        foreach (var striker in summary.Strikers ?? [])
        {
            var slot = save.Strikers!.FirstOrDefault(x => x.SlotIndex == striker.SlotIndex);
            if (slot != null)
                slot.Health = striker.Health;
        }

        save.TotalUsedCost += summary.TotalUsedCost;
        save.TotalDamageAmount += summary.TotalDamageAmount;
        save.TotalKillCount += summary.TotalKillCount;

        save.TotalSkillCount ??= new Dictionary<long, int>();
        foreach (var (skillId, count) in summary.TotalSkillCount ?? [])
            save.TotalSkillCount[skillId] = save.TotalSkillCount.GetValueOrDefault(skillId) + count;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGUseCostCount, summary.TotalUsedCost, request.EventContentId);
        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGTotalDamageCount, summary.TotalDamageAmount, request.EventContentId);

        if (summary.IsPlayerWin)
        {
            ClearStage(save, row, stage);
            SettleStage(save, row);
        }
        else
        {
            save.GameOver = true;
            save.CurrentStageDB = null;
        }

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.StageDB = play;
        response.SaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGEndStageEvent)]
    public async Task<MiniGameCCGEndStageEventResponse> EndStageEvent(
        SchaleDataContext db,
        MiniGameCCGEndStageEventRequest request,
        MiniGameCCGEndStageEventResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);
        var play = Playing(save);
        var stage = StageInfo(play.StageId);

        if (stage.StageType != CCGStageType.Event)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Stage {stage.Id} is not an event");

        ClearStage(save, row, stage);

        // event nodes hand over everything they drew, so nothing waits on a pick
        foreach (var reward in play.RewardDBs ?? [])
        {
            if (reward.Type != MiniGameCCGStageRewardType.All)
                continue;
            foreach (var rewardId in reward.RewardIds ?? [])
                GrantRewardCard(save, rewardId);
        }

        play.RewardDBs = (play.RewardDBs ?? []).Where(x => x.Type != MiniGameCCGStageRewardType.All).ToList();
        SettleStage(save, row);

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.StageDB = play;
        response.SaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGSelectRewardCard)]
    public async Task<MiniGameCCGSelectRewardCardResponse> SelectRewardCard(
        SchaleDataContext db,
        MiniGameCCGSelectRewardCardRequest request,
        MiniGameCCGSelectRewardCardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);
        var play = Playing(save);

        var reward = (play.RewardDBs ?? []).FirstOrDefault(x => x.RewardIndex == request.RewardIndex)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Reward {request.RewardIndex} is not on offer");

        var offered = reward.RewardIds ?? [];
        var taken = new List<long>();

        if (reward.Type == MiniGameCCGStageRewardType.All)
            taken.AddRange(offered);
        else if (request.SelectedIndex >= 0 && request.SelectedIndex < offered.Length)
            taken.Add(offered[request.SelectedIndex]);
        else
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Card {request.SelectedIndex} is not among the {offered.Length} on offer");

        foreach (var rewardId in taken)
            GrantRewardCard(save, rewardId);

        play.RewardDBs!.Remove(reward);
        SettleStage(save, row);

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.StageDB = play;
        response.SaveDB = save;
        response.ReceivedRewardIds = taken;
        return response;
    }

    [ProtocolHandler(Protocol.Minigame_CCGReplaceCharacter)]
    public async Task<MiniGameCCGReplaceCharacterResponse> ReplaceCharacter(
        SchaleDataContext db,
        MiniGameCCGReplaceCharacterRequest request,
        MiniGameCCGReplaceCharacterResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);

        var overflow = request.IsStriker ? save.OverflowedStrikerIds! : save.OverflowedSpecialIds!;
        if (!overflow.Remove(request.CharacterId))
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Character {request.CharacterId} is not waiting for a slot");

        MiniGameCCGCharacterDB? placed = null;

        // skip and back both send -1, which drops the character instead of swapping one out
        if (request.SlotIndex >= 0)
        {
            var roster = request.IsStriker ? save.Strikers! : save.Specials!;
            var slot = roster.FirstOrDefault(x => x.SlotIndex == request.SlotIndex)
                ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Slot {request.SlotIndex} is empty");

            var excel = CharacterInfo(request.CharacterId);
            slot.CharacterId = request.CharacterId;
            slot.Health = excel?.MaxHealth ?? slot.Health;
            placed = slot;
        }

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.SaveDB = save;
        response.CCGCharacterDB = placed;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGSelectCampAction)]
    public async Task<MiniGameCCGSelectCampActionResponse> SelectCampAction(
        SchaleDataContext db,
        MiniGameCCGSelectCampActionRequest request,
        MiniGameCCGSelectCampActionResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);
        var play = Playing(save);
        var stage = StageInfo(play.StageId);

        if (stage.StageType != CCGStageType.Camp)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Stage {stage.Id} is not a camp");

        var everyone = save.Strikers!.Concat(save.Specials!).ToList();

        switch (request.SelectedOption)
        {
            case MiniGameCCGCampOption.Heal:
                foreach (var member in everyone.Where(x => x.Health > 0))
                    member.Health = CharacterInfo(member.CharacterId)?.MaxHealth ?? member.Health;
                break;

            case MiniGameCCGCampOption.Revive:
                foreach (var member in everyone.Where(x => x.Health <= 0))
                {
                    var max = CharacterInfo(member.CharacterId)?.MaxHealth ?? 0;
                    member.Health = Math.Max(1, max * Constants().CampReviveHealthRate / 10000);
                }
                break;

            case MiniGameCCGCampOption.RemoveCard:
            {
                var allowed = stage.CampDiscardCardCount + PerkRows(save).Sum(x => x.DiscardPoint);
                var discards = request.RemoveCardDBIds ?? [];
                if (discards.Count > allowed)
                    throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"{discards.Count} cards discarded against a limit of {allowed}");

                foreach (var cardDBId in discards)
                {
                    var card = save.Deck!.FirstOrDefault(x => x.CardDBId == cardDBId)
                        ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Card {cardDBId} is not in the deck");
                    save.Deck.Remove(card);
                }
                break;
            }

            case MiniGameCCGCampOption.Skip:
                break;

            default:
                throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Camp option {request.SelectedOption} is not a choice");
        }

        ClearStage(save, row, stage);
        SettleStage(save, row);

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.StageDB = play;
        response.SaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGRerollReward)]
    public async Task<MiniGameCCGRerollRewardResponse> RerollReward(
        SchaleDataContext db,
        MiniGameCCGRerollRewardRequest request,
        MiniGameCCGRerollRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);
        var play = Playing(save);

        if (play.RerollPoint <= 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGNoRerollPoint, "No rerolls left on this stage");

        play.RerollPoint--;
        play.RewardDBs = RollStageRewards(StageInfo(play.StageId));

        row.SaveDB = save;
        await db.SaveChangesAsync();

        response.StageDB = play;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGGiveupGame)]
    public async Task<MiniGameCCGGiveupGameResponse> GiveupGame(
        SchaleDataContext db,
        MiniGameCCGGiveupGameRequest request,
        MiniGameCCGGiveupGameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);

        save.GameOver = true;
        save.CurrentStageDB = null;
        row.SaveDB = save;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGRetreatCount, 1, request.EventContentId);
        await db.SaveChangesAsync();

        response.SaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGCompleteGame)]
    public async Task<MiniGameCCGCompleteGameResponse> CompleteGame(
        SchaleDataContext db,
        MiniGameCCGCompleteGameRequest request,
        MiniGameCCGCompleteGameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = Save(row);

        if (!save.GameOver)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGSaveNotComplete, "The run is still going");

        var earned = _excelService.GetTable<MinigameCCGRewardItemExcelT>()
            .Where(x => x.CCGId == row.CCGId && x.MinPoint <= row.RewardPoint && !row.ReceivedRewardItemIds.Contains(x.Id))
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        var parcels = earned.SelectMany(x => ParcelInfo.CreateParcelInfo(x.RewardParcelType, x.RewardParcelId, x.RewardParcelAmount)).ToList();
        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);

        row.ReceivedRewardItemIds.AddRange(earned.Select(x => x.Id));
        if (save.Clear)
            row.ClearCount++;
        row.SaveDB = null;

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGResultCount, 1, request.EventContentId);
        if (save.Clear)
            _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGCompleteCount, 1, request.EventContentId);

        await db.SaveChangesAsync();

        response.OldSaveDB = save;
        response.ParcelResultDB = resolver.ParcelResult;
        response.RewardParcels = parcels;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGSweep)]
    public async Task<MiniGameCCGSweepResponse> Sweep(
        SchaleDataContext db,
        MiniGameCCGSweepRequest request,
        MiniGameCCGSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = Info(request.EventContentId);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        if (row.ClearCount <= 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"CCG {info.CCGId} has never been cleared");

        var count = Math.Max(1, request.SweepCount);
        var resolver = await _parcelHandler.BuildParcel(db, account, ParcelInfo.CreateParcelInfo(info.CostParcelType, info.CostParcelId, info.CostParcelAmount * (long)count), isConsume: true);

        // a sweep stands in for a perfect run, so it pays the whole ladder rather than replaying the point count
        var ladder = _excelService.GetTable<MinigameCCGRewardItemExcelT>()
            .Where(x => x.CCGId == info.CCGId)
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        var rewards = new List<List<ParcelInfo>>();
        for (var i = 0; i < count; i++)
        {
            var parcels = ladder.SelectMany(x => ParcelInfo.CreateParcelInfo(x.RewardParcelType, x.RewardParcelId, x.RewardParcelAmount)).ToList();
            resolver = await _parcelHandler.BuildParcel(db, account, parcels, resolver.ParcelResult);
            rewards.Add(parcels);
        }

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGResultCount, count, request.EventContentId);
        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGCompleteCount, count, request.EventContentId);
        await db.SaveChangesAsync();

        response.Rewards = rewards;
        response.ParcelResultDB = resolver.ParcelResult;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGBuyPerk)]
    public async Task<MiniGameCCGBuyPerkResponse> BuyPerk(
        SchaleDataContext db,
        MiniGameCCGBuyPerkRequest request,
        MiniGameCCGBuyPerkResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var info = Info(request.EventContentId);

        var row = await db.MiniGameCCGSaves.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
        if (row == null)
        {
            row = NewRow(account.ServerId, info);
            db.MiniGameCCGSaves.Add(row);
        }

        var perk = _excelService.GetTable<MinigameCCGPerkExcelT>().FirstOrDefault(x => x.Id == request.PerkId && x.CCGId == info.CCGId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"MinigameCCGPerkExcel {request.PerkId} not found");

        if (row.Perks.Contains(perk.Id))
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Perk {perk.Id} is already owned");

        var missing = (perk.RequiredPerkId ?? []).Where(x => x != 0 && !row.Perks.Contains(x)).ToList();
        if (missing.Count > 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Perk {perk.Id} still needs {string.Join(",", missing)}");

        var resolver = await _parcelHandler.BuildParcel(db, account, ParcelInfo.CreateParcelInfo(info.PerkCostParcelType, info.PerkCostParcelId, perk.CostParcelAmount), isConsume: true);

        row.Perks.Add(perk.Id);
        if (row.SaveDB != null && !row.SaveDB.GameOver && row.SaveDB.Perks != null)
            row.SaveDB.Perks.Add(perk.Id);

        _missionService.UpdateMissionProgress(db, account, request.EventContentId, MissionCompleteConditionType.Reset_CCGActivatePerkCount, 1, request.EventContentId);
        await db.SaveChangesAsync();

        response.Perks = row.Perks;
        response.ParcelResultDB = resolver.ParcelResult;
        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.MinigameCCGBuyPerk);
        await db.SaveChangesAsync();

        return response;
    }

    private void ClearStage(MiniGameCCGSaveDB save, MiniGameCCGSaveDBServer row, MinigameCCGLevelStageExcelT stage)
    {
        var play = save.CurrentStageDB!;
        if (play.IsClear)
            throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"Node {save.CurrentNodeId} has already been cleared");

        play.IsClear = true;
        play.RewardDBs = RollStageRewards(stage);

        save.RewardPoint++;
        save.CurrentStageIndex++;
        save.ClearedHistoryDBs!.Add(new MiniGameCCGPlayHistory
        {
            LevelId = save.LevelId,
            NodeId = save.CurrentNodeId,
            StagePlayDB = play
        });

        row.RewardPoint = save.RewardPoint;
    }

    // a stage only lets go once nothing is left to pick from it, and the last node of a level rolls the run over onto the next floor
    private void SettleStage(MiniGameCCGSaveDB save, MiniGameCCGSaveDBServer row)
    {
        var play = save.CurrentStageDB;
        if (play == null || !play.IsClear || (play.RewardDBs != null && play.RewardDBs.Count > 0))
            return;

        save.CurrentStageDB = null;

        var node = Node(save.LevelId, save.CurrentNodeId);
        if (node != null && node.NextNodeId != null && node.NextNodeId.Count > 0)
            return;

        var current = _excelService.GetTable<MinigameCCGLevelExcelT>().FirstOrDefault(x => x.LevelId == save.LevelId);
        var next = current != null ? Levels(save.CCGId, current.FloorIndex + 1) : [];

        if (next.Count == 0)
        {
            save.Clear = true;
            save.GameOver = true;
            return;
        }

        save.LevelId = next[Random.Shared.Next(next.Count)].LevelId;
        save.CurrentNodeId = 0;
    }

    private List<MiniGameCCGStageRewardDB> RollStageRewards(MinigameCCGLevelStageExcelT stage)
    {
        if (stage.RewardType == CCGStageRewardType.None || stage.RewardCardGroupId == 0)
            return [];

        var pool = _excelService.GetTable<MinigameCCGRewardCardExcelT>().Where(x => x.GroupId == stage.RewardCardGroupId).ToList();
        if (pool.Count == 0)
            return [];

        var draws = Math.Min(Math.Max(1, stage.RewardCount), pool.Count);
        var offered = new List<long>();

        for (var i = 0; i < draws; i++)
        {
            var rarity = RollRarity(stage.CardRarityGroupId);
            var tier = pool.Where(x => x.CardRarity == rarity).ToList();
            if (tier.Count == 0)
                tier = pool;

            var pick = tier[Random.Shared.Next(tier.Count)];
            pool.Remove(pick);
            offered.Add(pick.Id);
        }

        return
        [
            new MiniGameCCGStageRewardDB
            {
                Type = stage.RewardType == CCGStageRewardType.Select ? MiniGameCCGStageRewardType.Select : MiniGameCCGStageRewardType.All,
                RewardIndex = 0,
                RewardIds = offered.ToArray()
            }
        ];
    }

    private int RollRarity(long rarityGroupId)
    {
        var rates = _excelService.GetTable<MinigameCCGRewardCardRateExcelT>().Where(x => x.RarityGroupId == rarityGroupId).ToList();
        if (rates.Count == 0)
            return 0;

        var total = rates.Sum(x => x.Rate);
        if (total <= 0)
            return rates[0].CardRarity;

        var roll = Random.Shared.Next(total);
        foreach (var rate in rates)
        {
            roll -= rate.Rate;
            if (roll < 0)
                return rate.CardRarity;
        }

        return rates[^1].CardRarity;
    }

    // a card joins the deck, a duplicate or an eleventh striker queues on the overflow list until the replace screen resolves it
    private void GrantRewardCard(MiniGameCCGSaveDB save, long rewardCardId)
    {
        var reward = _excelService.GetTable<MinigameCCGRewardCardExcelT>().FirstOrDefault(x => x.Id == rewardCardId);
        if (reward == null)
            return;

        if (reward.EntityType == CCGEntityType.Card)
        {
            save.Deck!.Add(new MiniGameCCGCardDB { CardDBId = save.CardDBIdProvider++, CardId = reward.CardId });
            return;
        }

        var excel = CharacterInfo(reward.CardId);
        var isStriker = excel == null || excel.Type != CCGCharacterType.Special;
        var roster = isStriker ? save.Strikers! : save.Specials!;

        if (roster.Any(x => x.CharacterId == reward.CardId) || roster.Count >= Constants().StrikerMaxEquipCount)
        {
            (isStriker ? save.OverflowedStrikerIds! : save.OverflowedSpecialIds!).Add(reward.CardId);
            return;
        }

        roster.Add(new MiniGameCCGCharacterDB { SlotIndex = roster.Count, CharacterId = reward.CardId, Health = excel?.MaxHealth ?? 0 });
    }

    private int RerollPoints(MiniGameCCGSaveDB save)
        => Constants().BaseRewardRerollPoint + PerkRows(save).Sum(x => x.RerollPoint);

    private List<MinigameCCGPerkExcelT> PerkRows(MiniGameCCGSaveDB save)
    {
        var owned = save.Perks ?? [];
        return _excelService.GetTable<MinigameCCGPerkExcelT>().Where(x => owned.Contains(x.Id)).ToList();
    }

    private static MiniGameCCGSaveDBServer NewRow(long accountServerId, MinigameCCGInfoExcelT info)
        => new()
        {
            AccountServerId = accountServerId,
            EventContentId = info.EventContentId,
            CCGId = info.CCGId
        };

    private async Task<MiniGameCCGSaveDBServer> LoadRow(SchaleDataContext db, long accountServerId, long eventContentId)
        => await db.MiniGameCCGSaves.FirstOrDefaultAsync(x => x.AccountServerId == accountServerId && x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGSaveNotExists, $"No CCG save for event {eventContentId}");

    private static MiniGameCCGSaveDB Save(MiniGameCCGSaveDBServer row)
        => row.SaveDB ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGSaveNotExists, $"No CCG run open for event {row.EventContentId}");

    private static MiniGameCCGStagePlayDB Playing(MiniGameCCGSaveDB save)
        => save.CurrentStageDB ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGPlayingStageNotExists, $"No stage open on level {save.LevelId}");

    private MinigameCCGInfoExcelT Info(long eventContentId)
        => _excelService.GetTable<MinigameCCGInfoExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"MinigameCCGInfoExcel {eventContentId} not found");

    private ConstMinigameCCGExcelT Constants()
        => _excelService.GetTable<ConstMinigameCCGExcelT>().First();

    private MinigameCCGCharacterExcelT? CharacterInfo(long characterId)
        => _excelService.GetTable<MinigameCCGCharacterExcelT>().FirstOrDefault(x => x.Id == characterId);

    private MinigameCCGLevelStageExcelT StageInfo(long stageId)
        => _excelService.GetTable<MinigameCCGLevelStageExcelT>().FirstOrDefault(x => x.Id == stageId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameCCGInvalidOperation, $"MinigameCCGLevelStageExcel {stageId} not found");

    private List<MinigameCCGLevelExcelT> Levels(long ccgId, int floorIndex)
        => _excelService.GetTable<MinigameCCGLevelExcelT>().Where(x => x.CCGId == ccgId && x.FloorIndex == floorIndex).ToList();

    private List<MinigameCCGLevelNodeExcelT> Nodes(long levelId)
        => _excelService.GetTable<MinigameCCGLevelNodeExcelT>().Where(x => x.LevelId == levelId).ToList();

    private MinigameCCGLevelNodeExcelT? Node(long levelId, long nodeId)
        => _excelService.GetTable<MinigameCCGLevelNodeExcelT>().FirstOrDefault(x => x.LevelId == levelId && x.NodeId == nodeId);

    private long StartNode(long levelId)
    {
        var nodes = Nodes(levelId);
        var entered = nodes.SelectMany(x => x.NextNodeId ?? []).ToHashSet();
        var start = nodes.FirstOrDefault(x => !entered.Contains(x.NodeId));
        return start != null ? start.NodeId : 0;
    }

    // the progress readout counts nodes across the whole run, and every level of a floor is the same depth, so one level per floor measures it
    private int TotalStages(long ccgId)
    {
        var total = 0;
        foreach (var floor in _excelService.GetTable<MinigameCCGLevelExcelT>().Where(x => x.CCGId == ccgId).GroupBy(x => x.FloorIndex))
            total += Depth(floor.First().LevelId);
        return total;
    }

    private int Depth(long levelId)
    {
        var nodes = Nodes(levelId).ToDictionary(x => x.NodeId);
        var seen = new Dictionary<long, int>();

        int Walk(long nodeId)
        {
            if (seen.TryGetValue(nodeId, out var known))
                return known;

            seen[nodeId] = 1;
            if (!nodes.TryGetValue(nodeId, out var node))
                return 1;

            var deepest = 0;
            foreach (var next in node.NextNodeId ?? [])
                deepest = Math.Max(deepest, Walk(next));

            seen[nodeId] = deepest + 1;
            return deepest + 1;
        }

        return nodes.Count > 0 ? Walk(StartNode(levelId)) : 0;
    }
}
