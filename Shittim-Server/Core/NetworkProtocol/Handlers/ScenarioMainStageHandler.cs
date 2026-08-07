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

}
