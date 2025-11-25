using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("give", "Give yourself items by name", "/give <itemname> <amount>")]
    internal class GiveCommand : Command
    {
        public GiveCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^.+$", "Item name or partial name")]
        public string ItemName { get; set; } = "";

        [Argument(1, @"^\d+$", "Amount to give")]
        public string AmountStr { get; set; } = "1";

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var itemExcel = connection.ExcelTableService.GetTable<ItemExcelT>();

            long amount = long.Parse(AmountStr);

            var lowerName = ItemName.ToLower();
            if (lowerName == "credit" || lowerName == "credits" || lowerName == "money" || lowerName == "currency")
            {
                await GiveItemsByCategory(context, itemExcel, ItemCategory.Coin, amount, "Currency");
                return;
            }

            if (lowerName == "activity" || lowerName == "level" || lowerName == "exp" || lowerName == "activityreport" || lowerName == "reports")
            {
                await GiveItemsByCategory(context, itemExcel, ItemCategory.CharacterExpGrowth, amount, "Activity Reports");
                return;
            }

            if (long.TryParse(ItemName, out long itemId))
            {
                var itemById = itemExcel.FirstOrDefault(x => x.Id == itemId);
                if (itemById != null)
                {
                    await GiveItem(context, itemById, amount);
                    return;
                }
            }

            var exactMatch = itemExcel.FirstOrDefault(x => 
                x.Icon != null && 
                x.Icon.Equals(ItemName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                await GiveItem(context, exactMatch, amount);
                return;
            }

            var partialMatches = itemExcel.Where(x => 
                x.Icon != null && 
                (x.Icon.Contains(ItemName, StringComparison.OrdinalIgnoreCase) ||
                 GetIconBaseName(x.Icon).Contains(ItemName, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => GetIconBaseName(x.Icon).Length)
                .Take(10)
                .ToList();

            if (partialMatches.Count == 1)
            {
                await GiveItem(context, partialMatches[0], amount);
                return;
            }

            if (partialMatches.Count > 1)
            {
                await connection.SendChatMessage($"Multiple items found matching '{ItemName}':");
                foreach (var item in partialMatches)
                {
                    var baseName = GetIconBaseName(item.Icon);
                    await connection.SendChatMessage($"  - {baseName} (ID: {item.Id}, Category: {item.ItemCategory})");
                }
                await connection.SendChatMessage($"Use item ID or be more specific.");
                return;
            }

            var fuzzyMatches = itemExcel.Where(x => 
                x.Icon != null && 
                LevenshteinDistance(GetIconBaseName(x.Icon).ToLower(), ItemName.ToLower()) <= 3)
                .OrderBy(x => LevenshteinDistance(GetIconBaseName(x.Icon).ToLower(), ItemName.ToLower()))
                .Take(10)
                .ToList();

            if (fuzzyMatches.Count > 0)
            {
                await connection.SendChatMessage($"Item '{ItemName}' not found. Did you mean:");
                foreach (var item in fuzzyMatches)
                {
                    var baseName = GetIconBaseName(item.Icon);
                    await connection.SendChatMessage($"  - {baseName} (ID: {item.Id})");
                }
            }
            else
            {
                await connection.SendChatMessage($"Item '{ItemName}' not found and no close matches.");
            }
        }

        private static string GetIconBaseName(string icon)
        {
            if (string.IsNullOrEmpty(icon)) return "";
            var parts = icon.Split('_');
            return parts.Length > 0 ? parts[^1] : icon;
        }

        private async Task GiveItem(SchaleDataContext context, ItemExcelT itemData, long amount)
        {
            var existing = context.Items.FirstOrDefault(x => 
                x.AccountServerId == connection.AccountServerId && 
                x.UniqueId == itemData.Id);

            if (existing != null)
            {
                existing.StackCount += amount;
                context.Items.Update(existing);
            }
            else
            {
                context.Items.Add(new ItemDBServer()
                {
                    AccountServerId = connection.AccountServerId,
                    UniqueId = itemData.Id,
                    StackCount = amount
                });
            }

            await context.SaveChangesAsync();
            var baseName = GetIconBaseName(itemData.Icon);
            await connection.SendChatMessage($"Added {amount}x {baseName} (ID: {itemData.Id})");
        }

        private async Task GiveItemsByCategory(SchaleDataContext context, List<ItemExcelT> itemExcel, ItemCategory category, long amount, string categoryName)
        {
            var items = itemExcel.Where(x => x.ItemCategory == category).ToList();
            int count = 0;

            foreach (var itemData in items)
            {
                var existing = context.Items.FirstOrDefault(x => 
                    x.AccountServerId == connection.AccountServerId && 
                    x.UniqueId == itemData.Id);

                if (existing != null)
                {
                    existing.StackCount += amount;
                    context.Items.Update(existing);
                }
                else
                {
                    context.Items.Add(new ItemDBServer()
                    {
                        AccountServerId = connection.AccountServerId,
                        UniqueId = itemData.Id,
                        StackCount = amount
                    });
                }
                count++;
            }

            await context.SaveChangesAsync();
            await connection.SendChatMessage($"Added {amount} to {count} {categoryName} items");
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; i++)
                d[i, 0] = i;
            for (int j = 0; j <= m; j++)
                d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}
