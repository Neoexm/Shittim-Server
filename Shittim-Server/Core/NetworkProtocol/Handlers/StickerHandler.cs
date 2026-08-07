using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class StickerHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public StickerHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Sticker_Login)]
    public async Task<StickerLoginResponse> Login(
        SchaleDataContext db,
        StickerLoginRequest request,
        StickerLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.StickerBookDB = db.GetAccountStickerBooks(account.ServerId).FirstMapTo(_mapper);
        return response;
    }

}
