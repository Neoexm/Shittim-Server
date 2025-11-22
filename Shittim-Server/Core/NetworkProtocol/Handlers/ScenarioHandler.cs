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
using Shittim_Server.Managers;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ScenarioHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ScenarioManager _scenarioManager;

    public ScenarioHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper,
        ScenarioManager scenarioManager) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _scenarioManager = scenarioManager;
    }

    [ProtocolHandler(Protocol.Scenario_List)]
    public async Task<ScenarioListResponse> List(
        SchaleDataContext db,
        ScenarioListRequest request,
        ScenarioListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ScenarioCollectionDBs = [];
        response.ScenarioGroupHistoryDBs = db.GetAccountScenarioGroupHistories(account.ServerId).ToMapList(_mapper);
        response.ScenarioHistoryDBs = db.GetAccountScenarioHistories(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_Skip)]
    public async Task<ScenarioSkipResponse> Skip(
        SchaleDataContext db,
        ScenarioSkipRequest request,
        ScenarioSkipResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_Select)]
    public async Task<ScenarioSelectResponse> Select(
        SchaleDataContext db,
        ScenarioSelectRequest request,
        ScenarioSelectResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_GroupHistoryUpdate)]
    public async Task<ScenarioGroupHistoryUpdateResponse> GroupHistoryUpdate(
        SchaleDataContext db,
        ScenarioGroupHistoryUpdateRequest request,
        ScenarioGroupHistoryUpdateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var scenarioGroup = await _scenarioManager.ScenarioGroupHistoryUpdate(db, account, request);

        response.ScenarioGroupHistoryDB = scenarioGroup.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_Clear)]
    public async Task<ScenarioClearResponse> Clear(
        SchaleDataContext db,
        ScenarioClearRequest request,
        ScenarioClearResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (scenario, parcelResultDB) = await _scenarioManager.ScenarioClear(db, account, request);

        response.ScenarioHistoryDB = scenario.ToMap(_mapper);
        if (parcelResultDB != null)
            response.ParcelResultDB = parcelResultDB;

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_LobbyStudentChange)]
    public async Task<ScenarioLobbyStudentChangeResponse> LobbyStudentChange(
        SchaleDataContext db,
        ScenarioLobbyStudentChangeRequest request,
        ScenarioLobbyStudentChangeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_AccountStudentChange)]
    public async Task<ScenarioAccountStudentChangeResponse> AccountStudentChange(
        SchaleDataContext db,
        ScenarioAccountStudentChangeRequest request,
        ScenarioAccountStudentChangeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_SpecialLobbyChange)]
    public async Task<ScenarioSpecialLobbyChangeResponse> SpecialLobbyChange(
        SchaleDataContext db,
        ScenarioSpecialLobbyChangeRequest request,
        ScenarioSpecialLobbyChangeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
