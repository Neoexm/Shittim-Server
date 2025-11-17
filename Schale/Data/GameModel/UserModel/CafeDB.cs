using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class CafeDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long CafeDBId { get; set; }
        
        public long CafeId { get; set; }
        public long AccountId { get => AccountServerId; set => AccountServerId = value; }
        public int CafeRank { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime? LastSummonDate { get; set; }

        [NotMapped]
        public bool IsNew { get; set; }

        public Dictionary<long, CafeCharacterDBServer> CafeVisitCharacterDBs { get; set; } = [];
        public List<FurnitureDBServer> FurnitureDBs { get; set; } = [];
        public virtual CafeProductionDBServer? ProductionDB { get; set; }
        public DateTime ProductionAppliedTime { get; set; }

        public class CafeCharacterDBServer : VisitingCharacterDBServer
        {
            public bool IsSummon { get; set; }
            public DateTime LastInteractTime { get; set; }
        }

        public CafeDBServer() { }

        public CafeDBServer(AccountDBServer account, long cafeId)
        {
            var currentTime = account.GameSettings?.ServerDateTime() ?? DateTime.Now;
            
            CafeId = cafeId;
            AccountId = account.ServerId;
            CafeRank = 10;
            LastUpdate = currentTime;
            LastSummonDate = currentTime.AddMonths(-1);
            ProductionAppliedTime = currentTime;
            
            ProductionDB = CreateProductionDB(cafeId, currentTime);
        }

        private CafeProductionDBServer CreateProductionDB(long cafeId, DateTime appliedDate)
        {
            var productionDB = new CafeProductionDBServer
            {
                AppliedDate = appliedDate,
                ComfortValue = 60
            };

            if (cafeId == 1)
            {
                productionDB.ProductionParcelInfos = 
                [
                    new(ParcelType.Currency, CurrencyTypes.ActionPoint, 0),
                    new(ParcelType.Currency, CurrencyTypes.Gold, 0)
                ];
            }
            else
            {
                productionDB.ProductionParcelInfos = [new(ParcelType.Currency, CurrencyTypes.Gold, 0)];
            }

            return productionDB;
        }
    }

    public static class CafeDBServerExtensions
    {
        public static IQueryable<CafeDBServer> GetAccountCafes(this SchaleDataContext context, long accountId)
        {
            return context.Cafes.Where(x => x.AccountServerId == accountId);
        }

        public static CafeDBServer GetCafeById(this IQueryable<CafeDBServer> cafes, long accountId, long cafeId)
        {
            return cafes.First(x => x.AccountServerId == accountId && x.CafeId == cafeId);
        }

        public static CafeDBServer GetCafeByCafeDBId(this IQueryable<CafeDBServer> cafes, long accountId, long cafeDBId)
        {
            return cafes.First(x => x.AccountServerId == accountId && x.CafeDBId == cafeDBId);
        }

        public static List<CafeDBServer> AddCafes(this SchaleDataContext context, long accountId, params CafeDBServer[] cafes)
        {
            if (cafes == null || cafes.Length == 0)
                return [];

            foreach (var cafe in cafes)
            {
                cafe.AccountServerId = accountId;
                context.Cafes.Add(cafe);
            }

            return [.. cafes];
        }
    }
}


