using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MailHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;

    public MailHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        IMapper mapper,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Mail_Check)]
    public async Task<MailCheckResponse> Check(
        SchaleDataContext db,
        MailCheckRequest request,
        MailCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var mailCount = db.GetAccountMails(account.ServerId).Count();

        response.Count = mailCount;

        return response;
    }

    [ProtocolHandler(Protocol.Mail_List)]
    public async Task<MailListResponse> List(
        SchaleDataContext db,
        MailListRequest request,
        MailListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var mails = db.GetAccountMails(account.ServerId).ToList();

        response.MailDBs = _mapper.Map<List<MailDB>>(mails);
        response.Count = mails.Count;
        response.ServerNotification = ServerNotificationFlag.None;

        return response;
    }

    [ProtocolHandler(Protocol.Mail_Receive)]
    public async Task<MailReceiveResponse> Receive(
        SchaleDataContext db,
        MailReceiveRequest request,
        MailReceiveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var mailsToReceive = db.GetAccountMails(account.ServerId)
            .Where(m => request.MailServerIds.Contains(m.ServerId))
            .ToList();

        var parcelResults = new List<ParcelResult>();
        foreach (var mail in mailsToReceive)
        {
            if (mail.Type == MailType.System && mail.ParcelInfos != null)
            {
                foreach (var parcel in mail.ParcelInfos)
                {
                    parcelResults.Add(new ParcelResult(parcel.Key.Type, parcel.Key.Id, parcel.Amount));
                }
            }
            db.Mails.Remove(mail);
        }

        await db.SaveChangesAsync();

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcelResults);

        response.MailServerIds = request.MailServerIds;
        response.ParcelResultDB = parcelResolver.ParcelResult;
        response.ServerNotification = ServerNotificationFlag.None;

        return response;
    }
}
