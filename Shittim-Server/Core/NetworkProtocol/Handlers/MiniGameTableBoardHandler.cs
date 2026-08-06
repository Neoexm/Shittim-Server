using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.MX.TableBoard;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MiniGameTableBoardHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;
    private readonly TableBoardMapService _mapService;
    private readonly EventContentCollectionService _collectionService;

    // HexLocation.Directions is declared but never initialised by the client, so the cube neighbours live here instead.
    private static readonly (int x, int y, int z)[] HexDirections = [(1, -1, 0), (1, 0, -1), (0, 1, -1), (-1, 1, 0), (-1, 0, 1), (0, -1, 1)];

    private const int PreBattleAttack = 0;
    private const int PreBattleRunAway = 1;
    private const int PostBattleContinue = 0;
    private const int PostBattleRetreat = 1;

    public MiniGameTableBoardHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler,
        TableBoardMapService mapService,
        EventContentCollectionService collectionService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
        _mapService = mapService;
        _collectionService = collectionService;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardSync)]
    public async Task<MiniGameTableBoardSyncResponse> Sync(
        SchaleDataContext db,
        MiniGameTableBoardSyncRequest request,
        MiniGameTableBoardSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await db.MiniGameTableBoards.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);

        TBGBoardSaveDB save;
        if (row == null)
        {
            save = NewBoard(account.ServerId, request.EventContentId);
            row = new MiniGameTableBoardDBServer
            {
                AccountServerId = account.ServerId,
                EventContentId = request.EventContentId
            };
            db.MiniGameTableBoards.Add(row);
        }
        else
        {
            save = ToSave(row);
            DisposeEncounter(save);
        }

        Store(row, save);
        await db.SaveChangesAsync();

        response.SaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardMove)]
    public async Task<MiniGameTableBoardMoveResponse> Move(
        SchaleDataContext db,
        MiniGameTableBoardMoveRequest request,
        MiniGameTableBoardMoveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);
        DisposeEncounter(save);

        if (save.Encounter != null)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardPlayerCannotMove, $"Encounter {save.Encounter.EncounterId} is still open");

        if (save.Player!.HitPoint <= 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardPlayerCannotMove, "The echelon is down");

        var costs = new List<ParcelResult>();

        foreach (var step in request.Steps ?? [])
        {
            var map = CurrentMap(save)!;
            if (!CanStep(save, map, save.Player.Location, step))
                throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardPlayerCannotMove, $"{step.x},{step.y},{step.z} does not neighbour the echelon");

            var obj = map.Objects!.Values.FirstOrDefault(x => x.Activated && SameHex(x.Location, step));
            var objectType = obj != null ? ObjectInfo(obj.UniqueId)?.ObjectType ?? TBGObjectType.None : TBGObjectType.None;

            if (objectType == TBGObjectType.Portal && (CanEnterPortal(save) || map.MapType != TBGThemaType.Normal))
            {
                var other = map.MapType == TBGThemaType.Hidden ? save.MainMap : save.HiddenMap;
                var landing = other != null ? FindObjectLocation(other, other.MapType == TBGThemaType.Hidden ? TBGObjectType.Start : TBGObjectType.Portal) : null;
                if (landing != null)
                {
                    save.CurrentThemaMapType = other!.MapType;
                    save.Player.Location = landing.Value;
                    break;
                }
            }

            if (objectType >= TBGObjectType.EnemyBoss && objectType <= TBGObjectType.TreasureBox && TryInvokeEncounter(save, obj!, costs))
            {
                // a boss or a minion body-blocks the tile it stands on, everything else is walked onto and then triggered
                if (objectType != TBGObjectType.EnemyBoss && objectType != TBGObjectType.EnemyMinion)
                    save.Player.Location = step;

                break;
            }

            save.Player.Location = step;
        }

        Store(row, save);

        if (costs.Count > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, costs, isConsume: true);
            response.ParcelResultDB = resolver.ParcelResult;
        }
        else
        {
            response.ParcelResultDB = AccountSnapshot(db, account);
        }

        await db.SaveChangesAsync();

        response.PlayerDB = save.Player;
        response.SaveDB = save;
        response.EncounterDB = save.Encounter;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardEncounterInput)]
    public async Task<MiniGameTableBoardEncounterInputResponse> EncounterInput(
        SchaleDataContext db,
        MiniGameTableBoardEncounterInputRequest request,
        MiniGameTableBoardEncounterInputResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);

        var encounter = save.Encounter
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardNoActiveEncounter, $"No open encounter on event {request.EventContentId}");

        if (encounter.InvokerServerId != request.ObjectServerId)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidEncounterRequest, $"Encounter is on object {encounter.InvokerServerId}, request claimed {request.ObjectServerId}");

        if (StageOf(encounter) != request.EncounterStage)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidEncounterRequest, $"Encounter is at stage {StageOf(encounter)}, request claimed {request.EncounterStage}");

        var map = CurrentMap(save)!;
        if (!map.Objects!.TryGetValue(encounter.InvokerServerId, out var obj))
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"encounter object '{encounter.InvokerServerId}' is not exists for {encounter.EncounterId}");

        var play = new EncounterPlay();
        switch (encounter)
        {
            case TBGBattleEncounterDB battle:
                ProcessBattle(save, battle, obj, request.SelectedIndex, play);
                break;
            case TBGRandomEncounterDB random:
                ProcessRandom(save, random, obj, request.SelectedIndex, play);
                break;
            case TBGFacilityEncounterDB facility:
                ProcessFacility(save, facility, obj, request.SelectedIndex, play);
                break;
            case TBGTreasureBoxEncounterDB treasure:
                ProcessTreasure(save, treasure, obj, play);
                break;
        }

        Store(row, save);

        ParcelResolver? resolver = null;
        if (play.Costs.Count > 0)
            resolver = await _parcelHandler.BuildParcel(db, account, play.Costs, isConsume: true);

        if (play.Rewards.Count > 0)
        {
            var summarized = play.Rewards
                .GroupBy(x => (x.Type, x.Id))
                .Select(g => new ParcelResult(g.Key.Type, g.Key.Id, g.Sum(x => x.Amount)))
                .ToList();

            resolver = await _parcelHandler.BuildParcel(db, account, summarized, resolver?.ParcelResult);
        }

        await db.SaveChangesAsync();

        response.SaveDB = save;
        response.EncounterDB = save.Encounter;
        response.PlayerDiceResult = play.Dices;
        response.PlayerAddDotEffectResult = play.AddDot;
        response.PlayerDicePlayResult = play.Result;
        response.ParcelResultDB = resolver != null ? resolver.ParcelResult : AccountSnapshot(db, account);
        response.EventContentCollectionDBs = _collectionService.Unlock(db, account, request.EventContentId, CollectionUnlockType.MinigameTBGThemaClear);

        await db.SaveChangesAsync();
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardMoveThema)]
    public async Task<MiniGameTableBoardMoveThemaResponse> MoveThema(
        SchaleDataContext db,
        MiniGameTableBoardMoveThemaRequest request,
        MiniGameTableBoardMoveThemaResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);

        ExitClearedThema(save);
        DisposeEncounter(save);

        if (!IsClearThema(save))
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidClearThemaRequest, $"Thema {save.ThemaIndex} is not cleared");

        EnterNextThema(save, null);

        Store(row, save);
        await db.SaveChangesAsync();

        response.SaveDB = save;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardClearThema)]
    public async Task<MiniGameTableBoardClearThemaResponse> ClearThema(
        SchaleDataContext db,
        MiniGameTableBoardClearThemaRequest request,
        MiniGameTableBoardClearThemaResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);

        ExitClearedThema(save);
        DisposeEncounter(save);

        if (!IsClearThema(save))
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidClearThemaRequest, $"Thema {save.ThemaIndex} is not cleared");

        EnterNextThema(save, request.PreserveItemEffectUniqueIds);

        Store(row, save);
        await db.SaveChangesAsync();

        response.SaveDB = save;
        // the thema's treasure was already paid out when the box was opened, so this only carries the account back
        response.ParcelResultDB = AccountSnapshot(db, account);
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardSweep)]
    public async Task<MiniGameTableBoardSweepResponse> Sweep(
        SchaleDataContext db,
        MiniGameTableBoardSweepRequest request,
        MiniGameTableBoardSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);
        DisposeEncounter(save);

        var season = Season(request.EventContentId);
        if (save.Encounter != null || IsClearThema(save) || save.Round < season.InstantClearRound || save.CurrentThemaMapType != TBGThemaType.Normal || save.MainMap!.IsTutorial)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardSweepConditionFail, $"Thema {save.ThemaIndex} on round {save.Round} cannot be swept");

        if (!save.BestClearRecord!.TryGetValue(save.ThemaIndex, out var record) || record.SweepCosts == null || record.SweepCosts.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardSweepConditionFail, $"Thema {save.ThemaIndex} has never been cleared");

        var resolver = await _parcelHandler.BuildParcel(db, account, record.SweepCosts, isConsume: true);

        var treasure = ThemaReward(save, TBGThemaType.Normal, MiniGameTBGThemaRewardType.TreasureReward);
        if (treasure != null)
        {
            var rewards = ParcelResult.ConvertParcelResult(treasure.RewardParcelType, treasure.RewardParcelId, treasure.RewardParcelAmount);
            resolver = await _parcelHandler.BuildParcel(db, account, rewards, resolver.ParcelResult);
        }

        EnterNextThema(save, request.PreserveItemEffectUniqueIds);

        Store(row, save);
        await db.SaveChangesAsync();

        response.SaveDB = save;
        response.ParcelResultDB = resolver.ParcelResult;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardUseItem)]
    public async Task<MiniGameTableBoardUseItemResponse> UseItem(
        SchaleDataContext db,
        MiniGameTableBoardUseItemRequest request,
        MiniGameTableBoardUseItemResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);
        var items = save.Player!.Items ??= [];

        if (request.ItemSlotIndex < 0 || request.ItemSlotIndex >= items.Count || items[request.ItemSlotIndex].UniqueId != request.UsedItemId)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardItemNotExist, $"Slot {request.ItemSlotIndex} does not hold item {request.UsedItemId}");

        var item = items[request.ItemSlotIndex];
        items.RemoveAt(request.ItemSlotIndex);

        if (!request.IsDiscard)
        {
            var itemInfo = ItemInfo(item.UniqueId)
                ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardItemNotExist, $"MinigameTBGItemExcel {item.UniqueId} not found");

            ApplyItem(save, itemInfo);
        }

        Store(row, save);
        await db.SaveChangesAsync();

        response.PlayerDB = save.Player;
        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_TableBoardResurrect)]
    public async Task<MiniGameTableBoardResurrectResponse> Resurrect(
        SchaleDataContext db,
        MiniGameTableBoardResurrectRequest request,
        MiniGameTableBoardResurrectResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var row = await LoadRow(db, account.ServerId, request.EventContentId);
        var save = ToSave(row);
        DisposeEncounter(save);

        if (save.Player!.HitPoint > 0)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidResurrectRequest, "The echelon is not down");

        var season = Season(request.EventContentId);
        var map = CurrentMap(save);

        if (map != null && !map.IsTutorial && season.EchelonRevivalCostAmount > 0)
        {
            var cost = new ParcelResult(season.EchelonRevivalCostType, season.EchelonRevivalCostId, season.EchelonRevivalCostAmount);
            var resolver = await _parcelHandler.BuildParcel(db, account, cost, isConsume: true);
            response.ParcelResultDB = resolver.ParcelResult;
        }
        else
        {
            response.ParcelResultDB = AccountSnapshot(db, account);
        }

        save.Player.HitPoint = MaxHitPoint(save);
        save.Player.DiceProbModifyParams ??= [];
        save.Player.DiceProbModifyParams[TBGProbModifyCondition.AllyRevive] = 1;

        Store(row, save);
        await db.SaveChangesAsync();

        response.PlayerDB = save.Player;
        return response;
    }

    private sealed class EncounterPlay
    {
        public List<int>? Dices;
        public int? AddDot;
        public TBGDiceRollResult? Result;
        public readonly List<ParcelResult> Rewards = [];
        public readonly List<ParcelResult> Costs = [];
    }

    private void ProcessBattle(TBGBoardSaveDB save, TBGBattleEncounterDB encounter, TBGHexaObjectDB obj, int optionIndex, EncounterPlay play)
    {
        var season = Season(save.EventContentId);

        switch (encounter.Stage)
        {
            case TBGBattleEncounterDB.TBGBattleEncounterStage.BeforeStoryOption:
                obj.BeforeStoryOption = optionIndex;
                encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.PreBattlePhase;
                return;

            case TBGBattleEncounterDB.TBGBattleEncounterStage.PreBattlePhase:
            {
                if (optionIndex != PreBattleAttack && optionIndex != PreBattleRunAway)
                    throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"invalid encounter option for {encounter.EncounterId} : {optionIndex}");

                if (optionIndex == PreBattleAttack)
                {
                    encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.InBattlePhase;
                    return;
                }

                var option = Option(encounter, 0);
                var dices = RollDice(save, play);
                play.Result = dices.Sum() + play.AddDot!.Value >= option.RunawayOrHigherDiceCount ? TBGDiceRollResult.Success : TBGDiceRollResult.Failure;

                if (play.Result != TBGDiceRollResult.Failure)
                {
                    encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.ExitRunAway;
                    OnDicePlaySuccess(save.Player!);
                    obj.FixRewardUniqueIdByIndex = encounter.RewardUniqueIdByIndex;
                }
                else
                {
                    encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.InBattlePhase;
                    OnDicePlayFailure(save.Player!);
                }
                return;
            }

            case TBGBattleEncounterDB.TBGBattleEncounterStage.InBattlePhase:
            {
                var option = Option(encounter, 0);
                if (encounter.RewardUniqueIdByIndex == null || !encounter.RewardUniqueIdByIndex.TryGetValue(0, out var rewardId))
                    throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"encounter object '{obj.ServerId}' is not set random rewards");

                var rewardInfo = RewardInfo(rewardId);
                var dices = RollDice(save, play);
                play.Result = DiceRollResult(option, dices.Sum() + play.AddDot!.Value);

                if (play.Result == TBGDiceRollResult.Failure)
                {
                    DamagePlayer(save, season.AttackDamage);
                    OnDicePlayFailure(save.Player!);
                }
                else
                {
                    DamageObject(obj, play.Result == TBGDiceRollResult.CriticalSuccess ? season.CriticalAttackDamage : season.AttackDamage);
                    OnDicePlaySuccess(save.Player!);

                    if (obj.HitPoint > 0)
                        DamagePlayer(save, season.AttackDamage);
                }

                if (obj.HitPoint > 0)
                {
                    if (save.Player!.HitPoint <= 0)
                    {
                        encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleRetreat;
                        obj.FixRewardUniqueIdByIndex = encounter.RewardUniqueIdByIndex;
                    }
                    else
                    {
                        encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.PostBattlePhase;
                    }
                    return;
                }

                if (ReservesTemporaryItem(save, rewardInfo))
                    encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.EncounterRewardOption;
                else
                {
                    GiveEncounterReward(save, encounter, 0, play);
                    FinalizeObject(save, obj);
                    encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleVictory;
                }
                return;
            }

            case TBGBattleEncounterDB.TBGBattleEncounterStage.PostBattlePhase:
                if (optionIndex != PostBattleContinue && optionIndex != PostBattleRetreat)
                    throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"invalid encounter option for {encounter.EncounterId} : {optionIndex}");

                if (optionIndex == PostBattleRetreat)
                {
                    encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleRetreat;
                    return;
                }

                // another round against the same enemy pays the encounter cost again and ticks the item timers down
                FinalizeEncounter(save);
                PayCostEncounter(save, play.Costs);
                encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.InBattlePhase;
                return;

            case TBGBattleEncounterDB.TBGBattleEncounterStage.EncounterRewardOption:
                if (RewardReceiveIndex() == optionIndex)
                    GiveEncounterReward(save, encounter, 0, play);
                else
                    RemoveTemporaryItem(save.Player!);

                FinalizeObject(save, obj);
                encounter.Stage = TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleVictory;
                return;

            case TBGBattleEncounterDB.TBGBattleEncounterStage.ExitRunAway:
            case TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleVictory:
            case TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleRetreat:
                return;

            default:
                throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"invalid encounter stage {encounter.Stage}");
        }
    }

    private void ProcessRandom(TBGBoardSaveDB save, TBGRandomEncounterDB encounter, TBGHexaObjectDB obj, int optionIndex, EncounterPlay play)
    {
        switch (encounter.Stage)
        {
            case TBGRandomEncounterDB.TBGRandomEncounterStage.BeforeStoryOption:
                obj.BeforeStoryOption = optionIndex;
                encounter.Stage = TBGRandomEncounterDB.TBGRandomEncounterStage.EncounterOption;
                return;

            case TBGRandomEncounterDB.TBGRandomEncounterStage.EncounterOption:
            {
                var option = Option(encounter, optionIndex);
                if (encounter.RewardUniqueIdByIndex == null || !encounter.RewardUniqueIdByIndex.TryGetValue(optionIndex, out var rewardId))
                    throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"encounter object '{obj.ServerId}' is not set random rewards");

                var rewardInfo = RewardInfo(rewardId);
                var dices = RollDice(save, play);
                play.Result = DiceRollResult(option, dices.Sum() + play.AddDot!.Value);

                if (play.Result == TBGDiceRollResult.Failure)
                {
                    FinalizeObject(save, obj);
                    encounter.Stage = TBGRandomEncounterDB.TBGRandomEncounterStage.Exit;
                    OnDicePlayFailure(save.Player!);
                }
                else
                {
                    if (ReservesTemporaryItem(save, rewardInfo))
                        encounter.Stage = TBGRandomEncounterDB.TBGRandomEncounterStage.EncounterRewardOption;
                    else
                    {
                        GiveEncounterReward(save, encounter, optionIndex, play);
                        FinalizeObject(save, obj);
                        encounter.Stage = TBGRandomEncounterDB.TBGRandomEncounterStage.Exit;
                    }

                    OnDicePlaySuccess(save.Player!);
                }

                encounter.EncounterOptionChoice = optionIndex;
                return;
            }

            case TBGRandomEncounterDB.TBGRandomEncounterStage.EncounterRewardOption:
                if (RewardReceiveIndex() == optionIndex)
                    GiveEncounterReward(save, encounter, encounter.EncounterOptionChoice, play);
                else
                    RemoveTemporaryItem(save.Player!);

                FinalizeObject(save, obj);
                encounter.Stage = TBGRandomEncounterDB.TBGRandomEncounterStage.Exit;
                return;

            case TBGRandomEncounterDB.TBGRandomEncounterStage.Exit:
                return;

            default:
                throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"invalid encounter stage {encounter.Stage}");
        }
    }

    private void ProcessFacility(TBGBoardSaveDB save, TBGFacilityEncounterDB encounter, TBGHexaObjectDB obj, int optionIndex, EncounterPlay play)
    {
        if (encounter.Stage == TBGFacilityEncounterDB.TBGFacilityEncounterStage.Exit)
            return;

        if (encounter.Stage == TBGFacilityEncounterDB.TBGFacilityEncounterStage.EncounterRewardOption)
        {
            if (RewardReceiveIndex() != optionIndex)
            {
                // declining leaves the encounter parked on this stage, so the client can offer the item again
                RemoveTemporaryItem(save.Player!);
                return;
            }

            GiveEncounterReward(save, encounter, encounter.EncounterOptionChoice, play);
            FinalizeObject(save, obj);
            encounter.Stage = TBGFacilityEncounterDB.TBGFacilityEncounterStage.Exit;
            return;
        }

        if (encounter.Stage != TBGFacilityEncounterDB.TBGFacilityEncounterStage.EncounterOption)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"invalid encounter stage {encounter.Stage}");

        if (encounter.RewardUniqueIdByIndex == null || !encounter.RewardUniqueIdByIndex.TryGetValue(optionIndex, out var rewardId))
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, $"encounter object '{obj.ServerId}' is not set random rewards");

        if (ReservesTemporaryItem(save, RewardInfo(rewardId)))
            encounter.Stage = TBGFacilityEncounterDB.TBGFacilityEncounterStage.EncounterRewardOption;
        else
        {
            GiveEncounterReward(save, encounter, optionIndex, play);
            FinalizeObject(save, obj);
            encounter.Stage = TBGFacilityEncounterDB.TBGFacilityEncounterStage.Exit;
        }

        encounter.EncounterOptionChoice = optionIndex;
    }

    private void ProcessTreasure(TBGBoardSaveDB save, TBGTreasureBoxEncounterDB encounter, TBGHexaObjectDB obj, EncounterPlay play)
    {
        if (encounter.Stage == TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.PostReceiveReward)
        {
            if (encounter.IsRealTreasure)
            {
                obj.BeforeStoryOption = null;
                encounter.Stage = TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitToContinue;
            }
            else
            {
                encounter.Stage = TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitTreasureFake;
            }
            return;
        }

        if (encounter.Stage != TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.PreReceiveReward)
            return;

        GiveEncounterReward(save, encounter, 0, play);
        obj.HitPoint = 0;
        encounter.Stage = TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.PostReceiveReward;

        if (!encounter.IsRealTreasure)
        {
            FinalizeObject(save, obj);
            encounter.Stage = TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitTreasureFake;
        }
    }

    private TBGBoardSaveDB NewBoard(long accountServerId, long eventContentId)
    {
        var season = Season(eventContentId);
        var save = new TBGBoardSaveDB
        {
            AccountId = accountServerId,
            EventContentId = eventContentId,
            Round = 1,
            BestClearRecord = [],
            HiddenTreasureRecord = [],
            HiddenPotalOpenConditionRecord = [],
            Player = new TBGPlayerDB
            {
                EventContentId = eventContentId,
                HitPoint = season.DefaultEchelonHp,
                DiceId = season.DefaultItemDiceId,
                DiceProbModifyParams = [],
                Items = [],
                ItemEffects = []
            }
        };

        EnterThema(save, season.StartThemaIndex);
        return save;
    }

    private void EnterThema(TBGBoardSaveDB save, int themaIndex)
    {
        save.CurrentThemaMapType = TBGThemaType.Normal;
        save.ThemaIndex = themaIndex;
        save.MainMap = BuildMap(save, themaIndex, TBGThemaType.Normal);
        save.HiddenMap = Thema(save.EventContentId, themaIndex, TBGThemaType.Hidden) != null ? BuildMap(save, themaIndex, TBGThemaType.Hidden) : null;

        save.Player!.Location = FindObjectLocation(save.MainMap, TBGObjectType.Start)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"Thema {themaIndex} of event {save.EventContentId} has no start tile");
    }

    private void EnterNextThema(TBGBoardSaveDB save, List<long>? preserveItemEffectUniqueIds)
    {
        DisposeThema(save);

        var next = NextThemaIndex(save.EventContentId, save.ThemaIndex);
        if (next >= 0)
        {
            EnterThema(save, next);
            return;
        }

        var season = Season(save.EventContentId);
        var preserve = preserveItemEffectUniqueIds ?? [];
        if (preserve.Count > save.Round * season.RoundItemSelectLimit)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidClearThemaRequest, $"Round {save.Round} carries at most {save.Round * season.RoundItemSelectLimit} effects, request kept {preserve.Count}");

        var player = save.Player!;
        var kept = preserve.ToHashSet();
        player.ItemEffects = (player.ItemEffects ?? []).Where(x => kept.Contains(x.ItemUniqueId)).ToList();
        player.Items = [];
        save.Round++;
        player.DiceId = season.DefaultItemDiceId;
        player.HitPoint = MaxHitPoint(save);

        EnterThema(save, season.LoopThemaIndex);
    }

    private void DisposeThema(TBGBoardSaveDB save)
    {
        if (save.CurrentThemaMapType == TBGThemaType.Hidden && save.HiddenMap == null)
            return;

        var thema = Thema(save.EventContentId, save.ThemaIndex, TBGThemaType.Normal);
        if (thema != null && !save.BestClearRecord!.ContainsKey(save.ThemaIndex))
        {
            var season = Season(save.EventContentId);
            save.BestClearRecord.Add(save.ThemaIndex, new TBGThemaClearRecord
            {
                ThemaIndex = save.ThemaIndex,
                SweepCosts = ParcelInfo.CreateParcelInfo(season.EventUseCostType, season.EventUseCostId, thema.InstantClearCostAmount)
            });
        }

        var guide = (save.Player!.ItemEffects ?? []).FirstOrDefault(x => x.ItemType == TBGItemType.Guide);
        if (guide != null)
        {
            save.Player.ItemEffects!.Remove(guide);
            AddHitPoint(save, 0);
        }

        var map = CurrentMap(save);
        if (map == null)
            return;

        if (!map.IsTutorial)
        {
            save.CurrentThemaMapType = TBGThemaType.None;
            save.MainMap = null;
            save.HiddenMap = null;
            return;
        }

        save.Player.ItemEffects = [];
        save.Player.Items = [];
        save.Player.DiceId = Season(save.EventContentId).DefaultItemDiceId;
        save.Player.HitPoint = MaxHitPoint(save);
    }

    private TBGHexaMapDB BuildMap(TBGBoardSaveDB save, int themaIndex, TBGThemaType mapType)
    {
        var thema = Thema(save.EventContentId, themaIndex, mapType)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"MinigameTBGThemaExcel {save.EventContentId}/{themaIndex}/{mapType} not found");

        var data = _mapService.Load(thema.ThemaMap)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"TableBoard map {thema.ThemaMap} not found");

        var map = new TBGHexaMapDB { MapType = mapType, IsTutorial = thema.IsTutorial, Objects = [] };

        foreach (var group in data.Spawns.GroupBy(x => x.ShuffleGroupId))
        {
            var spawns = group.ToList();
            var locations = spawns.Select(x => x.Location).OrderBy(_ => Random.Shared.Next()).ToList();
            for (var i = 0; i < spawns.Count; i++)
                SpawnObject(save, map, themaIndex, spawns[i], locations[i]);
        }

        var boxes = map.Objects.Values
            .Where(x => ObjectInfo(x.UniqueId)?.ObjectType == TBGObjectType.TreasureBox)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        for (var i = 0; i < boxes.Count; i++)
        {
            boxes[i].IsFakeTreasure = i != 0;
            if (i != 0)
                continue;

            var real = RealTreasureEncounter(save.EventContentId, themaIndex, mapType);
            if (real != null)
                boxes[i].EncounterId = real.UniqueId;

            // a hidden thema only ever pays its treasure once, so on a revisit the box comes up already open
            if (mapType == TBGThemaType.Hidden && save.HiddenTreasureRecord!.Contains(themaIndex))
            {
                boxes[i].HitPoint = 0;
                boxes[i].EncounterCostAlreadyPaid = true;
            }
        }

        return map;
    }

    private void SpawnObject(TBGBoardSaveDB save, TBGHexaMapDB map, int themaIndex, TBGHexaSpawnData spawn, HexLocation location)
    {
        List<long> uniqueIds;
        if (spawn.SpawnRule == TBGHexaObjectSpawnRule.ObjectType)
            uniqueIds = _excelService.GetTable<MinigameTBGObjectExcelT>().Where(x => spawn.ObjectTypes!.Contains(x.ObjectType)).Select(x => x.UniqueId).ToList();
        else if (spawn.SpawnRule == TBGHexaObjectSpawnRule.ObjectId)
            uniqueIds = spawn.UniqueIds!;
        else
            return;

        var objectInfo = ObjectInfo(uniqueIds[Random.Shared.Next(uniqueIds.Count)])
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"MinigameTBGObjectExcel for the spawn at {location.x},{location.y},{location.z} not found");

        var encounterId = -1L;
        if (objectInfo.ObjectType >= TBGObjectType.EnemyBoss && objectInfo.ObjectType <= TBGObjectType.TreasureBox)
        {
            var candidates = EncounterInfos(save.EventContentId, themaIndex, mapType: map.MapType, objectInfo.ObjectType);
            if (objectInfo.ObjectType == TBGObjectType.TreasureBox)
                candidates = candidates.Where(x => x.AllThema).ToList();

            if (candidates.Count == 0)
                throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"No MinigameTBGEncounterExcel for {objectInfo.ObjectType} on thema {themaIndex} of event {save.EventContentId}");

            encounterId = candidates[Random.Shared.Next(candidates.Count)].UniqueId;
        }

        var serverId = map.Objects!.Count > 0 ? map.Objects.Values.Max(x => x.ServerId) + 1 : 1;
        var obj = new TBGHexaObjectDB
        {
            ServerId = serverId,
            UniqueId = objectInfo.UniqueId,
            EncounterId = encounterId,
            MapType = map.MapType,
            Location = location,
            Activated = true
        };

        var season = Season(save.EventContentId);
        if (objectInfo.ObjectType == TBGObjectType.EnemyBoss)
            obj.HitPoint = season.EnemyBossHP;
        else if (objectInfo.ObjectType == TBGObjectType.EnemyMinion)
            obj.HitPoint = season.EnemyMinionHP;
        else if (objectInfo.ObjectType == TBGObjectType.TreasureBox)
            obj.HitPoint = 1;

        map.Objects.Add(serverId, obj);
    }

    private bool TryInvokeEncounter(TBGBoardSaveDB save, TBGHexaObjectDB obj, List<ParcelResult> costs)
    {
        var objectInfo = ObjectInfo(obj.UniqueId);
        if (objectInfo == null || obj.EncounterId < 0)
            return false;

        TBGEncounterDB? encounter = objectInfo.ObjectType switch
        {
            TBGObjectType.EnemyBoss or TBGObjectType.EnemyMinion => new TBGBattleEncounterDB(),
            TBGObjectType.Random => new TBGRandomEncounterDB(),
            TBGObjectType.Facility => new TBGFacilityEncounterDB(),
            TBGObjectType.TreasureBox => new TBGTreasureBoxEncounterDB { IsRealTreasure = obj.IsFakeTreasure == false },
            _ => null
        };

        if (encounter == null)
            return false;

        encounter.EncounterId = obj.EncounterId;
        encounter.InvokerServerId = obj.ServerId;
        encounter.ObjectType = (int)objectInfo.ObjectType;

        save.Encounter = encounter;
        InitializeEncounter(encounter, obj, objectInfo);
        SetEncounterRewards(encounter);
        PayCostEncounter(save, costs);
        return true;
    }

    private void InitializeEncounter(TBGEncounterDB encounter, TBGHexaObjectDB obj, MinigameTBGObjectExcelT objectInfo)
    {
        switch (encounter)
        {
            case TBGBattleEncounterDB battle:
                battle.Stage = CanSkipBeforeStory(obj, objectInfo)
                    ? TBGBattleEncounterDB.TBGBattleEncounterStage.PreBattlePhase
                    : TBGBattleEncounterDB.TBGBattleEncounterStage.BeforeStoryOption;

                // a battle that was run from or retreated out of keeps the reward roll it already made
                if (obj.FixRewardUniqueIdByIndex != null && obj.FixRewardUniqueIdByIndex.Count > 0)
                    battle.RewardUniqueIdByIndex = obj.FixRewardUniqueIdByIndex;
                break;

            case TBGRandomEncounterDB random:
                random.Stage = CanSkipBeforeStory(obj, objectInfo)
                    ? TBGRandomEncounterDB.TBGRandomEncounterStage.EncounterOption
                    : TBGRandomEncounterDB.TBGRandomEncounterStage.BeforeStoryOption;
                break;

            case TBGFacilityEncounterDB facility:
                facility.Stage = TBGFacilityEncounterDB.TBGFacilityEncounterStage.EncounterOption;
                break;

            case TBGTreasureBoxEncounterDB treasure:
                if (obj.HitPoint > 0)
                    treasure.Stage = TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.PreReceiveReward;
                else
                    treasure.Stage = treasure.IsRealTreasure
                        ? TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.PostReceiveReward
                        : TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitTreasureFake;
                break;
        }
    }

    private bool CanSkipBeforeStory(TBGHexaObjectDB obj, MinigameTBGObjectExcelT objectInfo)
    {
        if (objectInfo.ObjectType > TBGObjectType.Random)
            return false;

        if (string.IsNullOrEmpty(EncounterInfo(obj.EncounterId).BeforeStoryLocalize))
            return true;

        return obj.BeforeStoryOption != null;
    }

    private void SetEncounterRewards(TBGEncounterDB encounter)
    {
        if (encounter.RewardUniqueIdByIndex != null && encounter.RewardUniqueIdByIndex.Count > 0)
            return;

        var options = Options(EncounterInfo(encounter.EncounterId).OptionGroupId);
        if (options.Count == 0)
            return;

        encounter.RewardUniqueIdByIndex = [];
        var index = 0;
        foreach (var slot in options.OrderBy(x => x.Key))
        {
            var reward = RollReward(slot.Value.OptionSuccessRewardGroupId);
            if (reward == null)
                continue;

            encounter.RewardUniqueIdByIndex.Add(index, reward.UniqueId);
            index++;
        }
    }

    private void PayCostEncounter(TBGBoardSaveDB save, List<ParcelResult> costs)
    {
        var encounter = save.Encounter;
        if (encounter == null)
            return;

        var map = CurrentMap(save);
        if (map == null || !map.Objects!.TryGetValue(encounter.InvokerServerId, out var obj))
            return;

        var objectInfo = ObjectInfo(obj.UniqueId);
        if (objectInfo == null)
            return;

        if (!objectInfo.ReEncounterCost && obj.EncounterCostAlreadyPaid)
            return;

        if (objectInfo.ObjectCostAmount > 0)
            costs.Add(new ParcelResult(objectInfo.ObjectCostType, objectInfo.ObjectCostId, objectInfo.ObjectCostAmount));

        if (objectInfo.ObjectType != TBGObjectType.TreasureBox)
            encounter.ShouldDecreaseItemEffectCounter = true;

        obj.EncounterCostAlreadyPaid = true;
    }

    private static void FinalizeEncounter(TBGBoardSaveDB save)
    {
        var encounter = save.Encounter;
        if (encounter == null || !encounter.ShouldDecreaseItemEffectCounter)
            return;

        var effects = save.Player!.ItemEffects;
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect.ItemType != TBGItemType.Guide && effect.EffectType != TBGItemEffectType.PermanentContinuity && effect.RemainEncounterCounter > 0)
                    effect.RemainEncounterCounter--;
            }

            effects.RemoveAll(x => x.EffectType != TBGItemEffectType.PermanentContinuity && x.RemainEncounterCounter <= 0);
        }

        encounter.ShouldDecreaseItemEffectCounter = false;
    }

    private void FinalizeObject(TBGBoardSaveDB save, TBGHexaObjectDB obj)
    {
        obj.Activated = false;

        if (!CanEnterPortal(save) || save.HiddenPotalOpenConditionRecord!.Contains(save.ThemaIndex))
            return;

        save.HiddenPotalOpenConditionRecord.Add(save.ThemaIndex);
    }

    private static void DisposeEncounter(TBGBoardSaveDB save)
    {
        if (save.Encounter == null || !IsTerminal(save.Encounter))
            return;

        FinalizeEncounter(save);
        save.Encounter = null;
    }

    // popping the real box is what ends the thema, but the client leaves the encounter parked on PostReceiveReward until it asks to move on
    private void ExitClearedThema(TBGBoardSaveDB save)
    {
        if (save.Encounter is not TBGTreasureBoxEncounterDB treasure
            || treasure.Stage != TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.PostReceiveReward
            || !treasure.IsRealTreasure)
            return;

        var map = CurrentMap(save);
        if (map == null || !map.Objects!.TryGetValue(treasure.InvokerServerId, out var obj))
            return;

        FinalizeObject(save, obj);
        treasure.Stage = TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitToClearThema;
    }

    private void GiveEncounterReward(TBGBoardSaveDB save, TBGEncounterDB encounter, int optionIndex, EncounterPlay play)
    {
        if (encounter is TBGTreasureBoxEncounterDB treasure)
        {
            if (save.CurrentThemaMapType == TBGThemaType.Hidden)
            {
                if (!save.HiddenTreasureRecord!.Contains(save.ThemaIndex))
                {
                    var hidden = ThemaReward(save, TBGThemaType.Hidden, MiniGameTBGThemaRewardType.HiddenThemaTreasureReward);
                    if (hidden != null)
                    {
                        save.HiddenTreasureRecord.Add(save.ThemaIndex);
                        play.Rewards.AddRange(ParcelResult.ConvertParcelResult(hidden.RewardParcelType, hidden.RewardParcelId, hidden.RewardParcelAmount));
                    }
                }
            }
            else
            {
                var thema = ThemaReward(save, TBGThemaType.Normal, treasure.IsRealTreasure ? MiniGameTBGThemaRewardType.TreasureReward : MiniGameTBGThemaRewardType.EmptyTreasureReward);
                if (thema != null)
                    play.Rewards.AddRange(ParcelResult.ConvertParcelResult(thema.RewardParcelType, thema.RewardParcelId, thema.RewardParcelAmount));
            }
        }

        if (encounter.RewardUniqueIdByIndex == null || !encounter.RewardUniqueIdByIndex.TryGetValue(optionIndex, out var rewardId))
            return;

        var rewardInfo = RewardInfo(rewardId);
        if (rewardInfo == null)
            return;

        switch (rewardInfo.TBGOptionSuccessType)
        {
            case TBGOptionSuccessType.TBGDiceAcquire:
                if (DiceInfos(save.EventContentId, rewardInfo.Paremeter).Count > 0)
                    save.Player!.DiceId = rewardInfo.Paremeter;
                break;
            case TBGOptionSuccessType.ItemAcquire:
                if (rewardInfo.Amount > 0)
                    play.Rewards.Add(new ParcelResult(rewardInfo.ParcelType, rewardInfo.ParcelId, rewardInfo.Amount));
                break;
            case TBGOptionSuccessType.TBGItemAcquire:
                ReceiveTemporaryItem(save);
                break;
        }
    }

    private bool ReservesTemporaryItem(TBGBoardSaveDB save, MinigameTBGEncounterRewardExcelT? rewardInfo)
    {
        if (rewardInfo == null || (rewardInfo.TBGOptionSuccessType != TBGOptionSuccessType.TBGItemAcquire && rewardInfo.TBGOptionSuccessType != TBGOptionSuccessType.TBGDiceAcquire))
            return false;

        var itemInfo = ItemInfo(rewardInfo.Paremeter);
        if (itemInfo != null)
            save.Player!.TemporaryItem = new TBGItemDB { UniqueId = itemInfo.UniqueId };

        return true;
    }

    private void ReceiveTemporaryItem(TBGBoardSaveDB save)
    {
        var player = save.Player!;
        if (player.TemporaryItem == null)
            return;

        var item = player.TemporaryItem;
        player.TemporaryItem = null;
        player.Items ??= [];

        // a full rack means the pickup is used on the spot instead of stored
        if (player.Items.Count >= Season(save.EventContentId).ItemSlot)
        {
            var itemInfo = ItemInfo(item.UniqueId);
            if (itemInfo != null)
                ApplyItem(save, itemInfo);

            return;
        }

        player.Items.Add(item);
    }

    private static void RemoveTemporaryItem(TBGPlayerDB player)
    {
        if (player.TemporaryItem == null)
            return;

        player.Items?.Remove(player.TemporaryItem);
        player.TemporaryItem = null;
    }

    private void ApplyItem(TBGBoardSaveDB save, MinigameTBGItemExcelT itemInfo)
    {
        if (itemInfo.ItemType == TBGItemType.Heal)
        {
            AddHitPoint(save, itemInfo.ItemParameter);
            return;
        }

        if (itemInfo.ItemType < TBGItemType.HealExpansion || itemInfo.ItemType > TBGItemType.DiceResultConfirm)
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidItemUse, $"MinigameTBGItem {itemInfo.UniqueId} is a {itemInfo.ItemType} and cannot be used from the rack");

        if (itemInfo.TBGItemEffectType == TBGItemEffectType.PermanentContinuity)
        {
            var season = Season(save.EventContentId);
            if (itemInfo.ItemType == TBGItemType.HealExpansion && MaxHitPoint(save) >= season.MaxHp)
                return;

            if (itemInfo.ItemType == TBGItemType.DiceResultValue && PermanentDiceAddDot(save) >= season.MaxDicePlus)
                return;
        }
        else if (itemInfo.TBGItemEffectType != TBGItemEffectType.TemporaryContinuation)
        {
            throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidItemUse, $"MinigameTBGItem {itemInfo.UniqueId} has effect type {itemInfo.TBGItemEffectType}");
        }

        var effects = save.Player!.ItemEffects ??= [];
        if (itemInfo.TBGItemEffectType != TBGItemEffectType.PermanentContinuity)
        {
            var duplicate = effects.FirstOrDefault(x => x.ItemType == itemInfo.ItemType && x.EffectType == itemInfo.TBGItemEffectType);
            if (duplicate != null)
                effects.Remove(duplicate);
        }

        effects.Add(new TBGItemEffectDB
        {
            ItemUniqueId = itemInfo.UniqueId,
            ItemType = itemInfo.ItemType,
            EffectType = itemInfo.TBGItemEffectType,
            RemainEncounterCounter = itemInfo.EncounterCount
        });

        AddHitPoint(save, 0);
    }

    private int MaxHitPoint(TBGBoardSaveDB save)
    {
        var season = Season(save.EventContentId);
        return Math.Min(season.DefaultEchelonHp + ActiveEffects(save.Player!, TBGItemType.HealExpansion).Sum(x => ItemInfo(x.ItemUniqueId)?.ItemParameter ?? 0), season.MaxHp);
    }

    private int PermanentDiceAddDot(TBGBoardSaveDB save)
        => ActiveEffects(save.Player!, TBGItemType.DiceResultValue)
            .Where(x => x.EffectType == TBGItemEffectType.PermanentContinuity)
            .Sum(x => ItemInfo(x.ItemUniqueId)?.ItemParameter ?? 0);

    // the clamp runs against a MaxHitPoint that may have just dropped, so healing zero is how the engine re-caps the echelon
    private void AddHitPoint(TBGBoardSaveDB save, int healPoint)
        => save.Player!.HitPoint = Math.Clamp(save.Player.HitPoint + healPoint, 0, MaxHitPoint(save));

    private void DamagePlayer(TBGBoardSaveDB save, int amount)
    {
        if (ActiveEffects(save.Player!, TBGItemType.Defence).Any())
            return;

        var critical = ActiveEffects(save.Player!, TBGItemType.DefenceCritical).OrderBy(x => x.EffectType).FirstOrDefault();
        if (critical != null)
            amount = Math.Min(amount, ItemInfo(critical.ItemUniqueId)?.ItemParameter ?? amount);

        if (amount != 0)
            AddHitPoint(save, -amount);
    }

    private static void DamageObject(TBGHexaObjectDB obj, int amount)
    {
        if (obj.HitPoint == null)
            return;

        obj.HitPoint = Math.Max(0, obj.HitPoint.Value - amount);
    }

    private static IEnumerable<TBGItemEffectDB> ActiveEffects(TBGPlayerDB player, TBGItemType itemType)
        => (player.ItemEffects ?? []).Where(x => x.ItemType == itemType && (x.EffectType == TBGItemEffectType.PermanentContinuity || (x.EffectType == TBGItemEffectType.TemporaryContinuation && x.RemainEncounterCounter > 0)));

    private List<int> RollDice(TBGBoardSaveDB save, EncounterPlay play)
    {
        var player = save.Player!;
        var count = DiceCount(Season(save.EventContentId));
        var dices = new List<int>();

        var forced = ActiveEffects(player, TBGItemType.DiceResultConfirm).OrderBy(x => x.EffectType).FirstOrDefault();
        if (forced != null)
        {
            var dot = ItemInfo(forced.ItemUniqueId)?.ItemParameter ?? 0;
            for (var i = 0; i < count; i++)
                dices.Add(dot);
        }
        else
        {
            var faces = DiceInfos(save.EventContentId, player.DiceId);
            if (faces.Count == 0)
                throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"MinigameTBGDiceExcel group {player.DiceId} of event {save.EventContentId} not found");

            var weights = faces.Select(x => DiceWeight(player, x)).ToList();
            var total = weights.Sum();
            for (var i = 0; i < count; i++)
            {
                var roll = total > 0 ? Random.Shared.Next(total) : 0;
                var picked = faces[^1];
                for (var f = 0; f < faces.Count; f++)
                {
                    roll -= weights[f];
                    if (roll < 0)
                    {
                        picked = faces[f];
                        break;
                    }
                }

                dices.Add(picked.DiceResult);
            }
        }

        play.Dices = dices;
        play.AddDot = ActiveEffects(player, TBGItemType.DiceResultValue).Sum(x => ItemInfo(x.ItemUniqueId)?.ItemParameter ?? 0);
        return dices;
    }

    private static int DiceWeight(TBGPlayerDB player, MinigameTBGDiceExcelT dice)
    {
        var weight = dice.Prob;
        if (player.DiceProbModifyParams == null || player.DiceProbModifyParams.Count == 0)
            return weight;

        foreach (var modifier in player.DiceProbModifyParams.Where(x => x.Value > 0))
        {
            var slot = dice.ProbModifyCondition.IndexOf(modifier.Key);
            if (slot < 0)
                continue;

            var delta = dice.ProbModifyValue[slot] * modifier.Value;
            weight = delta < 0 ? Math.Max(weight + delta, dice.ProbModifyLimit[slot]) : Math.Min(weight + delta, dice.ProbModifyLimit[slot]);
        }

        return weight;
    }

    private static TBGDiceRollResult DiceRollResult(MinigameTBGEncounterOptionExcelT option, int dots)
    {
        if (option.OptionGreatSuccessOrHigherDiceCount > 0 && dots >= option.OptionGreatSuccessOrHigherDiceCount)
            return TBGDiceRollResult.CriticalSuccess;

        return dots >= option.OptionSuccessOrHigherDiceCount ? TBGDiceRollResult.Success : TBGDiceRollResult.Failure;
    }

    private static void OnDicePlaySuccess(TBGPlayerDB player)
    {
        if (player.DiceProbModifyParams == null)
            return;

        player.DiceProbModifyParams.Remove(TBGProbModifyCondition.AllyRevive);
        player.DiceProbModifyParams.Remove(TBGProbModifyCondition.DicePlayFail);
    }

    private static void OnDicePlayFailure(TBGPlayerDB player)
    {
        player.DiceProbModifyParams ??= [];
        player.DiceProbModifyParams.Remove(TBGProbModifyCondition.AllyRevive);
        player.DiceProbModifyParams.TryGetValue(TBGProbModifyCondition.DicePlayFail, out var stack);
        player.DiceProbModifyParams[TBGProbModifyCondition.DicePlayFail] = stack + 1;
    }

    private bool CanEnterPortal(TBGBoardSaveDB save)
    {
        var thema = Thema(save.EventContentId, save.ThemaIndex, save.CurrentThemaMapType);
        if (thema == null || thema.PortalCondition == null || thema.PortalCondition.Count == 0)
            return false;

        if (save.HiddenPotalOpenConditionRecord!.Contains(save.ThemaIndex))
            return true;

        var map = CurrentMap(save);
        for (var i = 0; i < thema.PortalCondition.Count; i++)
        {
            var parameter = thema.PortalConditionParameter[i];
            if (thema.PortalCondition[i] == TBGPortalCondition.Round && save.Round < int.Parse(parameter))
                return false;

            if (thema.PortalCondition[i] == TBGPortalCondition.ObjectAllEncounter && map != null)
            {
                var gate = Enum.Parse<TBGObjectType>(parameter);
                if (map.Objects!.Values.Any(x => x.Activated && ObjectInfo(x.UniqueId)?.ObjectType == gate))
                    return false;
            }
        }

        return true;
    }

    private bool IsClearThema(TBGBoardSaveDB save)
    {
        var map = CurrentMap(save);
        if (map == null)
            return false;

        var box = map.Objects!.Values.FirstOrDefault(x => x.IsFakeTreasure == false && ObjectInfo(x.UniqueId)?.ObjectType == TBGObjectType.TreasureBox);
        return box != null && box.HitPoint == 0;
    }

    private bool CanStep(TBGBoardSaveDB save, TBGHexaMapDB map, HexLocation from, HexLocation to)
    {
        if (!HexDirections.Any(d => from.x + d.x == to.x && from.y + d.y == to.y && from.z + d.z == to.z))
            return false;

        var thema = Thema(save.EventContentId, save.ThemaIndex, map.MapType);
        var data = thema != null ? _mapService.Load(thema.ThemaMap) : null;
        return data != null && data.Tiles.Any(t => (t.TileType == TBGTileType.Movable || t.TileType == TBGTileType.Start) && SameHex(t.Location, to));
    }

    private HexLocation? FindObjectLocation(TBGHexaMapDB map, TBGObjectType objectType)
    {
        var candidates = map.Objects!.Values.Where(x => ObjectInfo(x.UniqueId)?.ObjectType == objectType).ToList();
        return candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)].Location : null;
    }

    private static TBGHexaMapDB? CurrentMap(TBGBoardSaveDB save)
        => save.CurrentThemaMapType == TBGThemaType.Hidden ? save.HiddenMap : save.MainMap;

    private static bool SameHex(HexLocation a, HexLocation b) => a.x == b.x && a.y == b.y && a.z == b.z;

    private static bool IsTerminal(TBGEncounterDB encounter)
    {
        switch (encounter)
        {
            case TBGBattleEncounterDB battle:
                return battle.Stage == TBGBattleEncounterDB.TBGBattleEncounterStage.ExitRunAway
                    || battle.Stage == TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleVictory
                    || battle.Stage == TBGBattleEncounterDB.TBGBattleEncounterStage.ExitBattleRetreat;
            case TBGRandomEncounterDB random:
                return random.Stage == TBGRandomEncounterDB.TBGRandomEncounterStage.Exit;
            case TBGFacilityEncounterDB facility:
                return facility.Stage == TBGFacilityEncounterDB.TBGFacilityEncounterStage.Exit;
            case TBGTreasureBoxEncounterDB treasure:
                return treasure.Stage == TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitTreasureFake
                    || treasure.Stage == TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitToClearThema
                    || treasure.Stage == TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage.ExitToContinue;
            default:
                return false;
        }
    }

    private static int StageOf(TBGEncounterDB encounter)
    {
        switch (encounter)
        {
            case TBGBattleEncounterDB battle:
                return (int)battle.Stage;
            case TBGRandomEncounterDB random:
                return (int)random.Stage;
            case TBGFacilityEncounterDB facility:
                return (int)facility.Stage;
            case TBGTreasureBoxEncounterDB treasure:
                return (int)treasure.Stage;
            default:
                return 0;
        }
    }

    private static TBGBoardSaveDB ToSave(MiniGameTableBoardDBServer row)
    {
        var save = new TBGBoardSaveDB
        {
            AccountId = row.AccountServerId,
            EventContentId = row.EventContentId,
            Round = row.Round,
            ThemaIndex = row.ThemaIndex,
            CurrentThemaMapType = row.CurrentThemaMapType,
            MainMap = row.MainMap,
            HiddenMap = row.HiddenMap,
            Player = row.Player,
            BestClearRecord = row.BestClearRecord,
            HiddenTreasureRecord = row.HiddenTreasureRecord,
            HiddenPotalOpenConditionRecord = row.HiddenPotalOpenConditionRecord
        };

        if (!row.HasEncounter)
            return save;

        save.Encounter = row.EncounterObjectType switch
        {
            (int)TBGObjectType.EnemyBoss or (int)TBGObjectType.EnemyMinion => new TBGBattleEncounterDB { Stage = (TBGBattleEncounterDB.TBGBattleEncounterStage)row.EncounterStage },
            (int)TBGObjectType.Random => new TBGRandomEncounterDB { Stage = (TBGRandomEncounterDB.TBGRandomEncounterStage)row.EncounterStage, EncounterOptionChoice = row.EncounterOptionChoice },
            (int)TBGObjectType.Facility => new TBGFacilityEncounterDB { Stage = (TBGFacilityEncounterDB.TBGFacilityEncounterStage)row.EncounterStage, EncounterOptionChoice = row.EncounterOptionChoice },
            (int)TBGObjectType.TreasureBox => new TBGTreasureBoxEncounterDB { Stage = (TBGTreasureBoxEncounterDB.TBGTreasureEncounterStage)row.EncounterStage, IsRealTreasure = row.EncounterIsRealTreasure },
            _ => null
        };

        if (save.Encounter != null)
        {
            save.Encounter.EncounterId = row.EncounterId;
            save.Encounter.InvokerServerId = row.EncounterInvokerServerId;
            save.Encounter.ObjectType = row.EncounterObjectType;
            save.Encounter.ShouldDecreaseItemEffectCounter = row.EncounterShouldDecreaseItemEffectCounter;
            save.Encounter.RewardUniqueIdByIndex = row.EncounterRewards;
        }

        return save;
    }

    private static void Store(MiniGameTableBoardDBServer row, TBGBoardSaveDB save)
    {
        row.Round = save.Round;
        row.ThemaIndex = save.ThemaIndex;
        row.CurrentThemaMapType = save.CurrentThemaMapType;
        row.MainMap = save.MainMap;
        row.HiddenMap = save.HiddenMap;
        row.Player = save.Player;
        row.BestClearRecord = save.BestClearRecord ?? [];
        row.HiddenTreasureRecord = save.HiddenTreasureRecord ?? [];
        row.HiddenPotalOpenConditionRecord = save.HiddenPotalOpenConditionRecord ?? [];

        row.HasEncounter = save.Encounter != null;
        if (save.Encounter == null)
            return;

        row.EncounterId = save.Encounter.EncounterId;
        row.EncounterInvokerServerId = save.Encounter.InvokerServerId;
        row.EncounterObjectType = save.Encounter.ObjectType;
        row.EncounterShouldDecreaseItemEffectCounter = save.Encounter.ShouldDecreaseItemEffectCounter;
        row.EncounterRewards = save.Encounter.RewardUniqueIdByIndex ?? [];
        row.EncounterStage = StageOf(save.Encounter);

        switch (save.Encounter)
        {
            case TBGRandomEncounterDB random:
                row.EncounterOptionChoice = random.EncounterOptionChoice;
                break;
            case TBGFacilityEncounterDB facility:
                row.EncounterOptionChoice = facility.EncounterOptionChoice;
                break;
            case TBGTreasureBoxEncounterDB treasure:
                row.EncounterIsRealTreasure = treasure.IsRealTreasure;
                break;
        }
    }

    private async Task<MiniGameTableBoardDBServer> LoadRow(SchaleDataContext db, long accountServerId, long eventContentId)
        => await db.MiniGameTableBoards.FirstOrDefaultAsync(x => x.AccountServerId == accountServerId && x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardSaveNotExist, $"No table board save for event {eventContentId}");

    private ParcelResultDB AccountSnapshot(SchaleDataContext db, AccountDBServer account)
        => new()
        {
            AccountDB = account.ToMap(_mapper),
            AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(_mapper)
        };

    private MinigameTBGSeasonExcelT Season(long eventContentId)
        => _excelService.GetTable<MinigameTBGSeasonExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidSeason, $"MinigameTBGSeasonExcel {eventContentId} not found");

    private static int DiceCount(MinigameTBGSeasonExcelT season)
        => new[] { season.EchelonSlot1CharacterId, season.EchelonSlot2CharacterId, season.EchelonSlot3CharacterId, season.EchelonSlot4CharacterId }.Count(x => x != 0);

    private MinigameTBGThemaExcelT? Thema(long eventContentId, int themaIndex, TBGThemaType themaType)
        => _excelService.GetTable<MinigameTBGThemaExcelT>().FirstOrDefault(x => x.EventContentId == eventContentId && x.ThemaIndex == themaIndex && x.ThemaType == themaType);

    private int NextThemaIndex(long eventContentId, int themaIndex)
    {
        var next = _excelService.GetTable<MinigameTBGThemaExcelT>()
            .Where(x => x.EventContentId == eventContentId && x.ThemaType == TBGThemaType.Normal && x.ThemaIndex > themaIndex)
            .OrderBy(x => x.ThemaIndex)
            .FirstOrDefault();

        return next != null ? next.ThemaIndex : -1;
    }

    private MinigameTBGObjectExcelT? ObjectInfo(long uniqueId)
        => _excelService.GetTable<MinigameTBGObjectExcelT>().FirstOrDefault(x => x.UniqueId == uniqueId);

    private List<MinigameTBGEncounterExcelT> EncounterInfos(long eventContentId, int themaIndex, TBGThemaType mapType, TBGObjectType objectType)
        => _excelService.GetTable<MinigameTBGEncounterExcelT>()
            .Where(x => x.EventContentId == eventContentId && x.ObjectType == objectType && (x.AllThema || (x.ThemaIndex == themaIndex && x.ThemaType == mapType)))
            .ToList();

    private MinigameTBGEncounterExcelT? RealTreasureEncounter(long eventContentId, int themaIndex, TBGThemaType mapType)
        => _excelService.GetTable<MinigameTBGEncounterExcelT>()
            .FirstOrDefault(x => x.EventContentId == eventContentId && x.ObjectType == TBGObjectType.TreasureBox && !x.AllThema && x.ThemaIndex == themaIndex && x.ThemaType == mapType);

    private MinigameTBGEncounterExcelT EncounterInfo(long uniqueId)
        => _excelService.GetTable<MinigameTBGEncounterExcelT>().FirstOrDefault(x => x.UniqueId == uniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardInvalidData, $"MinigameTBGEncounterExcel {uniqueId} not found");

    private Dictionary<int, MinigameTBGEncounterOptionExcelT> Options(long optionGroupId)
        => _excelService.GetTable<MinigameTBGEncounterOptionExcelT>()
            .Where(x => x.OptionGroupId == optionGroupId)
            .GroupBy(x => x.SlotIndex)
            .ToDictionary(g => g.Key, g => g.First());

    private MinigameTBGEncounterOptionExcelT Option(TBGEncounterDB encounter, int slotIndex)
        => Options(EncounterInfo(encounter.EncounterId).OptionGroupId).GetValueOrDefault(slotIndex)
            ?? throw new WebAPIException(WebAPIErrorCode.MiniGameTableBoardProcessEncounterFailed, "invalid encounter option selected");

    private MinigameTBGEncounterRewardExcelT? RewardInfo(long uniqueId)
        => _excelService.GetTable<MinigameTBGEncounterRewardExcelT>().FirstOrDefault(x => x.UniqueId == uniqueId);

    // the treasure and facility groups carry a single row at Prob 0, so a zero total falls back to the first instead of rolling nothing
    private MinigameTBGEncounterRewardExcelT? RollReward(long groupId)
    {
        var rows = _excelService.GetTable<MinigameTBGEncounterRewardExcelT>().Where(x => x.GroupId == groupId).ToList();
        if (rows.Count == 0)
            return null;

        var total = rows.Sum(x => x.Prob);
        if (total <= 0)
            return rows[0];

        var roll = Random.Shared.Next(total);
        foreach (var row in rows)
        {
            roll -= row.Prob;
            if (roll < 0)
                return row;
        }

        return rows[^1];
    }

    private MinigameTBGItemExcelT? ItemInfo(long uniqueId)
        => _excelService.GetTable<MinigameTBGItemExcelT>().FirstOrDefault(x => x.UniqueId == uniqueId);

    private List<MinigameTBGDiceExcelT> DiceInfos(long eventContentId, long diceGroup)
        => _excelService.GetTable<MinigameTBGDiceExcelT>()
            .Where(x => x.EventContentId == eventContentId && x.DiceGroup == diceGroup)
            .OrderBy(x => x.UniqueId)
            .ToList();

    private MiniGameTBGThemaRewardExcelT? ThemaReward(TBGBoardSaveDB save, TBGThemaType mapType, MiniGameTBGThemaRewardType rewardType)
    {
        var thema = Thema(save.EventContentId, save.ThemaIndex, mapType);
        if (thema == null)
            return null;

        var rows = _excelService.GetTable<MiniGameTBGThemaRewardExcelT>()
            .Where(x => x.EventContentId == save.EventContentId && x.ThemaUniqueId == thema.UniqueId && x.MiniGameTBGThemaRewardType == rewardType)
            .ToList();

        return rows.FirstOrDefault(x => !x.IsLoop && x.ThemaRound == save.Round)
            ?? rows.Where(x => x.IsLoop && x.ThemaRound <= save.Round).OrderByDescending(x => x.ThemaRound).FirstOrDefault()
            ?? rows.FirstOrDefault(x => x.IsLoop);
    }

    private int RewardReceiveIndex()
        => _excelService.GetTable<ConstMinigameTBGExcelT>().Select(x => x.EncounterRewardReceiveIndex).FirstOrDefault();
}
