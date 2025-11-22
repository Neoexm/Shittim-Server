using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class BattlePassHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public BattlePassHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.BattlePass_Check)]
    public async Task<BattlePassCheckResponse> Check(
        SchaleDataContext db,
        BattlePassCheckRequest request,
        BattlePassCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.BattlePass_GetInfo)]
    public async Task<BattlePassGetInfoResponse> GetInfo(
        SchaleDataContext db,
        BattlePassGetInfoRequest request,
        BattlePassGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.BattlePass_MissionList)]
    public async Task<BattlePassMissionListResponse> MissionList(
        SchaleDataContext db,
        BattlePassMissionListRequest request,
        BattlePassMissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.MissionHistoryUniqueIds = [];

        return response;
    }
}
