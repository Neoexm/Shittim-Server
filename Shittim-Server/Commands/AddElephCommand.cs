using Shittim.Services.Client;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;

namespace Shittim.Commands
{
    [CommandHandler("addeleph", "Add character elephs: /addeleph <charactername> <amount>", "/addeleph <character> <amount>")]
    internal class AddElephCommand : Command
    {
        public AddElephCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^.+$", "Character name (e.g., Kotama, Aru, Hoshino)", ArgumentFlags.Optional)]
        public string CharacterName { get; set; } = "";

        [Argument(1, @"^\d+$", "Amount of elephs to add", ArgumentFlags.Optional)]
        public string AmountStr { get; set; } = "9999";

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(CharacterName))
            {
                await connection.SendChatMessage("Usage: /addeleph <charactername> <amount>");
                await connection.SendChatMessage("Example: /addeleph Kotama 9999");
                return;
            }

            long amount = long.Parse(AmountStr);

            using var context = await connection.Context.CreateDbContextAsync();
            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();

            var character = characterExcel.FirstOrDefault(x => 
                x.DevName != null && 
                x.DevName.Contains(CharacterName, StringComparison.OrdinalIgnoreCase));

            if (character == null)
            {
                await connection.SendChatMessage($"Character '{CharacterName}' not found!");
                return;
            }

            var elephItemId = character.CharacterPieceItemId;

            if (elephItemId == 0)
            {
                await connection.SendChatMessage($"{character.DevName} has no eleph item!");
                return;
            }

            var existingItem = context.Items.FirstOrDefault(x =>
                x.AccountServerId == connection.AccountServerId &&
                x.UniqueId == elephItemId);

            if (existingItem != null)
            {
                existingItem.StackCount = amount;
                context.Items.Update(existingItem);
            }
            else
            {
                context.Items.Add(new ItemDBServer()
                {
                    AccountServerId = connection.AccountServerId,
                    UniqueId = elephItemId,
                    StackCount = amount
                });
            }

            await context.SaveChangesAsync();

            await connection.SendChatMessage($"Added {amount} elephs for {character.DevName}!");
        }
    }
}
