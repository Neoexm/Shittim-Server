using AutoMapper;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.Data.ModelMapping;
using Serilog;

namespace Shittim_Server.Services;

public class MailManager
{
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;
    private readonly SchaleDataContext _context;

    public MailManager(ParcelHandler parcelHandler, IMapper mapper, SchaleDataContext context)
    {
        _parcelHandler = parcelHandler;
        _mapper = mapper;
        _context = context;
    }

    public async Task SendSystemMail(
        AccountDBServer account,
        string sender,
        string comment,
        List<ParcelInfo> parcelInfos,
        DateTime? expireDate = null)
    {
        var mail = new MailDBServer
        {
            AccountServerId = account.ServerId,
            Type = MailType.System,
            Sender = sender,
            Comment = comment,
            SendDate = DateTime.Now,
            ExpireDate = expireDate ?? DateTime.Now.AddDays(7),
            ParcelInfos = parcelInfos,
            RemainParcelInfos = new List<ParcelInfo>()
        };

        _context.Mails.Add(mail);
        await _context.SaveChangesAsync();

        Log.Information($"System mail sent to account {account.ServerId} from {sender}");
    }

    public async Task SendSystemMailWithParcels(
        AccountDBServer account,
        string sender,
        string comment,
        ParcelType parcelType,
        long parcelId,
        long parcelAmount,
        DateTime? expireDate = null)
    {
        var parcelInfos = new List<ParcelInfo>
        {
            new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = parcelType, Id = parcelId },
                Amount = parcelAmount
            }
        };

        await SendSystemMail(account, sender, comment, parcelInfos, expireDate);
    }

    public async Task SendSystemMailMultipleParcels(
        AccountDBServer account,
        string sender,
        string comment,
        List<(ParcelType type, long id, long amount)> parcels,
        DateTime? expireDate = null)
    {
        var parcelInfos = parcels.Select(p => new ParcelInfo
        {
            Key = new ParcelKeyPair { Type = p.type, Id = p.id },
            Amount = p.amount
        }).ToList();

        await SendSystemMail(account, sender, comment, parcelInfos, expireDate);
    }

    public async Task<ParcelResultDB> ReceiveMail(
        AccountDBServer account,
        List<long> mailServerIds)
    {
        var parcelResultDb = new ParcelResultDB();
        List<ParcelResult> parcelResults = new();

        foreach (var mailId in mailServerIds)
        {
            var targetMail = _context.Mails.FirstOrDefault(y => y.ServerId == mailId);
            if (targetMail == null) continue;

            targetMail.ReceiptDate = DateTime.Now;
            
            parcelResults.AddRange(ParcelResult.ConvertParcelResult(targetMail.ParcelInfos));
        }

        var parcelResolver = await _parcelHandler.BuildParcel(_context, account, parcelResults, parcelResultDb);
        await _context.SaveChangesAsync();

        parcelResultDb.AccountDB = account.ToMap(_mapper);
        parcelResultDb.AccountCurrencyDB = _context.Currencies.FirstMapTo(x => x.AccountServerId == account.ServerId, _mapper);
        
        return parcelResultDb;
    }

    public List<MailDBServer> GetAccountMails(AccountDBServer account, bool onlyUnread = false)
    {
        var mailsQuery = _context.GetAccountMails(account.ServerId);
        
        if (onlyUnread)
            mailsQuery = mailsQuery.Where(m => m.ReceiptDate == null);

        return mailsQuery.OrderByDescending(m => m.SendDate).ToList();
    }

    public long GetUnreadMailCount(AccountDBServer account)
    {
        return _context.GetAccountMails(account.ServerId).Count(y => y.ReceiptDate == null);
    }
}
