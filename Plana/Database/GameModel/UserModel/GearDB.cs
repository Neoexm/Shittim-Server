using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class GearDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type { get => ParcelType.CharacterGear; }

        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        [Key]
        public long ServerId { get; set; }

        public long UniqueId { get; set; }
        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int Tier { get; set; }
        public long SlotIndex { get; set; } = 4;
        public long BoundCharacterServerId { get; set; }

        [JsonIgnore]
        [NotMapped]
        public EquipmentDBServer? ToEquipmentDB
        {
            get;
            /*{
                return new()
                {
                    IsNew = true,
                    ServerId = ServerId,
                    BoundCharacterServerId = BoundCharacterServerId,
                    Tier = Tier,
                    Level = Level,
                    StackCount = 1,
                    Exp = Exp
                };
            }*/
        }
    }

    public static class GearDBServerExtensions
    {
        public static IQueryable<GearDBServer> GetAccountGears(this SCHALEContext context, long accountId)
        {
            return context.Gears.Where(x => x.AccountServerId == accountId);
        }

        public static List<GearDBServer> AddGears(this SCHALEContext context, long accountId, params GearDBServer[] gears)
        {
            if (gears == null || gears.Length == 0)
                return new List<GearDBServer>();

            foreach (var gear in gears)
            {
                gear.AccountServerId = accountId;
                context.Gears.Add(gear);

                var target = context.GetAccountCharacters(accountId)
                    .FirstOrDefault(c => c.ServerId == gear.BoundCharacterServerId);
                if (target != null)
                {
                    while (target.EquipmentServerIds.Count < 4)
                        target.EquipmentServerIds.Add(0);
                    target.EquipmentServerIds[3] = gear.ServerId;
                }
            }

            return gears.ToList();
        }
    }
}