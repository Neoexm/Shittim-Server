using Schale.Data;
using Microsoft.IdentityModel.Tokens;
using Shittim.GameMasters;
using Shittim.Services.Client;
using Schale.Data.GameModel;

namespace Shittim.Commands
{
    [CommandHandler("character", "Command to interact with user's characters", "/character [command] [characterID] [options] [parameters]")]
    internal class CharacterCommand : Command
    {
        public CharacterCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^add$|^remove$|^help$|^show$|^modify$", "The operation selected (add, remove, show, modify)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Op { get; set; } = string.Empty;

        [Argument(1, @"^[0-9]+$|^all$", "The target character (all or characterID)", ArgumentFlags.Optional)]
        public string Target { get; set; } = string.Empty;

        [Argument(2, @"^barebone$|^basic$|^ue30$|^ue50$|^max$|^level$|^star$|^skill$|^ps$", "Character options (basic, ue30, ue50, max) or modify options (level, star, skill, ps)", ArgumentFlags.Optional)]
        public string Options { get; set; } = string.Empty;

        [Argument(3, @"^[0-9\s]+$", "Modification parameters", ArgumentFlags.Optional | ArgumentFlags.Remaining)]
        public string ModifyParams { get; set; } = string.Empty;

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);
            if (string.IsNullOrEmpty(Op) || Op.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            if (Op.ToLower() == "show")
            {
                await ShowCharacters(context);
                return;
            }


            var characterDB = context.Characters;
            var options = string.IsNullOrEmpty(Options) ? "barebone" : Options;
            List<string> optionList = ["barebone", "basic", "ue30", "ue50", "max", "level", "star", "skill", "ps"];

            if (!optionList.Contains(options) && options.Length > 0 && !Op.ToLower().Equals("add"))
            {
                await connection.SendChatMessage("Unknown options!");
                await ShowHelp();
                return;
            }

            uint characterId = 0;
            bool isValidId = Target != "all" && uint.TryParse(Target, out characterId);

            switch (Op.ToLower())
            {
                case "add":
                    await connection.SendChatMessage("Selected options: " + options);
                    if (Target == "all")
                    {
                        await InventoryGM.AddAllCharacters(connection, options);
                        await connection.SendChatMessage("All Characters Added!");
                    }
                    else if (isValidId)
                    {
                        if (characterDB.Any(x => x.UniqueId == characterId))
                        {
                            await connection.SendChatMessage($"{characterId} already exists!");
                            return;
                        }

                        await CharacterGM.AddCharacter(context, account, characterId, options);
                        await connection.SendChatMessage($"{characterId} added!");
                    }
                    else
                    {
                        await connection.SendChatMessage("Invalid Character ID!");
                        await ShowHelp();
                        return;
                    }
                    break;

                case "remove":
                    if (Target == "all")
                    {
                        await InventoryGM.RemoveAllCharacters(connection);
                        await connection.SendChatMessage("All Characters Removed!");
                    }
                    else if (isValidId)
                    {
                        await CharacterGM.RemoveCharacter(connection, characterId);
                        await connection.SendChatMessage($"{characterId} removed!");
                    }
                    else
                    {
                        await connection.SendChatMessage("Invalid Character ID!");
                        await ShowHelp();
                        return;
                    }
                    break;

                case "modify":
                    if (Target == "all")
                    {
                        await CharacterGM.ModifyAllCharacters(connection, Options, ModifyParams);
                        await connection.SendChatMessage("Modified All Characters");
                    }
                    else if (isValidId)
                    {
                        await CharacterGM.ModifyCharacter(connection, characterId, Options, ModifyParams);
                    }
                    else
                    {
                        await connection.SendChatMessage("Invalid Character ID!");
                        await ShowHelp();
                        return;
                    }
                    break;

                default:
                    await connection.SendChatMessage("Unknown command!");
                    await ShowHelp();
                    return;
            }
        }

        private async Task ShowCharacters(SchaleDataContext context)
        {
            var characters = context.Characters
                .OrderBy(x => x.UniqueId);

            await connection.SendChatMessage("Current Characters:");
            foreach (var character in characters)
            {
                await connection.SendChatMessage($"Character ID: {character.UniqueId}");
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/character - Command to interact with user's characters");
            await connection.SendChatMessage("Usage: /character [command] [characterID] [options] [parameters]");
            await connection.SendChatMessage("Command: add | remove | modify | show");
            await connection.SendChatMessage("CharacterID: all | characterID");
            await connection.SendChatMessage("Options for add: barebone | basic | ue30 | ue50 | max");
            await connection.SendChatMessage("Options for modify:");
            await connection.SendChatMessage("  level {level} - Set character level");
            await connection.SendChatMessage("  star {star} - Set character star grade");
            await connection.SendChatMessage("  skill {skill1 skill2 skill3 skill4} - Set skill levels");
            await connection.SendChatMessage("  ps {ps1 ps2 ps3} - Set potential stats");
            await connection.SendChatMessage("/character show - List all current characters");
        }
    }
}
