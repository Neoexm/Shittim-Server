using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class FurnitureDBServer : ConsumableItemBaseDBServer
    {
        public FurnitureLocation Location { get; set; } = FurnitureLocation.Inventory;
        public long CafeDBId { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float Rotation { get; set; }
        public long ItemDeploySequence { get; set; } = 0;

        [NotMapped]
        public override ParcelType Type { get => ParcelType.Furniture; }

        [NotMapped]
        [JsonIgnore]
        public override bool CanConsume { get => false; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public static class FurnitureDBServerExtensions
    {
        public static IQueryable<FurnitureDBServer> GetAccountFurnitures(this SCHALEContext context, long accountId)
        {
            return context.Furnitures.Where(x => x.AccountServerId == accountId);
        }

        public static IQueryable<FurnitureDBServer> GetCafeDeployedFurniture(this IQueryable<FurnitureDBServer> furnitures, long accountId, long cafeDbId)
        {
            return furnitures.Where(x => x.AccountServerId == accountId && x.ItemDeploySequence != 0 && x.CafeDBId == cafeDbId);
        }

        public static List<FurnitureDBServer> AddFurnitures(this SCHALEContext context, long accountId, params FurnitureDBServer[] furnitures)
        {
            if (furnitures == null || furnitures.Length == 0)
                return new List<FurnitureDBServer>();

            foreach (var furniture in furnitures)
            {
                furniture.AccountServerId = accountId;
                context.Furnitures.Add(furniture);
            }

            return furnitures.ToList();
        }
    }
}