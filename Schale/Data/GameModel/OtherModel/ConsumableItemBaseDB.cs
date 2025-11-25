using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public abstract class ConsumableItemBaseDBServer : ParcelBase
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public ParcelKeyPair? Key { get; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public long UniqueId { get; set; }
        public long StackCount { get; set; }

        [JsonIgnore]
        public abstract bool CanConsume { get; }
    }
}


