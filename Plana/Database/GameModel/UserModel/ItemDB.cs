using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class ItemDBServer : ConsumableItemBaseDBServer
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.Item;

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        [NotMapped]
        [JsonIgnore]
        public override bool CanConsume => true;
    }

    public static class ItemDBServerExtensions
    {
        public static IQueryable<ItemDBServer> GetAccountItems(this SCHALEContext context, long accountId)
        {
            return context.Items.Where(x => x.AccountServerId == accountId);
        }

        public static List<ItemDBServer> AddItems(this SCHALEContext context, long accountId, params ItemDBServer[] items)
        {
            if (items == null || items.Length == 0)
                return new List<ItemDBServer>();

            foreach (var item in items)
            {
                item.AccountServerId = accountId;
                var existing = context.GetAccountItems(accountId)
                    .FirstOrDefault(x => x.UniqueId == item.UniqueId);

                if (existing != null)
                    existing.StackCount += item.StackCount;
                else
                    context.Items.Add(item);
            }

            return items.ToList();
        }
    }
}