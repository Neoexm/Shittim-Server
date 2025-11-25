using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
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
        public static IQueryable<ItemDBServer> GetAccountItems(this SchaleDataContext context, long accountId)
        {
            return context.Items.Where(x => x.AccountServerId == accountId);
        }

        public static List<ItemDBServer> AddItems(this SchaleDataContext context, long accountId, params ItemDBServer[] items)
        {
            if (items == null || items.Length == 0)
                return [];

            foreach (var item in items)
            {
                item.AccountServerId = accountId;
                
                var existingItem = context.GetAccountItems(accountId)
                    .FirstOrDefault(x => x.UniqueId == item.UniqueId);

                if (existingItem != null)
                {
                    existingItem.StackCount += item.StackCount;
                }
                else
                {
                    context.Items.Add(item);
                }
            }

            return [.. items];
        }
    }
}


