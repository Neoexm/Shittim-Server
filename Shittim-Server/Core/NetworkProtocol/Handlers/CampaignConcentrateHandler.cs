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
    private readonly IMapper _mapper;
    private readonly ILogger<CampaignConcentrateHandler> _logger;

    public CampaignConcentrateHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConcentrateCampaignManager concentrateCampaignManager,
        IMapper mapper,
        ILogger<CampaignConcentrateHandler> logger) : base(registry)
    {
        _sessionService = sessionService;
        _concentrateCampaignManager = concentrateCampaignManager;
        _mapper = mapper;
        _logger = logger;
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

    [ProtocolHandler(Protocol.Campaign_WithdrawEchelon)]
    public async Task<CampaignWithdrawEchelonResponse> WithdrawEchelon(
        SchaleDataContext db,
        CampaignWithdrawEchelonRequest request,
        CampaignWithdrawEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.GetConcentrateCampaign(db, account, request.StageUniqueId);
        if (stageSave == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {request.StageUniqueId}");

        response.WithdrawEchelonDBs = new();

        // the save keeps which students left with each slot; the client syncs it, nothing server-side reads it back - a redeploy through Campaign_DeployEchelon starts fresh
        foreach (var entityId in request.WithdrawEchelonEntityId ?? new List<long>())
        {
            if (stageSave.EchelonInfos == null || !stageSave.EchelonInfos.TryGetValue(entityId, out var unit))
                continue;

            stageSave.WithdrawInfos ??= new Dictionary<long, List<long>>();
            stageSave.WithdrawInfos[entityId] = unit.HpInfos?.Keys.ToList() ?? new List<long>();
            stageSave.EchelonInfos.Remove(entityId);

            var echelon = await EchelonService.GetConcentratedCampaignEchelon(db, account.ServerId, entityId);
            if (echelon != null)
                response.WithdrawEchelonDBs.Add(echelon.ToMap(_mapper));
        }

        db.CampaignMainStageSaves.Update(stageSave);
        await db.SaveChangesAsync();

        response.SaveDataDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_Heal)]
    public async Task<CampaignHealResponse> Heal(
        SchaleDataContext db,
        CampaignHealRequest request,
        CampaignHealResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.GetConcentrateCampaign(db, account, request.CampaignStageUniqueId);
        if (stageSave == null)
            throw new InvalidOperationException($"Campaign stage save not found for stage {request.CampaignStageUniqueId}");

        // heal tiles restore one student at a time: a downed one comes off the dying ledger, everyone else goes back to the full 10000 rate
        if (stageSave.EchelonInfos != null && stageSave.EchelonInfos.TryGetValue(request.EchelonIndex, out var echelon))
        {
            echelon.DyingInfos?.Remove(request.CharacterServerId);
            echelon.HpInfos ??= new Dictionary<long, long>();
            echelon.HpInfos[request.CharacterServerId] = 10000;
        }

        db.CampaignMainStageSaves.Update(stageSave);
        await db.SaveChangesAsync();

        response.AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new();
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

        var stageSave = await _concentrateCampaignManager.UsePortal(db, account, request);

        // the one campaign response whose save rides under the type name rather than SaveDataDB
        response.CampaignMainStageSaveDB = ConcentrateCampaignManager.ShapeForWire(stageSave.ToMap(_mapper));

        return response;
    }
}
