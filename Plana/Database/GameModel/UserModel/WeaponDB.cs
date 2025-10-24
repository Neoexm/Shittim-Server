using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class WeaponDBServer : ParcelBase
    {

        [NotMapped]
        public override ParcelType Type { get => ParcelType.CharacterWeapon; }

        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        public long UniqueId { get; set; }
        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int StarGrade { get; set; } = 1;
        public long BoundCharacterServerId { get; set; }
    }

    public static class WeaponDBServerExtensions
    {
        public static WeaponDBServer GetWeapon(this IQueryable<WeaponDBServer> weapons, long accountId)
        {
            return weapons.First(x => x.AccountServerId == accountId);
        }

        public static IQueryable<WeaponDBServer> GetAccountWeapons(this SCHALEContext context, long accountId)
        {
            return context.Weapons.Where(x => x.AccountServerId == accountId);
        }

        public static List<WeaponDBServer> AddWeapons(this SCHALEContext context, long accountId, params WeaponDBServer[] weapons)
        {
            if (weapons == null || weapons.Length == 0)
                return new List<WeaponDBServer>();

            foreach (var weapon in weapons)
            {
                weapon.AccountServerId = accountId;
                var existing = context.GetAccountWeapons(accountId)
                    .FirstOrDefault(x => x.UniqueId == weapon.UniqueId);

                if (existing == null) context.Weapons.Add(weapon);
            }

            return weapons.ToList();
        }
    } 
}