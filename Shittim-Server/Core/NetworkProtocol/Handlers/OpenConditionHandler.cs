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
        response.StaticOpenConditions = Enum.GetValues<OpenConditionContent>()
            .ToDictionary(c => c, _ => OpenConditionLockReason.None);

        return response;
    }
}
