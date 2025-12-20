using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class GearDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.CharacterGear;

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
        public EquipmentDBServer? ToEquipmentDB { get; }
    }

    public static class GearDBServerExtensions
    {
        public static IQueryable<GearDBServer> GetAccountGears(this SchaleDataContext context, long accountId)
        {
            return context.Gears.Where(x => x.AccountServerId == accountId);
        }

        public static List<GearDBServer> AddGears(this SchaleDataContext context, long accountId, params GearDBServer[] gears)
        {
            if (gears == null || gears.Length == 0)
                return [];

            foreach (var gear in gears)
            {
                gear.AccountServerId = accountId;
                context.Gears.Add(gear);

                var boundCharacter = context.GetAccountCharacters(accountId)
                    .FirstOrDefault(c => c.ServerId == gear.BoundCharacterServerId);
                
                if (boundCharacter != null)
                {
                    while (boundCharacter.EquipmentServerIds.Count < 4)
                        boundCharacter.EquipmentServerIds.Add(0);
                    
                    boundCharacter.EquipmentServerIds[3] = gear.ServerId;
                }
            }

            return [.. gears];
        }
    }
}


