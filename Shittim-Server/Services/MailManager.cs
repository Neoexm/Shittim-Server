using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
    private readonly IDbContextFactory<SchaleDataContext> _dbFactory;

    public MailManager(ParcelHandler parcelHandler, IMapper mapper, IDbContextFactory<SchaleDataContext> dbFactory)
    {
        _parcelHandler = parcelHandler;
        _mapper = mapper;
        _dbFactory = dbFactory;
    }

    public async Task SendSystemMail(
        AccountDBServer account,
        string sender,
        string comment,
        List<ParcelInfo> parcelInfos,
        DateTime? expireDate = null)
    {
        await using var context = await _dbFactory.CreateDbContextAsync();
        
        var mail = new MailDBServer
        {
            AccountServerId = account.ServerId,
            Type = MailType.System,
            Sender = sender,
            Comment = comment,
            SendDate = account.GameSettings.ServerDateTime(),
            ExpireDate = expireDate ?? account.GameSettings.ServerDateTime().AddDays(7),
            ParcelInfos = parcelInfos,
            RemainParcelInfos = new List<ParcelInfo>()
        };

        context.Mails.Add(mail);
        await context.SaveChangesAsync();

        // Mid-session delivery: the account's next Mail_Check reports NewMailArrived once.
        MailNotificationService.MarkNewMail(account.ServerId);

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
        await using var context = await _dbFactory.CreateDbContextAsync();
        
        var parcelResultDb = new ParcelResultDB();
        List<ParcelResult> parcelResults = new();

        foreach (var mailId in mailServerIds)
        {
            var targetMail = context.Mails.FirstOrDefault(y => y.ServerId == mailId);
            if (targetMail == null) continue;

            targetMail.ReceiptDate = account.GameSettings.ServerDateTime();
            
            parcelResults.AddRange(ParcelResult.ConvertParcelResult(targetMail.ParcelInfos));
        }

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelResults, parcelResultDb);
        await context.SaveChangesAsync();

        // AccountDB is left to the resolver, which adds it only when the claim changed the account
        // row - official's mail claims carry AccountCurrencyDB but no AccountDB.
        parcelResultDb.AccountCurrencyDB = context.Currencies.FirstMapTo(x => x.AccountServerId == account.ServerId, _mapper);
        
        return parcelResultDb;
    }

    public async Task<List<MailDBServer>> GetAccountMails(AccountDBServer account, bool onlyUnread = false)
    {
        await using var context = await _dbFactory.CreateDbContextAsync();
        
        var mailsQuery = context.GetAccountMails(account.ServerId);
        
        if (onlyUnread)
            mailsQuery = mailsQuery.Where(m => m.ReceiptDate == null);

        return mailsQuery.OrderByDescending(m => m.SendDate).ToList();
    }

    public async Task<long> GetUnreadMailCount(AccountDBServer account)
    {
        await using var context = await _dbFactory.CreateDbContextAsync();
        return context.GetAccountMails(account.ServerId).Count(y => y.ReceiptDate == null);
    }
}
