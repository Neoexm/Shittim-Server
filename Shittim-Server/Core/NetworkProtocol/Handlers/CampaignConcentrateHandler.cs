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

        _logger.LogInformation("[SHITTIM] StageSave created - EntityId: {EntityId}, EnemyCount: {EnemyCount}, StrategyCount: {StrategyCount}",
            stageSave.LastEnemyEntityId, stageSave.EnemyInfos?.Count ?? 0, stageSave.StrategyObjects?.Count ?? 0);

        response.SaveDataDB = stageSave.ToMap(_mapper);

        var jsonResponse = JsonSerializer.Serialize(response.SaveDataDB, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("[SHITTIM] Response SaveDataDB JSON:\n{Json}", jsonResponse);

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

        response.SaveDataDB = stageSave.ToMap(_mapper);

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
        response.SaveDataDB = stageSave.ToMap(_mapper);
        response.StageInfo = await _concentrateCampaignManager.GetStageInfo(stageSave.StageUniqueId);

        var stageInfoJson = JsonSerializer.Serialize(response.StageInfo, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("[SHITTIM] ConfirmMainStage - StageInfo:\n{Json}", stageInfoJson);
        _logger.LogInformation("[SHITTIM] ConfirmMainStage - StrategySkipGroundId: {StrategySkipGroundId}", response.StageInfo?.StrategySkipGroundId ?? 0);

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_MapMove)]
    public async Task<CampaignMapMoveResponse> MapMove(
        SchaleDataContext db,
        CampaignMapMoveRequest request,
        CampaignMapMoveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.MoveTarget(db, account, request);

        response.SaveDataDB = stageSave.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EnterTactic)]
    public async Task<CampaignEnterTacticResponse> EnterTactic(
        SchaleDataContext db,
        CampaignEnterTacticRequest request,
        CampaignEnterTacticResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_TacticResult)]
    public async Task<CampaignTacticResultResponse> TacticResult(
        SchaleDataContext db,
        CampaignTacticResultRequest request,
        CampaignTacticResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, historyDb) = await _concentrateCampaignManager.TacticResult(db, account, request);

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
        response.SaveDataDB = stageSave.ToMap(_mapper);
        response.CampaignStageHistoryDB = historyDb;

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

        response.SaveDataDB = stageSave.ToMap(_mapper);

        return response;
    }
}
