using Shittim.Services.Client;

namespace Shittim.Commands
{
    [CommandHandler("clearinventory", "Removes ALL items and equipment from inventory", "/clearinventory")]
    internal class ClearInventoryCommand : Command
    {
        public ClearInventoryCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        public override async Task Execute()
        {
            await connection.SendChatMessage("Clearing inventory...");

            using var context = await connection.Context.CreateDbContextAsync();

            var items = context.Items.Where(x => x.AccountServerId == connection.AccountServerId).ToList();
            var equipment = context.Equipments.Where(x => x.AccountServerId == connection.AccountServerId && x.BoundCharacterServerId == 0).ToList();

            context.Items.RemoveRange(items);
            context.Equipments.RemoveRange(equipment);

            await context.SaveChangesAsync();

            await connection.SendChatMessage($"Cleared {items.Count} items and {equipment.Count} equipment from inventory.");
        }
    }
}
