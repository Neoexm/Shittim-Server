using Shittim.Services.Client;
using Schale.Excel;
using Schale.FlatData;
using System.Text;

namespace Shittim.Commands
{
    [CommandHandler("gacha", "Command to set gacha rates and guarantee", "/gacha [type] [value]")]
    internal class GachaCommand : Command
    {
        public GachaCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^rate$|^guarantee$|^reset$|^settings$|^show$|^help$", "Operation type (rate, guarantee, reset, show, list)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Type { get; set; } = string.Empty;

        [Argument(1, @"^[0-9]+$|^ssr$|^sr$|^r$", "Value (pickup character or rarity)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Value { get; set; } = string.Empty;

        [Argument(2, @"^[0-9]+(\.[0-9]+)?$", "Rate percentage (0-100)", ArgumentFlags.Optional)]
        public string Rate { get; set; } = string.Empty;

        private static Dictionary<long, double> customRates = new();
        private static CharacterExcelT? guaranteedCharacterId = null;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(Type) || Type.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            switch (Type.ToLower())
            {
                case "rate":
                    if (string.IsNullOrEmpty(Value) || string.IsNullOrEmpty(Rate))
                    {
                        await connection.SendChatMessage("Please specify both rarity and rate!");
                        return;
                    }
                    await SetRate(Value, Rate);
                    break;

                case "guarantee":
                    if (string.IsNullOrEmpty(Value))
                    {
                        await connection.SendChatMessage("Please specify character ID!");
                        return;
                    }
                    await SetGuarantee(Value);
                    break;

                case "show":
                    await ListAvailableCharacters();
                    break;

                case "reset":
                    await ResetGachaSettings();
                    break;

                case "settings":
                    await ShowCurrentSettings();
                    break;

                default:
                    await connection.SendChatMessage("Unknown command!");
                    await ShowHelp();
                    break;
            }
        }

        private async Task ListAvailableCharacters()
        {
            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();
            var characters = characterExcel
                .GetReleaseCharacters()
                .ToList();

            if (!characters.Any())
            {
                await connection.SendChatMessage("No characters found!");
                return;
            }

            await connection.SendChatMessage("Available Characters:");
            var currentRarity = -1;
            var sb = new StringBuilder();

            foreach (var character in characters)
            {
                if (currentRarity != character.DefaultStarGrade)
                {
                    if (sb.Length > 0)
                    {
                        await connection.SendChatMessage(sb.ToString());
                        sb.Clear();
                    }
                    
                    string rarity = character.DefaultStarGrade switch
                    {
                        3 => "SSR",
                        2 => "SR",
                        1 => "R",
                        _ => $"{character.DefaultStarGrade}★"
                    };
                    await connection.SendChatMessage($"\n{rarity} Characters:");
                    currentRarity = character.DefaultStarGrade;
                }

                sb.Append($"[{character.Id}] {character.DevName}, ");

                if (sb.Length > 200)
                {
                    await connection.SendChatMessage(sb.ToString().TrimEnd(',', ' '));
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
            {
                await connection.SendChatMessage(sb.ToString().TrimEnd(',', ' '));
            }
        }

        private async Task SetRate(string rarity, string rateStr)
        {
            if (!double.TryParse(rateStr, out double rate) || rate < 0 || rate > 100)
            {
                await connection.SendChatMessage("Invalid rate! Please use a number between 0 and 100.");
                return;
            }

            switch (rarity.ToLower())
            {
                case "ssr":
                    customRates[3] = rate;
                    await connection.SendChatMessage($"SSR rate set to {rate}%");
                    break;
                case "sr":
                    customRates[2] = rate;
                    await connection.SendChatMessage($"SR rate set to {rate}%");
                    break;
                case "r":
                    customRates[1] = rate;
                    await connection.SendChatMessage($"R rate set to {rate}%");
                    break;
                default:
                    await connection.SendChatMessage("Invalid rarity! Use SSR, SR, or R.");
                    break;
            }
        }

        private async Task SetGuarantee(string characterId)
        {
            if (!long.TryParse(characterId, out long id))
            {
                await connection.SendChatMessage("Invalid character ID!");
                return;
            }

            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();
            var character = characterExcel.FirstOrDefault(x => x.Id == id);

            if (character == null)
            {
                await connection.SendChatMessage("Character not found!");
                return;
            }

            guaranteedCharacterId = character;
            await connection.SendChatMessage($"Guaranteed character set to {id}");
        }

        private async Task ResetGachaSettings()
        {
            customRates.Clear();
            guaranteedCharacterId = null;
            await connection.SendChatMessage("Gacha settings reset to default");
        }

        private async Task ShowCurrentSettings()
        {
            await connection.SendChatMessage("Current Gacha Settings:");
            
            if (customRates.Count > 0)
            {
                foreach (var rate in customRates)
                {
                    string rarity = rate.Key switch
                    {
                        3 => "SSR",
                        2 => "SR",
                        1 => "R",
                        _ => "Unknown"
                    };
                    await connection.SendChatMessage($"{rarity} Rate: {rate.Value}%");
                }
            }
            else
                await connection.SendChatMessage("Using default rates");

            if (guaranteedCharacterId != null)
                await connection.SendChatMessage($"Guaranteed Character ID: {guaranteedCharacterId}");
            else
                await connection.SendChatMessage("No guaranteed character set");
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/gacha - Command to set gacha rates and guarantee");
            await connection.SendChatMessage("Usage: /gacha [type] [value] [rate]");
            await connection.SendChatMessage("Types:");
            await connection.SendChatMessage("rate [rarity] [percentage] - Set rate for rarity (SSR/SR/R)");
            await connection.SendChatMessage("guarantee [pickupcharacterId] - Set guaranteed character (Warning: This Operation can break your client if your not careful)");
            await connection.SendChatMessage("/gacha show - Show list of available Pickup Characters");
            await connection.SendChatMessage("/gacha reset - Reset to default settings");
            await connection.SendChatMessage("/gacha settings - Show current settings");
        }

        public static Dictionary<long, double> GetCustomRates() => customRates;
        public static CharacterExcelT? GetGuaranteedCharacter() => guaranteedCharacterId;
    }
} 
