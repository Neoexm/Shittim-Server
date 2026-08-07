using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MemoryLobbyHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public MemoryLobbyHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.MemoryLobby_List)]
    public async Task<MemoryLobbyListResponse> List(
        SchaleDataContext db,
        MemoryLobbyListRequest request,
        MemoryLobbyListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.MemoryLobbyDBs = db.GetAccountMemoryLobbies(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.MemoryLobby_SetMain)]
    public async Task<MemoryLobbySetMainResponse> SetMain(
        SchaleDataContext db,
        MemoryLobbySetMainRequest request,
        MemoryLobbySetMainResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.MemoryLobbyUniqueId = request.MemoryLobbyId;
        await db.SaveChangesAsync();

        response.AccountDB = _mapper.Map<AccountDB>(account);
        return response;
    }

    [ProtocolHandler(Protocol.MemoryLobby_UpdateLobbyMode)]
    public async Task<MemoryLobbyUpdateLobbyModeResponse> UpdateLobbyMode(
        SchaleDataContext db,
        MemoryLobbyUpdateLobbyModeRequest request,
        MemoryLobbyUpdateLobbyModeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.LobbyMode = request.IsMemoryLobbyMode ? 1 : 0;
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.MemoryLobby_Interact)]
    public async Task<MemoryLobbyInteractResponse> Interact(
        SchaleDataContext db,
        MemoryLobbyInteractRequest request,
        MemoryLobbyInteractResponse response)
    {
        // the tap counter lives client-side; the request only exists so the server can see the account is active
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
