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

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CampaignConcentrateHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ConcentrateCampaignManager _concentrateCampaignManager;
    private readonly IMapper _mapper;

    public CampaignConcentrateHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConcentrateCampaignManager concentrateCampaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _concentrateCampaignManager = concentrateCampaignManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Campaign_EnterMainStage)]
    public async Task<CampaignEnterMainStageResponse> EnterMainStage(
        SchaleDataContext db,
        CampaignEnterMainStageRequest request,
        CampaignEnterMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.CreateConcentrateCampaign(db, account, request.StageUniqueId);

        response.SaveDataDB = stageSave.ToMap(_mapper);

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

        response.ParcelResultDB = new();
        response.SaveDataDB = stageSave.ToMap(_mapper);

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

        response.ParcelResultDB = new();
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
