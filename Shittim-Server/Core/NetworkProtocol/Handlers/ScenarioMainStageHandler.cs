using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.Data.GameModel;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

// StoryStrategyStageSaveDB is an empty subclass of CampaignMainStageSaveDB and these stages play on the
// same hex maps, so every operation is handed to ConcentrateCampaignManager against a Campaign*Request.
public class ScenarioMainStageHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ConcentrateCampaignManager _concentrateCampaignManager;
    private readonly EventContentCampaignManager _eventContentCampaignManager;
    private readonly IMapper _mapper;

    public ScenarioMainStageHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConcentrateCampaignManager concentrateCampaignManager,
        EventContentCampaignManager eventContentCampaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _concentrateCampaignManager = concentrateCampaignManager;
        _eventContentCampaignManager = eventContentCampaignManager;
        _mapper = mapper;
    }

    private StoryStrategyStageSaveDB Wire(CampaignMainStageSaveDBServer save)
        => (StoryStrategyStageSaveDB)ConcentrateCampaignManager.ShapeForWire(_mapper.Map<StoryStrategyStageSaveDB>(save));

    [ProtocolHandler(Protocol.Scenario_EnterMainStage)]
    public async Task<ScenarioEnterMainStageResponse> EnterMainStage(
        SchaleDataContext db,
        ScenarioEnterMainStageRequest request,
        ScenarioEnterMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.CreateStoryStrategyStage(db, account, request.StageUniqueId);

        response.SaveDataDB = Wire(stageSave);
        return response;
    }

    [ProtocolHandler(Protocol.Scenario_ConfirmMainStage)]
    public async Task<ScenarioConfirmMainStageResponse> ConfirmMainStage(
        SchaleDataContext db,
        ScenarioConfirmMainStageRequest request,
        ScenarioConfirmMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.StartConcentrateCampaign(db, account, new CampaignConfirmMainStageRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        response.ParcelResultDB = new()
        {
            AccountDB = account.ToMap(_mapper),
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new()
        };
        response.SaveDataDB = Wire(stageSave);
        return response;
    }

    [ProtocolHandler(Protocol.Scenario_DeployEchelon)]
    public async Task<ScenarioDeployEchelonResponse> DeployEchelon(
        SchaleDataContext db,
        ScenarioDeployEchelonRequest request,
        ScenarioDeployEchelonResponse response)
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

    [ProtocolHandler(Protocol.Scenario_WithdrawEchelon)]
    public async Task<ScenarioWithdrawEchelonResponse> WithdrawEchelon(
        SchaleDataContext db,
        ScenarioWithdrawEchelonRequest request,
        ScenarioWithdrawEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, echelons) = await _eventContentCampaignManager.WithdrawEchelon(db, account, new EventContentWithdrawEchelonRequest
        {
            StageUniqueId = request.StageUniqueId,
            WithdrawEchelonEntityId = request.WithdrawEchelonEntityId
        });

        response.SaveDataDB = Wire(stageSave);
        response.WithdrawEchelonDBs = echelons;
        return response;
    }

    [ProtocolHandler(Protocol.Scenario_MapMove)]
    public async Task<ScenarioMapMoveResponse> MapMove(
        SchaleDataContext db,
        ScenarioMapMoveRequest request,
        ScenarioMapMoveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, preMove) = await _concentrateCampaignManager.MoveTarget(db, account, new CampaignMapMoveRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonEntityId = request.EchelonEntityId,
            DestPosition = request.DestPosition
        });

        response.EchelonEntityId = request.EchelonEntityId;
        // Rewind before shaping: the response reports the mover where it stood at the start of this step,
        // and the DisplayInfos entry is what walks it to the destination.
        response.SaveDataDB = (StoryStrategyStageSaveDB)ConcentrateCampaignManager.ShapeForWire(
            ConcentrateCampaignManager.RewindMovedEchelonForWire(
                _mapper.Map<StoryStrategyStageSaveDB>(stageSave), request.EchelonEntityId, preMove));

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_EndTurn)]
    public async Task<ScenarioEndTurnResponse> EndTurn(
        SchaleDataContext db,
        ScenarioEndTurnRequest request,
        ScenarioEndTurnResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.EndTurn(db, account, new CampaignEndTurnRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        response.SaveDataDB = Wire(stageSave);
        return response;
    }

    [ProtocolHandler(Protocol.Scenario_EnterTactic)]
    public async Task<ScenarioEnterTacticResponse> EnterTactic(
        SchaleDataContext db,
        ScenarioEnterTacticRequest request,
        ScenarioEnterTacticResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        await _concentrateCampaignManager.EnterTactic(db, account, new CampaignEnterTacticRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonIndex = request.EchelonIndex,
            EnemyIndex = request.EnemyIndex
        });

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_TacticResult)]
    public async Task<ScenarioTacticResultResponse> TacticResult(
        SchaleDataContext db,
        ScenarioTacticResultRequest request,
        ScenarioTacticResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, isPlayerWin) = await _concentrateCampaignManager.ScenarioTacticResult(db, account, new CampaignTacticResultRequest
        {
            PassCheckCharacter = request.PassCheckCharacter,
            Summary = request.Summary,
            Hand = request.Hand,
            SkipSummary = request.SkipSummary
        });

        response.SaveDataDB = Wire(stageSave);
        response.IsPlayerWin = isPlayerWin;
        return response;
    }

    [ProtocolHandler(Protocol.Scenario_Retreat)]
    public async Task<ScenarioRetreatResponse> Retreat(
        SchaleDataContext db,
        ScenarioRetreatRequest request,
        ScenarioRetreatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var save = await _concentrateCampaignManager.GetConcentrateCampaign(db, account, request.StageUniqueId);
        response.ReleasedEchelonNumbers = save?.EchelonInfos?.Keys.ToList() ?? [];

        await _concentrateCampaignManager.CloseConcentrateCampaigns(db, account, request.StageUniqueId);

        // Story stages cost nothing to enter, so there is nothing to refund.
        response.ParcelResultDB = new()
        {
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new()
        };
        return response;
    }

}
