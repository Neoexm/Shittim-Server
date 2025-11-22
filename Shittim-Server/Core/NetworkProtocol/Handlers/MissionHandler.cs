using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MissionHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public MissionHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Mission_Sync)]
    public async Task<MissionSyncResponse> Sync(
        SchaleDataContext db,
        MissionSyncRequest request,
        MissionSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionProgresses = db.GetAccountMissionProgresses(account.ServerId).ToList();

        response.MissionProgressDBs = _mapper.Map<List<MissionProgressDB>>(missionProgresses);

        return response;
    }

    [ProtocolHandler(Protocol.Mission_List)]
    public async Task<MissionListResponse> List(
        SchaleDataContext db,
        MissionListRequest request,
        MissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var campaignHistories = db.GetAccountCampaignStageHistories(account.ServerId)
            .Select(h => h.StoryUniqueId)
            .ToList();

        var missions = db.GetAccountMissionProgresses(account.ServerId);

        var missionProgresses = request.EventContentId == null
            ? missions.ToList()
            : missions.Where(x => x.MissionUniqueId.ToString().StartsWith(request.EventContentId.ToString())).ToList();

        response.MissionHistoryUniqueIds = campaignHistories;
        response.ProgressDBs = _mapper.Map<List<MissionProgressDB>>(missionProgresses);

        return response;
    }

    [ProtocolHandler(Protocol.Mission_GuideMissionSeasonList)]
    public async Task<GuideMissionSeasonListResponse> GuideMissionSeasonList(
        SchaleDataContext db,
        GuideMissionSeasonListRequest request,
        GuideMissionSeasonListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
