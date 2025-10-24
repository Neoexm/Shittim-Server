using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;

namespace Plana.Database.GameModel
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
        public Nullable<DateTime> LastSummonDate { get; set; }

        [NotMapped]
        public bool IsNew { get; set; }

        public Dictionary<long, CafeCharacterDBServer> CafeVisitCharacterDBs { get; set; } = [];
        public List<FurnitureDBServer> FurnitureDBs { get; set; } = [];
        public DateTime ProductionAppliedTime { get; set; }
        public virtual CafeProductionDBServer? ProductionDB { get; set; }

        public class CafeCharacterDBServer : VisitingCharacterDBServer
        {
            public bool IsSummon { get; set; }
            public DateTime LastInteractTime { get; set; }
        }

        public CafeDBServer() { }

        public CafeDBServer(AccountDBServer account, long cafeId)
        {
            var serverTime = account.GameSettings?.ServerDateTime() ?? DateTime.Now;
            CafeId = cafeId;
            AccountId = account.ServerId;
            CafeRank = 10;
            LastUpdate = serverTime;
            LastSummonDate = serverTime.AddMonths(-1);
            ProductionAppliedTime = serverTime;
            ProductionDB = new()
            {
                AppliedDate = serverTime,
                ComfortValue = 60,
                ProductionParcelInfos = cafeId == 1 ?
                    [
                        new(ParcelType.Currency, CurrencyTypes.ActionPoint, 0),
                        new(ParcelType.Currency, CurrencyTypes.Gold, 0)
                    ] :
                    [new(ParcelType.Currency, CurrencyTypes.Gold, 0)]
            };
        }
    }

    public static class CafeDBServerExtensions
    {
        public static CafeDBServer GetCafeById(this IQueryable<CafeDBServer> cafes, long accountId, long cafeId)
        {
            return cafes.First(x => x.AccountServerId == accountId && x.CafeId == cafeId);
        }

        public static CafeDBServer GetCafeByCafeDBId(this IQueryable<CafeDBServer> cafes, long accountId, long cafeDBId)
        {
            return cafes.First(x => x.AccountServerId == accountId && x.CafeDBId == cafeDBId);
        }

        public static IQueryable<CafeDBServer> GetAccountCafes(this SCHALEContext context, long accountId)
        {
            return context.Cafes.Where(x => x.AccountServerId == accountId);
        }
        
        public static List<CafeDBServer> AddCafes(this SCHALEContext context, long accountId, params CafeDBServer[] cafes)
        {
            if (cafes == null || cafes.Length == 0)
                return new List<CafeDBServer>();

            foreach (var cafe in cafes)
            {
                cafe.AccountServerId = accountId;
                context.Cafes.Add(cafe);
            }

            return cafes.ToList();
        }
    }
}