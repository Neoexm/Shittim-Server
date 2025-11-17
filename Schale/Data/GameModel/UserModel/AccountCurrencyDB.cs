using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.NetworkProtocol;

namespace Schale.Data.GameModel
{
    public class AccountCurrencyDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public long AccountLevel { get; set; }
        public long AcademyLocationRankSum { get; set; }
        public Dictionary<CurrencyTypes, long> CurrencyDict { get; set; }
        public Dictionary<CurrencyTypes, DateTime> UpdateTimeDict { get; set; }

        public AccountCurrencyDBServer()
        {
            CurrencyDict = new();
            UpdateTimeDict = new();
            InitializeCurrencies();
        }

        public AccountCurrencyDBServer(long accountId) : this()
        {
            AccountServerId = accountId;
            AccountLevel = 1;
            AcademyLocationRankSum = 1;
        }

        private void InitializeCurrencies()
        {
            var currencyTypes = Enum.GetValues<CurrencyTypes>();
            var now = DateTime.Now;
            
            foreach (var currencyType in currencyTypes)
            {
                if (currencyType == CurrencyTypes.Invalid) continue;
                
                CurrencyDict[currencyType] = 0;
                UpdateTimeDict[currencyType] = now;
            }
        }

        public void UpdateAccountLevel(long level)
        {
            AccountLevel = level;
        }

        public void UpdateAcademyLocationRankSum(List<AcademyLocationDBServer> locations)
        {
            if (locations == null || locations.Count == 0) return;
            
            var totalRank = locations.Sum(loc => loc.Rank);
            AcademyLocationRankSum = totalRank > 0 ? totalRank : 1;
        }

        public void UpdateCurrency(CurrencyTypes currencyType, long amount, DateTime timestamp)
        {
            if (currencyType == CurrencyTypes.Invalid) return;

            EnsureCurrencyExists(currencyType, timestamp);
            
            CurrencyDict[currencyType] += amount;
            UpdateTimeDict[currencyType] = timestamp;
        }

        private void EnsureCurrencyExists(CurrencyTypes currencyType, DateTime timestamp)
        {
            if (!CurrencyDict.ContainsKey(currencyType))
                CurrencyDict[currencyType] = 0;
            
            if (!UpdateTimeDict.ContainsKey(currencyType))
                UpdateTimeDict[currencyType] = timestamp;
        }

        public void UpdateGem(DateTime timestamp)
        {
            var bonusGems = GetCurrencyAmount(CurrencyTypes.GemBonus);
            var paidGems = GetCurrencyAmount(CurrencyTypes.GemPaid);
            
            CurrencyDict[CurrencyTypes.Gem] = bonusGems + paidGems;
            UpdateTimeDict[CurrencyTypes.Gem] = timestamp;
        }

        public void SubtractGem(long amount, DateTime timestamp)
        {
            var absAmount = Math.Abs(amount);
            var totalGems = GetCurrencyAmount(CurrencyTypes.Gem);

            if (absAmount > totalGems)
                throw new WebAPIException(WebAPIErrorCode.AccountCurrencyCannotAffordCost);

            DeductGemFromSources(absAmount, timestamp);
            UpdateGem(timestamp);
        }

        private void DeductGemFromSources(long amount, DateTime timestamp)
        {
            var bonusGems = GetCurrencyAmount(CurrencyTypes.GemBonus);
            
            if (bonusGems >= amount)
            {
                CurrencyDict[CurrencyTypes.GemBonus] -= amount;
            }
            else
            {
                var remainingToDeduct = amount - bonusGems;
                CurrencyDict[CurrencyTypes.GemBonus] = 0;
                CurrencyDict[CurrencyTypes.GemPaid] -= remainingToDeduct;
            }

            UpdateTimeDict[CurrencyTypes.GemBonus] = timestamp;
            UpdateTimeDict[CurrencyTypes.GemPaid] = timestamp;
        }

        private long GetCurrencyAmount(CurrencyTypes currencyType)
        {
            return CurrencyDict.TryGetValue(currencyType, out var value) ? value : 0;
        }
    }

    public static class AccountCurrencyDBServerExtensions
    {
        public static IQueryable<AccountCurrencyDBServer> GetAccountCurrencies(this SchaleDataContext context, long accountId)
        {
            return context.Currencies.Where(x => x.AccountServerId == accountId);
        }
    }
}


