using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class EquipmentDBServer : ConsumableItemBaseDBServer
    {
        [NotMapped]
        public override ParcelType Type { get => ParcelType.Equipment; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int Tier { get; set; } = 1;
        public long BoundCharacterServerId { get; set; }

        [JsonIgnore]
        public override bool CanConsume { get => false; }
    }

    public static class EquipmentDBServerExtensions
    {
        public static IQueryable<EquipmentDBServer> GetAccountEquipments(this SCHALEContext context, long accountId)
        {
            return context.Equipments.Where(x => x.AccountServerId == accountId);
        }

        public static List<EquipmentDBServer> AddEquipment(this SCHALEContext context, long accountId, params EquipmentDBServer[] equipmentDB)
        {
            if (equipmentDB == null || equipmentDB.Length == 0)
                return new List<EquipmentDBServer>();

            foreach (var equipment in equipmentDB)
            {
                equipment.AccountServerId = accountId;
                var existing = context.GetAccountEquipments(accountId)
                    .FirstOrDefault(x => x.UniqueId == equipment.UniqueId);

                if (existing != null && equipment.BoundCharacterServerId == default)
                    existing.StackCount += equipment.StackCount;
                else
                    context.Equipments.Add(equipment);
            }

            return equipmentDB.ToList();
        }
    }
}