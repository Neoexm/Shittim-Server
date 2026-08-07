using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class StickerHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    public StickerHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
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

        // the client checks the acquire conditions itself and reports which stickers it earned on the lobby screen
        var book = db.GetAccountStickerBooks(account.ServerId).First();
        var owned = db.Stickers.Where(x => x.AccountServerId == account.ServerId).Select(x => x.StickerUniqueId).ToHashSet();

        var received = new List<StickerDBServer>();
        foreach (var stickerId in request.AcquireStickerUniqueIds ?? [])
        {
            if (!owned.Add(stickerId)) continue;
            var sticker = new StickerDBServer { AccountServerId = account.ServerId, StickerUniqueId = stickerId };
            db.Stickers.Add(sticker);
            received.Add(sticker);
        }

        if (received.Count > 0)
        {
            book.UnusedStickerDBs = [.. book.UnusedStickerDBs ?? [], .. received];
            db.StickerBooks.Update(book);
            await db.SaveChangesAsync();
        }

        response.ReceivedStickerDBs = received.ToMapList(_mapper);
        response.StickerBookDB = book.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Sticker_UseSticker)]
    public async Task<StickerUseStickerResponse> UseSticker(
        SchaleDataContext db,
        StickerUseStickerRequest request,
        StickerUseStickerResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var book = db.GetAccountStickerBooks(account.ServerId).First();
        var unused = book.UnusedStickerDBs ?? [];
        var sticker = unused.FirstOrDefault(x => x.StickerUniqueId == request.StickerUniqueId);
        if (sticker != null)
        {
            book.UnusedStickerDBs = unused.Where(x => x.StickerUniqueId != request.StickerUniqueId).ToList();
            book.UsedStickerDBs = [.. book.UsedStickerDBs ?? [], sticker];
            db.StickerBooks.Update(book);

            // placing the last sticker of a page pays the group's page bonus
            var pageContents = _excelService.GetTable<StickerPageContentExcelT>();
            var placed = pageContents.FirstOrDefault(x => x.Id == request.StickerUniqueId);
            if (placed != null)
            {
                var usedIds = book.UsedStickerDBs.Select(x => x.StickerUniqueId).ToHashSet();
                var pageDone = pageContents.Where(x => x.StickerPageId == placed.StickerPageId).All(x => usedIds.Contains(x.Id));
                var group = _excelService.GetTable<StickerGroupExcelT>().FirstOrDefault(x => x.Id == placed.StickerGroupId);
                if (pageDone && group != null && group.PageCompleteRewardParcelType != ParcelType.None)
                {
                    var resolver = await _parcelHandler.BuildParcel(db, account,
                        new ParcelResult(group.PageCompleteRewardParcelType, group.PageCompleteRewardParcelId, group.PageCompleteRewardAmount));
                    response.ParcelResultDB = resolver.ParcelResult;
                }
            }

            await db.SaveChangesAsync();
        }

        response.StickerBookDB = book.ToMap(_mapper);

        return response;
    }
}
