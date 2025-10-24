using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;

namespace BlueArchiveAPI.Services
{
    public interface ICurrencyService
    {
        Task ChangeCurrencyAsync(BAContext context, long accountId, CurrencyTypes type, long delta, string reason, DateTime serverTime);
        Dictionary<CurrencyTypes, DateTime> GetUpdateTimeDict(BAContext context, long accountId, DateTime accountCreatedAt);
    }

    public class CurrencyService : ICurrencyService
    {
        public async Task ChangeCurrencyAsync(BAContext context, long accountId, CurrencyTypes type, long delta, string reason, DateTime serverTime)
        {
            // Get current currency
            var currency = await context.AccountCurrencies.FirstOrDefaultAsync(c => c.AccountServerId == accountId);
            if (currency == null)
                throw new InvalidOperationException($"Currency not found for account {accountId}");

            // Parse current dict
            var currencyDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<CurrencyTypes, long>>(currency.CurrencyDict) 
                ?? new Dictionary<CurrencyTypes, long>();
            
            var oldBalance = currencyDict.GetValueOrDefault(type, 0);
            var newBalance = oldBalance + delta;
            currencyDict[type] = newBalance;

            // Update currency
            currency.CurrencyDict = System.Text.Json.JsonSerializer.Serialize(currencyDict);

            // Log transaction
            context.CurrencyTransactions.Add(new Models.CurrencyTransaction
            {
                AccountServerId = accountId,
                CurrencyType = type,
                TransactionTime = serverTime,
                AmountChange = delta,
                NewBalance = newBalance,
                Reason = reason
            });

            await context.SaveChangesAsync();
        }

        public Dictionary<CurrencyTypes, DateTime> GetUpdateTimeDict(BAContext context, long accountId, DateTime accountCreatedAt)
        {
            var latest = context.CurrencyTransactions
                .Where(t => t.AccountServerId == accountId)
                .GroupBy(t => t.CurrencyType)
                .Select(g => new { Type = g.Key, Time = g.Max(t => t.TransactionTime) })
                .ToDictionary(x => x.Type, x => x.Time);

            // For all currency types, use latest transaction time or account creation
            var result = new Dictionary<CurrencyTypes, DateTime>();
            foreach (CurrencyTypes type in Enum.GetValues(typeof(CurrencyTypes)))
            {
                result[type] = latest.TryGetValue(type, out var time) ? time : accountCreatedAt;
            }
            
            return result;
        }
    }

    public static class CurrencyServiceExtensions
    {
        public static void AddCurrencyService(this IServiceCollection services)
        {
            services.AddScoped<ICurrencyService, CurrencyService>();
        }
    }
}