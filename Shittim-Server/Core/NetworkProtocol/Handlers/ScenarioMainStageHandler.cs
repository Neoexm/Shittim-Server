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

}
