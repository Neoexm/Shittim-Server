using Schale.FlatData;
using Schale.Data;
using Microsoft.EntityFrameworkCore;
using Shittim.Services.Client;
using Schale.Data.GameModel;

namespace Shittim.Commands
{
    [CommandHandler("currency", "Command to manage currency", "/currency [currencyId] [amount]")]
    internal class CurrencyCommand : Command
    {
        public CurrencyCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^[a-zA-Z0-9]+$|^show$", "The id or name of currency you want to change its amount", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string id { get; set; } = string.Empty;

        [Argument(1, @"", "amount", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string amountStr { get; set; } = string.Empty;

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            if (string.IsNullOrEmpty(id) || id.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            if (id.ToLower() == "show")
            {
                await ShowCurrencies(context);
                return;
            }

            var currencyType = CurrencyTypes.Invalid;
            long amount = 0;

            if (!Enum.TryParse(id, true, out currencyType))
            {
                switch (id.ToLower())
                {
                    case "credits":
                        currencyType = (CurrencyTypes)1;
                        break;
                    case "pyroxenes":
                        currencyType = (CurrencyTypes)2;
                        break;
                    default:
                        if (int.TryParse(id, out int currencyId))
                        {
                            currencyType = (CurrencyTypes)currencyId;
                        }
                        break;
                }
            }

            if (currencyType != CurrencyTypes.Invalid && long.TryParse(amountStr, out amount))
            {
                var currencies = context.Currencies.First(x => x.AccountServerId == account.ServerId);
                currencies.CurrencyDict[currencyType] = amount;
                currencies.UpdateTimeDict[currencyType] = account.GameSettings.ServerDateTime();
                currencies.UpdateGem(account.GameSettings.ServerDateTime());
                context.Entry(currencies).State = EntityState.Modified;
                await context.SaveChangesAsync();

                await connection.SendChatMessage($"Set amount of {currencyType} to {amount:N0}!");
            }
            else
            {
                await connection.SendChatMessage("Invalid Currency ID or Amount!");
                await ShowHelp();
            }
        }

        private async Task ShowCurrencies(SchaleDataContext context)
        {
            var account = context.GetAccount(connection.AccountServerId);
            
            await connection.SendChatMessage("Available Currencies:");
            var currencyTypes = Enum.GetValues<CurrencyTypes>()
                .Where(x => x != CurrencyTypes.Invalid)
                .OrderBy(x => (int)x);

            foreach (var type in currencyTypes)
            {
                var amount = context.Currencies.First(x => x.AccountServerId == account.ServerId).CurrencyDict[type];
                await connection.SendChatMessage($"{(int)type} - {type}: {amount:N0}");
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/Currency - Command to manage currency");
            await connection.SendChatMessage("Usage: /Currency [currencyID] [amount]");
            await connection.SendChatMessage("CurrencyID: credits | pyroxenes | 1, 2, 3...");
            await connection.SendChatMessage("/currency show - List all currencies and their amounts");
        }
    }
}
