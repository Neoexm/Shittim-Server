using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class CafeProductionDBServer
    {
        public long CafeDBId { get; set; }
        public long ComfortValue { get; set; }
        public DateTime AppliedDate { get; set; }
        public List<CafeProductionParcelInfoServer>? ProductionParcelInfos { get; set; }
        public class CafeProductionParcelInfoServer
        {
            public ParcelKeyPair? Key { get; set; }
            public long Amount { get; set; }

            public CafeProductionParcelInfoServer() { }

            public CafeProductionParcelInfoServer(ParcelType parcelType, CurrencyTypes currencyType, long amount)
            {
                this.Key = new(parcelType, currencyType);
                this.Amount = amount;
            }
        }
    }
}