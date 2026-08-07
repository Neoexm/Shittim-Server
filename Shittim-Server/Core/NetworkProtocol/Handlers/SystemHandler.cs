using AutoMapper;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class SystemHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public SystemHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.System_Version)]
    public Task<SystemVersionResponse> Version(
        SchaleDataContext db,
        SystemVersionRequest request,
        SystemVersionResponse response)
    {
        // pre-login probe, no session yet. Same triple Account_Auth answers with.
        response.CurrentVersion = Config.Instance.ServerConfiguration.AuthCurrentVersion;
        response.MinimumVersion = 0;
        response.IsDevelopment = false;
        return Task.FromResult(response);
    }

    // dev console leftover in the retail client; the cheat string itself is ignored, the client only reads the snapshot back.
    [ProtocolHandler(Protocol.Common_Cheat)]
    public async Task<CommonCheatResponse> Cheat(
        SchaleDataContext db,
        CommonCheatRequest request,
        CommonCheatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.Account = _mapper.Map<AccountDB>(account);
        response.AccountCurrency = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);
        return response;
    }

    [ProtocolHandler(Protocol.Session_Info)]
    public async Task<SessionInfoResponse> SessionInfo(
        SchaleDataContext db,
        SessionInfoRequest request,
        SessionInfoResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }

    // protocol -1, the gateway's error envelope. It has no request class to route by, so nothing can ever reach this; it exists so the enum member is covered like the rest.
    [ProtocolHandler(Protocol.Error)]
    public Task<ErrorPacket> Error(
        SchaleDataContext db,
        RequestPacket request,
        ErrorPacket response)
    {
        return Task.FromResult(response);
    }

    // protocol 0, the enum's zero value; nothing in the client sends it, but a packet explicitly stamped with it routes here and answering costs nothing.
    [ProtocolHandler(Protocol.None)]
    public async Task<NoneResponse> None(
        SchaleDataContext db,
        NoneRequest request,
        NoneResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
