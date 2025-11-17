using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class EquipmentDBServer : ConsumableItemBaseDBServer
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.Equipment;

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int Tier { get; set; } = 1;
        public long BoundCharacterServerId { get; set; }

        [JsonIgnore]
        public override bool CanConsume => false;
    }

    public static class EquipmentDBServerExtensions
    {
        public static IQueryable<EquipmentDBServer> GetAccountEquipments(this SchaleDataContext context, long accountId)
        {
            return context.Equipments.Where(x => x.AccountServerId == accountId);
        }

        public static List<EquipmentDBServer> AddEquipment(this SchaleDataContext context, long accountId, params EquipmentDBServer[] equipmentDB)
        {
            if (equipmentDB == null || equipmentDB.Length == 0)
                return [];

            foreach (var equipment in equipmentDB)
            {
                equipment.AccountServerId = accountId;
                
                var existingEquipment = context.GetAccountEquipments(accountId)
                    .FirstOrDefault(x => x.UniqueId == equipment.UniqueId);

                if (existingEquipment != null && equipment.BoundCharacterServerId == default)
                {
                    existingEquipment.StackCount += equipment.StackCount;
                }
                else
                {
                    context.Equipments.Add(equipment);
                }
            }

            return [.. equipmentDB];
        }
    }
}


