using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ContentSaveHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ConcentrateCampaignManager _concentrateCampaignManager;
    private readonly IMapper _mapper;

    public ContentSaveHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConcentrateCampaignManager concentrateCampaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _concentrateCampaignManager = concentrateCampaignManager;
        _mapper = mapper;
    }

    /// <summary>
    /// "Am I in the middle of something?" - asked at login, and again by the pause menu's Retry.
    ///
    /// Retry on a campaign battle never sends Campaign_RestartMainStage; UIPause sends this and branches on one field.
    /// ContentSaveGetNetworkTask.HandleMessage reads "HasValidData", treating an absent key as false, and UIPause.HandleContentSaveGet either restarts the battle or puts up LocalizeData.GetText("CampaignStageInvalidSaveData") - "invalid mission info" - and drops the player back to the lobby.
    /// Left at its default false the key is dropped from the wire by DefaultValueHandling.Ignore, so answering with the open run is what makes Retry work at all.
    /// </summary>
    [ProtocolHandler(Protocol.ContentSave_Get)]
    public async Task<ContentSaveGetResponse> Get(
        SchaleDataContext db,
        ContentSaveGetRequest request,
        ContentSaveGetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var save = await _concentrateCampaignManager.GetOpenConcentrateCampaign(db, account);

        // No open run is the ordinary case - official's login-time answer is a bare header, which is exactly what leaving HasValidData false produces.
        if (save == null)
            return response;

        response.HasValidData = true;
        response.ContentSaveDB = ConcentrateCampaignManager.ShapeForWire(save.ToMap(_mapper));

        return response;
    }

    /// <summary>
    /// The client giving up on a run it was offered. Closing the save keeps the next ContentSave_Get from offering it again - without this the abandoned mission would follow the player into every subsequent login.
    /// </summary>
    [ProtocolHandler(Protocol.ContentSave_Discard)]
    public async Task<ContentSaveDiscardResponse> Discard(
        SchaleDataContext db,
        ContentSaveDiscardRequest request,
        ContentSaveDiscardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        await _concentrateCampaignManager.CloseConcentrateCampaigns(db, account, request.StageUniqueId);

        return response;
    }
}
