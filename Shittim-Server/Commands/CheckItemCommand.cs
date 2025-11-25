using Shittim.Services.Client;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("checkitem", "Check item details: /checkitem <itemname>", "/checkitem <name>")]
    internal class CheckItemCommand : Command
    {
        public CheckItemCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^.+$", "Item name to search for", ArgumentFlags.Optional)]
        public string ItemName { get; set; } = "";

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(ItemName))
            {
                await connection.SendChatMessage("Usage: /checkitem <itemname>");
                await connection.SendChatMessage("Example: /checkitem Eligma");
                return;
            }

            var itemExcel = connection.ExcelTableService.GetTable<Schale.FlatData.ItemExcelT>();

            var items = itemExcel.Where(x => 
                x.Icon != null && 
                x.Icon.Contains(ItemName, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            if (!items.Any())
            {
                await connection.SendChatMessage($"No items found matching '{ItemName}'");
                return;
            }

            foreach (var item in items)
            {
                await connection.SendChatMessage($"ID: {item.Id} | Name: {item.Icon} | Category: {item.ItemCategory} | StackMax: {item.StackableMax}");
            }
        }
    }
}
