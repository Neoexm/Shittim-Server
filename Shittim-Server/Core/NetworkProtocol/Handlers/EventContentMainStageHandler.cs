using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

// Event main stages play on the same hex maps campaign stages do - the client's own ContentSaveDBFactory.CreateEventMain is CampaignService.CreateStrategySave copy-constructed into an EventContentMainStageSaveDB - so deploy, confirm, move, engage and end-turn are handed straight to ConcentrateCampaignManager against a Campaign*Request built here. What the event adds on top is the buff/debuff popup and its reward tags, and those live in EventContentCampaignManager.
public class EventContentMainStageHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly EventContentCampaignManager _eventContentCampaignManager;
    private readonly ConcentrateCampaignManager _concentrateCampaignManager;
    private readonly IMapper _mapper;

    public EventContentMainStageHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        EventContentCampaignManager eventContentCampaignManager,
        ConcentrateCampaignManager concentrateCampaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _eventContentCampaignManager = eventContentCampaignManager;
        _concentrateCampaignManager = concentrateCampaignManager;
        _mapper = mapper;
    }

    private EventContentMainStageSaveDB Wire(CampaignMainStageSaveDBServer save)
        => (EventContentMainStageSaveDB)ConcentrateCampaignManager.ShapeForWire(_mapper.Map<EventContentMainStageSaveDB>(save));

    [ProtocolHandler(Protocol.EventContent_EnterMainStage)]
    public async Task<EventContentEnterMainStageResponse> EnterMainStage(
        SchaleDataContext db,
        EventContentEnterMainStageRequest request,
        EventContentEnterMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _eventContentCampaignManager.CreateEventMainStage(db, account, request.StageUniqueId);

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_DeployEchelon)]
    public async Task<EventContentDeployEchelonResponse> DeployEchelon(
        SchaleDataContext db,
        EventContentDeployEchelonRequest request,
        EventContentDeployEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.DeployEchelon(db, account, new CampaignDeployEchelonRequest
        {
            StageUniqueId = request.StageUniqueId,
            DeployedEchelons = request.DeployedEchelons
        });

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_ConfirmMainStage)]
    public async Task<EventContentConfirmMainStageResponse> ConfirmMainStage(
        SchaleDataContext db,
        EventContentConfirmMainStageRequest request,
        EventContentConfirmMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.StartConcentrateCampaign(db, account, new CampaignConfirmMainStageRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        response.ParcelResultDB = new()
        {
            AccountDB = account.ToMap(_mapper),
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new(),
            AcademyLocationDBs = new(),
            CharacterDBs = new(),
            CostumeDBs = new(),
            DisplaySequence = new(),
            EmblemDBs = new(),
            EquipmentDBs = new(),
            FurnitureDBs = new(),
            GachaResultCharacters = new(),
            ItemDBs = new(),
            IdCardBackgroundDBs = new(),
            MemoryLobbyDBs = new(),
            ParcelForMission = new(),
            ParcelResultStepInfoList = new(),
            RemovedItemIds = new(),
            RemovedEquipmentIds = new(),
            RemovedFurnitureIds = new(),
            StickerDBs = new(),
            SecretStoneCharacterIdAndCounts = new(),
            TSSCharacterDBs = new(),
            WeaponDBs = new(),
            CharacterNewUniqueIds = new(),
            BaseAccountExp = 0,
            AdditionalAccountExp = 0,
            NewbieBoostAccountExp = 0
        };
        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_RestartMainStage)]
    public async Task<EventContentRestartMainStageResponse> RestartMainStage(
        SchaleDataContext db,
        EventContentRestartMainStageRequest request,
        EventContentRestartMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Creating the run closes whatever was open, so a restart is just a fresh save: it comes back BeforeStart and the entrance fee is charged again at the ConfirmMainStage that follows.
        var stageSave = await _eventContentCampaignManager.CreateEventMainStage(db, account, request.StageUniqueId);

        response.ParcelResultDB = new()
        {
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new()
        };
        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_MapMove)]
    public async Task<EventContentMapMoveResponse> MapMove(
        SchaleDataContext db,
        EventContentMapMoveRequest request,
        EventContentMapMoveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, preMove) = await _concentrateCampaignManager.MoveTarget(db, account, new CampaignMapMoveRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonEntityId = request.EchelonEntityId,
            DestPosition = request.DestPosition
        });

        // Official echoes the mover back on every MapMove reply.
        response.EchelonEntityId = request.EchelonEntityId;

        // Rewind before shaping: the response reports the mover as it stood at the start of this step, and the DisplayInfos entry is what walks it to the destination.
        response.SaveDataDB = (EventContentMainStageSaveDB)ConcentrateCampaignManager.ShapeForWire(
            ConcentrateCampaignManager.RewindMovedEchelonForWire(
                _mapper.Map<EventContentMainStageSaveDB>(stageSave), request.EchelonEntityId, preMove));

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_Portal)]
    public async Task<EventContentPortalResponse> Portal(
        SchaleDataContext db,
        EventContentPortalRequest request,
        EventContentPortalResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _eventContentCampaignManager.Portal(db, account, request);

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_WithdrawEchelon)]
    public async Task<EventContentWithdrawEchelonResponse> WithdrawEchelon(
        SchaleDataContext db,
        EventContentWithdrawEchelonRequest request,
        EventContentWithdrawEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, echelons) = await _eventContentCampaignManager.WithdrawEchelon(db, account, request);

        response.SaveDataDB = Wire(stageSave);
        response.WithdrawEchelonDBs = echelons;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_EnterTactic)]
    public async Task<EventContentEnterTacticResponse> EnterTactic(
        SchaleDataContext db,
        EventContentEnterTacticRequest request,
        EventContentEnterTacticResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Official replies with nothing but the header here; the work is remembering which enemy was engaged so Campaign_TacticResult can clear it off the map.
        await _concentrateCampaignManager.EnterTactic(db, account, new CampaignEnterTacticRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonIndex = request.EchelonIndex,
            EnemyIndex = request.EnemyIndex
        });

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_TacticResult)]
    public async Task<EventContentTacticResultResponse> TacticResult(
        SchaleDataContext db,
        EventContentTacticResultRequest request,
        EventContentTacticResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, historyDb, tacticRank, clearReward, endBattleType, parcelResult, bonusReward, collections) =
            await _eventContentCampaignManager.EventTacticResult(db, account, request);

        response.ParcelResultDB = parcelResult;

        // Killing the map's designated boss ends the mission, and the EndBattle entry attached here is the only thing that tells the client so. Attached after shaping, since ShapeForWire nulls the empty DisplayInfos the manager leaves on the save row and the reward payload is wire-only.
        response.SaveDataDB = (EventContentMainStageSaveDB)ConcentrateCampaignManager.AttachStageClearForWire(
            Wire(stageSave), clearReward, endBattleType);
        response.CampaignStageHistoryDB = historyDb;

        // On the battle-skip path the client has no local battle to read the outcome from, so it takes the win/lose flag straight from TacticRank (> 0 means the player won).
        // Left out, the field sits at 0 and every skipped victory reads as a wipe.
        response.TacticRank = tacticRank;

        // Official always carries these, empty when there is nothing to award; the client Syncs them unconditionally after a tactic. On a clear they repeat what the EndBattle entry carries - official's top-level copies are byte-identical to the nested ones.
        response.LevelUpCharacterDBs = new();
        response.FirstClearReward = clearReward?.FirstClearReward ?? new();
        response.StrategyObjectRewards = clearReward?.StrategyObjectRewards ?? new();
        response.BonusReward = bonusReward;
        response.EventContentCollectionDBs = collections;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_SubStageResult)]
    public async Task<EventContentSubStageResultResponse> SubStageResult(
        SchaleDataContext db,
        EventContentSubStageResultRequest request,
        EventContentSubStageResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResult, tacticRank, firstClearReward, collections) =
            await _eventContentCampaignManager.EventSubStageResult(db, account, request);

        response.TacticRank = tacticRank;
        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);
        response.LevelUpCharacterDBs = new();
        response.ParcelResultDB = parcelResult;
        response.FirstClearReward = firstClearReward;

        // Sub stages are ordinary battles with no strategy map, so there is no buff group to have taken a debuff from and nothing to pay a bonus on.
        response.BonusReward = new();
        response.EventContentCollectionDBs = collections;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_SelectBuff)]
    public async Task<EventContentSelectBuffResponse> SelectBuff(
        SchaleDataContext db,
        EventContentSelectBuffRequest request,
        EventContentSelectBuffResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _eventContentCampaignManager.SelectBuff(db, account, request.SelectedBuffId);

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_EndTurn)]
    public async Task<EventContentEndTurnResponse> EndTurn(
        SchaleDataContext db,
        EventContentEndTurnRequest request,
        EventContentEndTurnResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.EndTurn(db, account, new CampaignEndTurnRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_Retreat)]
    public async Task<EventContentRetreatResponse> Retreat(
        SchaleDataContext db,
        EventContentRetreatRequest request,
        EventContentRetreatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (parcelResultDb, released) = await _eventContentCampaignManager.EventRetreat(db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResultDb;
        response.ReleasedEchelonNumbers = released;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_PurchasePlayCountHardStage)]
    public async Task<EventContentPurchasePlayCountHardStageResponse> PurchasePlayCountHardStage(
        SchaleDataContext db,
        EventContentPurchasePlayCountHardStageRequest request,
        EventContentPurchasePlayCountHardStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (currency, historyDb) = await _eventContentCampaignManager.PurchasePlayCountHardStage(db, account, request.StageUniqueId);

        response.AccountCurrencyDB = currency.ToMap(_mapper);
        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);

        return response;
    }
}
