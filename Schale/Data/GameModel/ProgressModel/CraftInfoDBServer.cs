using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.MX.GameLogic.DBModel; // Validation? DBModel namespace
using Schale.MX.GameLogic.Parcel; // For ParcelInfo

namespace Schale.Data.GameModel
{
    public class CraftInfoDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        // Properties from CraftInfoDB
        public long SlotSequence { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CraftSlotOpenDate { get; set; }
        public List<CraftNodeDB>? Nodes { get; set; }
        public List<long>? ResultIds { get; set; }
        public List<ParcelInfo>? RewardParcelInfos { get; set; }
    }
}
