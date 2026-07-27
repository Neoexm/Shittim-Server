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
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;

    public MailHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
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

        // Unclaimed, unexpired only: official's CommonMailCount matches Mail_List's Count and both
        // fall to 0 (key omitted) after Mail_Receive.
        response.CommonMailCount = db
            .GetAccountMailbox(account.ServerId, account.GameSettings.ServerDateTime())
            .Count();

        // Report-and-consume: official's first Mail_Check after an in-session delivery carries
        // NewMailArrived on top of the gateway's mailbox baseline (12 = 8|4) and the second is
        // back to 8 while the mail still sits unread.
        if (MailNotificationService.Consume(account.ServerId))
            response.ServerNotification |= ServerNotificationFlag.NewMailArrived;

        return response;
    }

    [ProtocolHandler(Protocol.Mail_List)]
    public async Task<MailListResponse> List(
        SchaleDataContext db,
        MailListRequest request,
        MailListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var mails = db.GetAccountMailbox(account.ServerId, account.GameSettings.ServerDateTime()).ToList();

        response.MailDBs = _mapper.Map<List<MailDB>>(mails);
        response.Count = mails.Count;
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Mail_Receive)]
    public async Task<MailReceiveResponse> Receive(
        SchaleDataContext db,
        MailReceiveRequest request,
        MailReceiveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var mailsToReceive = db.GetAccountMailbox(account.ServerId, account.GameSettings.ServerDateTime())
            .Where(m => request.MailServerIds.Contains(m.ServerId))
            .ToList();

        var parcelResults = new List<ParcelResult>();
        foreach (var mail in mailsToReceive)
        {
            // Every mail type delivers its attachments, not just MailType.System (0). Official
            // granted the rewards on ClanAttendance (11) and ExpiryChangeItem (10) mail in the
            // captures, and this server's own seeder writes NewUserBonus (13) — all of which the
            // old `Type == System` gate dropped on the floor while still deleting the mail, so the
            // attachments vanished and ParcelResultDB came back with nothing but AccountCurrencyDB.
            if (mail.ParcelInfos != null)
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
        // Official Mail_Receive always carries BattlePassInfoDBs (as [] when none).
        response.BattlePassInfoDBs = [];

        return response;
    }

    // Semi-permanent mailbox (second mail tab: monthly product / battle pass recurring rewards).
    // This server never seeds semi-permanent mail, so the box is always empty — but the client
    // queries it right after clearing the normal box, and an unhandled protocol there throws the
    // user back to the title screen with "server failed to process request".
    [ProtocolHandler(Protocol.Mail_ListSemiPermanent)]
    public async Task<MailListSemiPermanentResponse> ListSemiPermanent(
        SchaleDataContext db,
        MailListSemiPermanentRequest request,
        MailListSemiPermanentResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.MailDBs = new List<MailDB>();
        response.Count = 0;

        return response;
    }

    // Defensive: the semi-permanent box is empty, so the client won't normally reach this. If a
    // semi-permanent mail ever exists, receive the single requested mail like a regular one.
    [ProtocolHandler(Protocol.Mail_ReceiveSemiPermanent)]
    public async Task<MailReceiveSemiPermanentResponse> ReceiveSemiPermanent(
        SchaleDataContext db,
        MailReceiveSemiPermanentRequest request,
        MailReceiveSemiPermanentResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var mail = db.GetAccountMails(account.ServerId)
            .FirstOrDefault(m => m.ServerId == request.MailDBId);

        var parcelResults = new List<ParcelResult>();
        if (mail != null)
        {
            // Same as Receive: deliver the attachments whatever the mail type says.
            if (mail.ParcelInfos != null)
            {
                foreach (var parcel in mail.ParcelInfos)
                    parcelResults.Add(new ParcelResult(parcel.Key.Type, parcel.Key.Id, parcel.Amount));
            }
            db.Mails.Remove(mail);
            await db.SaveChangesAsync();
        }

        var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcelResults);

        response.MailDBId = request.MailDBId;
        response.ParcelResultDB = parcelResolver.ParcelResult;

        return response;
    }
}
