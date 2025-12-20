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

public class AttachmentHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public AttachmentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Attachment_EmblemList)]
    public async Task<AttachmentEmblemListResponse> EmblemList(
        SchaleDataContext db,
        AttachmentEmblemListRequest request,
        AttachmentEmblemListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        response.EmblemDBs = db.GetAccountEmblems(account.ServerId).ToMapList(_mapper);
        return response;
    }

    [ProtocolHandler(Protocol.Attachment_EmblemAcquire)]
    public async Task<AttachmentEmblemAcquireResponse> EmblemAcquire(
        SchaleDataContext db,
        AttachmentEmblemAcquireRequest request,
        AttachmentEmblemAcquireResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var emblemData = request.UniqueIds.Select(x => new EmblemDBServer
        {
            UniqueId = x,
            ReceiveDate = DateTime.UtcNow.Date
        }).ToList();

        db.AddEmblems(account.ServerId, [.. emblemData]);
        await db.SaveChangesAsync();

        response.EmblemDBs = emblemData.ToMapList(_mapper);
        return response;
    }

    [ProtocolHandler(Protocol.Attachment_EmblemAttach)]
    public async Task<AttachmentEmblemAttachResponse> EmblemAttach(
        SchaleDataContext db,
        AttachmentEmblemAttachRequest request,
        AttachmentEmblemAttachResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var accountAttachment = db.GetAccountAttachments(account.ServerId).FirstOrDefault();

        if (accountAttachment != null)
        {
            accountAttachment.EmblemUniqueId = request.UniqueId;
            await db.SaveChangesAsync();
            response.AttachmentDB = accountAttachment.ToMap(_mapper);
        }

        return response;
    }
}
