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

    [ProtocolHandler(Protocol.Sticker_Lobby)]
    public async Task<StickerLobbyResponse> Lobby(
        SchaleDataContext db,
        StickerLobbyRequest request,
        StickerLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var book = db.GetAccountStickerBooks(account.ServerId).First();
        book.UnusedStickerDBs ??= [];
        book.UsedStickerDBs ??= [];

        var received = new List<StickerDBServer>();
        foreach (var id in request.AcquireStickerUniqueIds ?? [])
        {
            var owned = book.UnusedStickerDBs.Any(x => x.StickerUniqueId == id)
                || book.UsedStickerDBs.Any(x => x.StickerUniqueId == id);
            if (owned)
                continue;

            var sticker = new StickerDBServer
            {
                AccountServerId = account.ServerId,
                StickerUniqueId = id
            };
            db.Stickers.Add(sticker);
            book.UnusedStickerDBs.Add(sticker);
            received.Add(sticker);
        }

        db.StickerBooks.Update(book);
        await db.SaveChangesAsync();

        response.ReceivedStickerDBs = received.ToMapList(_mapper);
        response.StickerBookDB = book.ToMap(_mapper);
        return response;
    }

}
