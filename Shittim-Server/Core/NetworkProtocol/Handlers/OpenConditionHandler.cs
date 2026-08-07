using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.Services;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class OpenConditionHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public OpenConditionHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.OpenCondition_List)]
    public async Task<OpenConditionListResponse> List(
        SchaleDataContext db,
        OpenConditionListRequest request,
        OpenConditionListResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // same content set the auth/sync StaticOpenConditions dictionary covers
        response.ConditionContents = [.. AccountHandler.BuildOfficialStaticOpenConditions().Keys];

        return response;
    }

    [ProtocolHandler(Protocol.OpenCondition_Set)]
    public async Task<OpenConditionSetResponse> Set(
        SchaleDataContext db,
        OpenConditionSetRequest request,
        OpenConditionSetResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // the client evaluates its own open conditions; nothing server-side to update
        response.ConditionDBs = request.ConditionDB != null ? [request.ConditionDB] : [];

        return response;
    }

    [ProtocolHandler(Protocol.OpenCondition_EventList)]
    public async Task<OpenConditionEventListResponse> EventList(
        SchaleDataContext db,
        OpenConditionEventListRequest request,
        OpenConditionEventListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ConquestTiles = new();
        response.WorldRaidLocalBossDBs = db.GetAccountWorldRaidLocalBosses(account.ServerId).ToMapList(_mapper)
            .GroupBy(x => x.GroupId).ToDictionary(x => x.Key, x => x.ToList());
        // Official OpenCondition_EventList responses carry no StaticOpenConditions
        // (that dict only appears on Account_Auth / Account_LoginSync).

        return response;
    }
}
