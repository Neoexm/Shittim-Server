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

        // Unclaimed, unexpired only: official's CommonMailCount matches Mail_List's Count and both fall to 0 (key omitted) after Mail_Receive.
        response.CommonMailCount = db
            .GetAccountMailbox(account.ServerId, account.GameSettings.ServerDateTime())
            .Count();

        // Report-and-consume: official's first Mail_Check after an in-session delivery carries NewMailArrived on top of the gateway's mailbox baseline (12 = 8|4) and the second is back to 8 while the mail still sits unread.
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

        // Count is the whole unreceived mailbox, not the returned window: official's second page (PivotTime = last row's SendDate) returned 1 row but kept Count at 3.
        response.MailDBs = _mapper.Map<List<MailDB>>(ApplyListWindow(mails, request.PivotTime, request.IsDescending));
        response.Count = mails.Count;
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    // Official Mail_List is a SendDate-ordered window: newest first for IsDescending (the only direction the client has been seen to send), and PivotTime bounds the page inclusively - the follow-up call passes the last row's SendDate and official returns that row again (capture 2026-07-28: page 1 = 22:03:12/22:03:00/22:02:53, page 2 with pivot 22:02:53 = the 22:02:53 row).
    internal static List<MailDBServer> ApplyListWindow(
        List<MailDBServer> mails, DateTime pivotTime, bool isDescending)
    {
        // The first-page sentinel (9999-12-31, or an unset pivot) bounds nothing in either direction - only a real timestamp from a previous page's last row narrows the window.
        var unbounded = pivotTime == default || pivotTime.Year >= 9999;

        // The pivot is a SendDate the client echoes back from a previous page, and the wire truncates to whole seconds while rows are stored with sub-second precision - compare at second granularity or the pivot row excludes itself.
        var pivot = TruncateToSecond(pivotTime);

        var window = unbounded
            ? mails
            : mails.Where(m => isDescending
                ? TruncateToSecond(m.SendDate) <= pivot
                : TruncateToSecond(m.SendDate) >= pivot);

        return isDescending
            ? window.OrderByDescending(m => m.SendDate).ThenByDescending(m => m.ServerId).ToList()
            : window.OrderBy(m => m.SendDate).ThenBy(m => m.ServerId).ToList();
    }

    private static DateTime TruncateToSecond(DateTime value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));

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
            // Every mail type delivers its attachments, not just MailType.System (0). Official granted the rewards on ClanAttendance (11) and ExpiryChangeItem (10) mail in the captures, and this server's own seeder writes NewUserBonus (13); a `Type == System` gate drops all of those on the floor while still deleting the mail, so the attachments vanish and ParcelResultDB comes back with nothing but AccountCurrencyDB.
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

    // Semi-permanent mailbox (second mail tab: monthly product / battle pass recurring rewards). This server never seeds semi-permanent mail, so the box is always empty - but the client queries it right after clearing the normal box, and an unhandled protocol there throws the user back to the title screen with "server failed to process request".
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

    // The semi-permanent box is empty, so the client won't normally reach this; if a semi-permanent mail ever exists, receive the single requested mail like a regular one.
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
