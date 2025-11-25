using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("listitem", "List items by category or search term", "/listitem <category|searchterm>")]
    internal class ListItemCommand : Command
    {
        public ListItemCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^.+$", "Category or search term (currency, material, favor, eleph, training, etc)")]
        public string SearchTerm { get; set; } = "";

        public override async Task Execute()
        {
            var itemExcel = connection.ExcelTableService.GetTable<ItemExcelT>();
            
            var lowerTerm = SearchTerm.ToLower();
            List<ItemExcelT> results = new List<ItemExcelT>();

            switch (lowerTerm)
            {
                case "currency":
                case "credit":
                case "credits":
                case "money":
                case "coin":
                case "coins":
                    results = itemExcel.Where(x => x.ItemCategory == ItemCategory.Coin).ToList();
                    await connection.SendChatMessage($"=== CURRENCY ITEMS ===");
                    break;

                case "material":
                case "materials":
                case "mat":
                case "mats":
                    results = itemExcel.Where(x => x.ItemCategory == ItemCategory.Material).ToList();
                    await connection.SendChatMessage($"=== MATERIAL ITEMS ===");
                    break;

                case "favor":
                case "gift":
                case "gifts":
                    results = itemExcel.Where(x => x.ItemCategory == ItemCategory.Favor).ToList();
                    await connection.SendChatMessage($"=== FAVOR ITEMS (GIFTS) ===");
                    break;

                case "eleph":
                case "elephs":
                case "shard":
                case "shards":
                case "secretstone":
                    results = itemExcel.Where(x => x.ItemCategory == ItemCategory.SecretStone).ToList();
                    await connection.SendChatMessage($"=== CHARACTER ELEPHS ===");
                    break;

                case "training":
                case "skill":
                case "bluray":
                case "bd":
                    results = itemExcel.Where(x => 
                        x.Icon != null && 
                        (x.Icon.Contains("Skill", StringComparison.OrdinalIgnoreCase) || 
                         x.Icon.Contains("BD_", StringComparison.OrdinalIgnoreCase))).ToList();
                    await connection.SendChatMessage($"=== TRAINING ITEMS (BLU-RAYS) ===");
                    break;

                case "activity":
                case "exp":
                case "level":
                case "report":
                case "reports":
                    results = itemExcel.Where(x => x.ItemCategory == ItemCategory.CharacterExpGrowth).ToList();
                    await connection.SendChatMessage($"=== ACTIVITY REPORTS ===");
                    break;

                case "all":
                    await connection.SendChatMessage("Categories: currency, material, favor, eleph, training, activity");
                    await connection.SendChatMessage("Or search by name for specific items");
                    return;

                default:
                    results = itemExcel.Where(x => 
                        x.Icon != null && 
                        (x.Icon.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                         GetIconBaseName(x.Icon).Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)))
                        .Take(50)
                        .ToList();
                    await connection.SendChatMessage($"=== SEARCH RESULTS FOR '{SearchTerm}' ===");
                    break;
            }

            if (results.Count == 0)
            {
                await connection.SendChatMessage("No items found!");
                return;
            }

            int shown = 0;
            foreach (var item in results.Take(30))
            {
                var baseName = GetIconBaseName(item.Icon);
                await connection.SendChatMessage($"{item.Id}: {baseName} (Stack: {item.StackableMax})");
                shown++;
            }

            if (results.Count > 30)
            {
                await connection.SendChatMessage($"... and {results.Count - 30} more (showing first 30)");
            }

            await connection.SendChatMessage($"Total: {results.Count} items. Use /give <id> <amount>");
        }

        private static string GetIconBaseName(string icon)
        {
            if (string.IsNullOrEmpty(icon)) return "";
            var parts = icon.Split('_');
            return parts.Length > 0 ? parts[^1] : icon;
        }
    }
}
