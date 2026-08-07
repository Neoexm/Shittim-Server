using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;
using System.Text.Json;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CampaignConcentrateHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ConcentrateCampaignManager _concentrateCampaignManager;
    private readonly EventContentCampaignManager _eventContentCampaignManager;
    private readonly IMapper _mapper;
    private readonly ILogger<CampaignConcentrateHandler> _logger;

    public CampaignConcentrateHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConcentrateCampaignManager concentrateCampaignManager,
        EventContentCampaignManager eventContentCampaignManager,
        IMapper mapper,
        ILogger<CampaignConcentrateHandler> logger) : base(registry)
    {
        _sessionService = sessionService;
        _concentrateCampaignManager = concentrateCampaignManager;
        _eventContentCampaignManager = eventContentCampaignManager;
        _mapper = mapper;
        _logger = logger;
    }

    [ProtocolHandler(Protocol.Campaign_ConfirmTutorialStage)]
    public async Task<CampaignConfirmTutorialStageResponse> ConfirmTutorialStage(
        SchaleDataContext db,
        CampaignConfirmTutorialStageRequest request,
        CampaignConfirmTutorialStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Get-or-create covers both orders: the tutorial client may or may not have sent a deploy
        // (which persists the row) before confirming.
        _ = await _concentrateCampaignManager.GetConcentrateCampaign(db, account, request.StageUniqueId)
            ?? await _concentrateCampaignManager.CreateConcentrateCampaign(db, account, request.StageUniqueId);

        var stageSave = await _concentrateCampaignManager.StartConcentrateCampaign(db, account, new CampaignConfirmMainStageRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));
        return response;
    }

    [ProtocolHandler(Protocol.Campaign_Portal)]
    public async Task<CampaignPortalResponse> Portal(
        SchaleDataContext db,
        CampaignPortalRequest request,
        CampaignPortalResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _eventContentCampaignManager.Portal(db, account, new EventContentPortalRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonEntityId = request.EchelonEntityId
        });

        response.CampaignMainStageSaveDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));
        return response;
    }

    [ProtocolHandler(Protocol.Campaign_WithdrawEchelon)]
    public async Task<CampaignWithdrawEchelonResponse> WithdrawEchelon(
        SchaleDataContext db,
        CampaignWithdrawEchelonRequest request,
        CampaignWithdrawEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, echelons) = await _eventContentCampaignManager.WithdrawEchelon(db, account, new EventContentWithdrawEchelonRequest
        {
            StageUniqueId = request.StageUniqueId,
            WithdrawEchelonEntityId = request.WithdrawEchelonEntityId
        });

        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));
        response.WithdrawEchelonDBs = echelons;
        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EnterMainStage)]
    public async Task<CampaignEnterMainStageResponse> EnterMainStage(
        SchaleDataContext db,
        CampaignEnterMainStageRequest request,
        CampaignEnterMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        _logger.LogInformation("[SHITTIM] Creating campaign for stage {StageId}", request.StageUniqueId);
        var stageSave = await _concentrateCampaignManager.CreateConcentrateCampaign(db, account, request.StageUniqueId);

        _logger.LogDebug("[SHITTIM] StageSave created - EntityId: {EntityId}, EnemyCount: {EnemyCount}, StrategyCount: {StrategyCount}",
            stageSave.LastEnemyEntityId, stageSave.EnemyInfos?.Count ?? 0, stageSave.StrategyObjects?.Count ?? 0);

        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));

        // Guarded: the indented serialization of a whole stage save is not worth paying for on every Campaign_EnterMainStage when nobody is reading Debug.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[SHITTIM] Response SaveDataDB JSON:\n{Json}",
                JsonSerializer.Serialize(response.SaveDataDB, new JsonSerializerOptions { WriteIndented = true }));
        }

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_DeployEchelon)]
    public async Task<CampaignDeployEchelonResponse> DeployEchelon(
        SchaleDataContext db,
        CampaignDeployEchelonRequest request,
        CampaignDeployEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.DeployEchelon(db, account, request);

        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_ConfirmMainStage)]
    public async Task<CampaignConfirmMainStageResponse> ConfirmMainStage(
        SchaleDataContext db,
        CampaignConfirmMainStageRequest request,
        CampaignConfirmMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.StartConcentrateCampaign(db, account, request);

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
        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));
        response.StageInfo = await _concentrateCampaignManager.GetStageInfo(stageSave.StageUniqueId);

        // Same as EnterMainStage: body dump only when Debug is actually on.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[SHITTIM] ConfirmMainStage - StageInfo:\n{Json}",
                JsonSerializer.Serialize(response.StageInfo, new JsonSerializerOptions { WriteIndented = true }));
        }
        _logger.LogDebug("[SHITTIM] ConfirmMainStage - StrategySkipGroundId: {StrategySkipGroundId}", response.StageInfo?.StrategySkipGroundId ?? 0);

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_MapMove)]
    public async Task<CampaignMapMoveResponse> MapMove(
        SchaleDataContext db,
        CampaignMapMoveRequest request,
        CampaignMapMoveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, preMove) = await _concentrateCampaignManager.MoveTarget(db, account, request);

        // Official echoes the mover back on every MapMove reply.
        response.EchelonEntityId = request.EchelonEntityId;

        // Rewind before shaping: the response reports the mover as it stood at the start of this step, and the DisplayInfos entry is what walks it to the destination.
        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(
            ConcentrateCampaignManager.RewindMovedEchelonForWire(
                stageSave.ToMap(_mapper), request.EchelonEntityId, preMove));

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EnterTactic)]
    public async Task<CampaignEnterTacticResponse> EnterTactic(
        SchaleDataContext db,
        CampaignEnterTacticRequest request,
        CampaignEnterTacticResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Official replies with nothing but the header here; the work is remembering which enemy was engaged so Campaign_TacticResult can clear it off the map.
        await _concentrateCampaignManager.EnterTactic(db, account, request);

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_TacticResult)]
    public async Task<CampaignTacticResultResponse> TacticResult(
        SchaleDataContext db,
        CampaignTacticResultRequest request,
        CampaignTacticResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, historyDb, tacticRank, clearReward, endBattleType, parcelResult, missionProgresses) =
            await _concentrateCampaignManager.TacticResult(db, account, request);

        // Built by the manager: a won tactic pays its characters exp whether or not it cleared the stage, so there is something to report on every response.
        // Official never sends an empty collection inside a ParcelResultDB, and OmitWhenEmpty drops the ones the resolver did not touch.
        response.ParcelResultDB = parcelResult;

        // Killing the map's designated boss ends the mission, and the EndBattle entry attached here is the only thing that tells the client so. Attached after shaping, since ShapeForWire nulls the empty DisplayInfos the manager leaves on the save row and the reward payload is wire-only.
        response.SaveDataDB = ConcentrateCampaignManager.AttachStageClearForWire(
            ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper)), clearReward, endBattleType);
        response.CampaignStageHistoryDB = historyDb;

        // On the battle-skip path the client has no local battle to read the outcome from, so it takes the win/lose flag straight from TacticRank (> 0 means the player won).
        // Left out, the field sits at 0 and every skipped victory reads as a wipe.
        response.TacticRank = tacticRank;

        // Official always carries these, empty when there is nothing to award; the client Syncs them unconditionally after a tactic. On a clear they repeat what the EndBattle entry carries - official's top-level copies are byte-identical to the nested ones.
        response.LevelUpCharacterDBs = new();
        response.FirstClearReward = clearReward?.FirstClearReward ?? new();
        response.ThreeStarReward = clearReward?.ThreeStarReward ?? new();
        response.StrategyObjectRewards = clearReward?.StrategyObjectRewards ?? new();

        // Official carries MissionProgressDBs on every tactic result; without it the client's mission screen only learns about campaign progress at the next login, when Mission_List re-reads it.
        if (missionProgresses.Count > 0)
            response.MissionProgressDBs = missionProgresses;

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EndTurn)]
    public async Task<CampaignEndTurnResponse> EndTurn(
        SchaleDataContext db,
        CampaignEndTurnRequest request,
        CampaignEndTurnResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.EndTurn(db, account, request);

        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));

        return response;
    }
}
