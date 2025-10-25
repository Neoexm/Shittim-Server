using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Models;
using BlueArchiveAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace BlueArchiveAPI.Handlers
{
    public static class Mail
    {
        public class Check : BaseHandler<MailCheckRequest, MailCheckResponse>
        {
            private readonly BAContext _context;

            public Check(BAContext context)
            {
                _context = context;
            }

            protected override async Task<MailCheckResponse> Handle(MailCheckRequest request)
            {
                var accountId = request.SessionKey.AccountServerId;
                var count = await _context.Mails
                    .CountAsync(m => m.AccountServerId == accountId && m.ReceiptDate == null);

                return new MailCheckResponse
                {
                    Count = count
                };
            }
        }

        public class List : BaseHandler<MailListRequest, MailListResponse>
        {
            private readonly BAContext _context;

            public List(BAContext context)
            {
                _context = context;
            }

            protected override async Task<MailListResponse> Handle(MailListRequest request)
            {
                var accountId = request.SessionKey.AccountServerId;

                var mailsQuery = _context.Mails
                    .Where(m => m.AccountServerId == accountId);

                if (request.IsReadMail)
                    mailsQuery = mailsQuery.Where(m => m.ReceiptDate != null);
                else
                    mailsQuery = mailsQuery.Where(m => m.ReceiptDate == null);

                var mails = await mailsQuery
                    .OrderByDescending(m => m.SendDate)
                    .Skip((int)request.PivotIndex)
                    .Take(50)
                    .ToListAsync();

                var unreadCount = await _context.Mails
                    .CountAsync(m => m.AccountServerId == accountId && m.ReceiptDate == null);

                var mailDBs = mails.Select(m => new MailDB
                {
                    ServerId = m.ServerId,
                    AccountServerId = m.AccountServerId,
                    Type = (MailType)m.Type,
                    UniqueId = m.UniqueId,
                    Sender = m.Sender,
                    LocalizedSender = null,
                    Comment = m.Comment,
                    LocalizedComment = null,
                    SendDate = m.SendDate,
                    ReceiptDate = m.ReceiptDate,
                    ExpireDate = m.ExpireDate,
                    ParcelInfos = m.ParcelInfos,
                    RemainParcelInfos = m.RemainParcelInfos
                }).ToList();

                return new MailListResponse
                {
                    MailDBs = mailDBs,
                    Count = unreadCount
                };
            }
        }
    }

    public static class Clan
    {
        public class Check : BaseHandler<ClanCheckRequest, ClanCheckResponse>
        {
            protected override async Task<ClanCheckResponse> Handle(ClanCheckRequest request)
            {
                return new ClanCheckResponse();
            }
        }
    }

    public static class Friend
    {
        public class Check : BaseHandler<FriendCheckRequest, FriendCheckResponse>
        {
            protected override async Task<FriendCheckResponse> Handle(FriendCheckRequest request)
            {
                return new FriendCheckResponse();
            }
        }
    }
}
