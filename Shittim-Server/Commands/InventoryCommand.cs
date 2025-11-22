using Microsoft.IdentityModel.Tokens;
using Shittim.GameMasters;
using Shittim.Services.Client;
using Schale.Data.GameModel;
using Schale.FlatData;

namespace Shittim.Commands
{
    [CommandHandler("inventory", "Command to manage inventory", "/inventory [command] [type] [options]")]
    internal class InventoryCommand : Command
    {
        public InventoryCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^add$|^remove$|^help$", "The operation selected (add, remove, help)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Op { get; set; } = string.Empty;

        [Argument(1, @"^all$|^characters$|^weapons$|^equipments$|^items$|^gears$|^lobbies$|^scenarios$|^furnitures$|^emblems$", 
            "The type to manage (all, characters, weapons, equipments, items, gears, lobbies, scenarios, furnitures, emblems)", 
            ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Type { get; set; } = string.Empty;

        [Argument(2, @"^barebone$|^basic$|^ue30$|^ue50$|^max$", 
            "The options selected (basic, ue30, ue50, max)", 
            ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Options { get; set; } = string.Empty;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(Op) || Op.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            using var context = await connection.Context.CreateDbContextAsync();
            var options = string.IsNullOrEmpty(Options) ? "barebone" : Options;
            List<string> optionList = ["barebone", "basic", "ue30", "ue50", "max"];
            if (!optionList.Contains(options) && options.Length > 0)
            {
                await connection.SendChatMessage("Unknown options!");
                await ShowHelp();
                return;
            }

            switch (Op.ToLower())
            {
                case "add":
                    await connection.SendChatMessage("Selected options: " + options);
                    switch (Type.ToLower())
                    {
                        case "all":
                            await InventoryGM.AddAllCharacters(connection, options);
                            await InventoryGM.AddAllWeapons(connection, options);
                            await InventoryGM.AddAllItems(connection);
                            await InventoryGM.AddAllEquipment(connection, options);
                            await InventoryGM.AddAllGears(connection, options);
                            await InventoryGM.AddAllMemoryLobbies(connection);
                            await InventoryGM.AddAllScenarios(connection);
                            await InventoryGM.AddAllFurnitures(connection);
                            await InventoryGM.AddAllEmblems(connection);
                            await connection.SendChatMessage("Added Everything!");
                            break;
                        case "characters":
                            await InventoryGM.AddAllCharacters(connection, options);
                            break;
                        case "weapons":
                            await InventoryGM.AddAllWeapons(connection, options);
                            break;
                        case "items":
                            await InventoryGM.AddAllItems(connection);
                            break;
                        case "equipments":
                            await InventoryGM.AddAllEquipment(connection, options);
                            break;
                        case "gears":
                            await InventoryGM.AddAllGears(connection, options);
                            break;
                        case "lobbies":
                            await InventoryGM.AddAllMemoryLobbies(connection);
                            break;
                        case "scenarios":
                            await InventoryGM.AddAllScenarios(connection);
                            break;
                        case "furnitures":
                            await InventoryGM.AddAllFurnitures(connection);
                            break;
                        case "emblems":
                            await InventoryGM.AddAllEmblems(connection);
                            break;
                        default:
                            await connection.SendChatMessage("Unknown type!");
                            await ShowHelp();
                            return;
                    }
                    break;

                case "remove":
                    switch (Type.ToLower())
                    {
                        case "all":
                            await InventoryGM.RemoveAllCharacters(connection);
                            context.Weapons.RemoveRange(context.GetAccountWeapons(connection.AccountServerId));
                            context.Items.RemoveRange(context.GetAccountItems(connection.AccountServerId));
                            context.Equipments.RemoveRange(context.GetAccountEquipments(connection.AccountServerId));
                            context.Gears.RemoveRange(context.GetAccountGears(connection.AccountServerId));
                            context.MemoryLobbies.RemoveRange(context.GetAccountMemoryLobbies(connection.AccountServerId));
                            context.ScenarioHistories.RemoveRange(context.GetAccountScenarioHistories(connection.AccountServerId));
                            context.Furnitures.RemoveRange(context.GetAccountFurnitures(connection.AccountServerId).Where(x => x.Location == FurnitureLocation.Inventory));
                            context.Emblems.RemoveRange(context.GetAccountEmblems(connection.AccountServerId));
                            await connection.SendChatMessage("Removed Everything!");
                            break;
                        case "characters":
                            await InventoryGM.RemoveAllCharacters(connection);
                            await connection.SendChatMessage("Removed all characters!");
                            break;
                        case "items":
                            context.Items.RemoveRange(context.Items.Where(x => x.AccountServerId == connection.AccountServerId));
                            context.Equipments.RemoveRange(context.Equipments.Where(x => x.AccountServerId == connection.AccountServerId && x.BoundCharacterServerId == 0));
                            await connection.SendChatMessage("Removed all items!");
                            break;
                        case "equipments":
                            await InventoryGM.AddAllEquipment(connection, "remove");
                            await connection.SendChatMessage("Removed all equipment!");
                            break;
                        case "gears":
                            await InventoryGM.AddAllGears(connection, "remove");
                            await connection.SendChatMessage("Removed all gears!");
                            break;
                        case "lobbies":
                            context.MemoryLobbies.RemoveRange(context.GetAccountMemoryLobbies(connection.AccountServerId));
                            await connection.SendChatMessage("Removed all lobbies!");
                            break;
                        case "scenarios":
                            context.ScenarioHistories.RemoveRange(context.GetAccountScenarioHistories(connection.AccountServerId));
                            await connection.SendChatMessage("Removed all scenarios!");
                            break;
                        case "furnitures":
                            context.Furnitures.RemoveRange(context.GetAccountFurnitures(connection.AccountServerId).Where(x => x.Location == FurnitureLocation.Inventory));
                            await connection.SendChatMessage("Removed all furnitures!");
                            break;
                        case "emblems":
                            context.Emblems.RemoveRange(context.GetAccountEmblems(connection.AccountServerId));
                            await connection.SendChatMessage("Removed all emblems!");
                            break;
                        default:
                            await connection.SendChatMessage("Unknown type!");
                            await ShowHelp();
                            return;
                    }
                    break;

                default:
                    await connection.SendChatMessage("Unknown operation!");
                    await ShowHelp();
                    return;
            }

            await context.SaveChangesAsync();
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/inventory - Command to manage inventory.");
            await connection.SendChatMessage("Usage: /inventory [command] [type] [options]");
            await connection.SendChatMessage("Commands: add | remove");
            await connection.SendChatMessage("Type: all | characters | weapons | equipments | items | gears | lobbies | scenarios | furniture");
            await connection.SendChatMessage("Options: barebone | basic | ue30 | ue50 | max");
        }
    }
}
