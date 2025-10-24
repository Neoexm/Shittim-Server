using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.NetworkProtocol;

namespace Plana.Database.GameModel
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
            this.CurrencyDict = new();
            this.UpdateTimeDict = new();
            foreach (var type in Enum.GetValues(typeof(CurrencyTypes)).Cast<CurrencyTypes>())
            {
                if (type == CurrencyTypes.Invalid) continue;
                this.CurrencyDict.Add(type, 0);
                this.UpdateTimeDict.Add(type, DateTime.Now);
            }
        }

        public AccountCurrencyDBServer(long accountId) : this()
        {
            this.AccountServerId = accountId;
            this.AccountLevel = 1;
            this.AcademyLocationRankSum = 1;
        }

        public void UpdateAccountLevel(long accountLevel) => this.AccountLevel = accountLevel;

        public void UpdateAcademyLocationRankSum(List<AcademyLocationDBServer> academyLocations)
        {
            if (academyLocations == null || academyLocations.Count == 0) return;
            var rankSum = academyLocations.Sum(x => x.Rank);
            this.AcademyLocationRankSum = rankSum == 0 ? 1 : rankSum;
        }

        public void UpdateCurrency(CurrencyTypes type, long amount, DateTime updateTime)
        {
            if (!CurrencyDict.ContainsKey(type))
            {
                if (type == CurrencyTypes.Invalid) return;
                CurrencyDict[type] = 0;
            }

            if (!UpdateTimeDict.ContainsKey(type))
            {
                if (type == CurrencyTypes.Invalid) return;
                UpdateTimeDict[type] = updateTime;
            }

            this.CurrencyDict[type] += amount;
            this.UpdateTimeDict[type] = updateTime;
        }
        
        public void UpdateGem(DateTime updateTime)
        {
            this.CurrencyDict[CurrencyTypes.Gem] = this.CurrencyDict[CurrencyTypes.GemBonus] + this.CurrencyDict[CurrencyTypes.GemPaid];
            this.UpdateTimeDict[CurrencyTypes.Gem] = updateTime;
        }

        public void SubtractGem(long amount, DateTime updateTime)
        {
            if (amount < 0) amount = Math.Abs(amount);

            if (amount > this.CurrencyDict[CurrencyTypes.Gem])
                throw new WebAPIException(WebAPIErrorCode.AccountCurrencyCannotAffordCost);

            if (this.CurrencyDict[CurrencyTypes.GemBonus] >= amount)
                this.CurrencyDict[CurrencyTypes.GemBonus] -= amount;
            else
            {
                long remainingAmount = amount - this.CurrencyDict[CurrencyTypes.GemBonus];
                this.CurrencyDict[CurrencyTypes.GemBonus] = 0;
                this.CurrencyDict[CurrencyTypes.GemPaid] -= remainingAmount;
            }
            this.CurrencyDict[CurrencyTypes.Gem] = this.CurrencyDict[CurrencyTypes.GemBonus] + this.CurrencyDict[CurrencyTypes.GemPaid];

            this.UpdateTimeDict[CurrencyTypes.GemBonus] = updateTime;
            this.UpdateTimeDict[CurrencyTypes.GemPaid] = updateTime;
            this.UpdateTimeDict[CurrencyTypes.Gem] = updateTime;
        }
    }
    
    public static class AccountCurrencyDBServerExtensions
    {
        public static IQueryable<AccountCurrencyDBServer> GetAccountCurrencies(this SCHALEContext context, long accountId)
        {
            return context.Currencies.Where(x => x.AccountServerId == accountId);
        }
    }
}