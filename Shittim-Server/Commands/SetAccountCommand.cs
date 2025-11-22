using Schale.MX.GameLogic.DBModel;
using System.ComponentModel;
using System.Reflection;
using Shittim.Extensions;
using Serilog;
using Shittim.Services.Client;
using Schale.Data.GameModel;

namespace Shittim.Commands
{
    [CommandHandler("setaccount", "Command to change player's account data", "/setaccount [type] [value]")]
    internal class SetAccountCommand : Command
    {
        public SetAccountCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^[a-zA-Z]+$", "The Account Property you want to change", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Property { get; set; } = string.Empty;

        [Argument(1, @"", "The value you want to change it to", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Value { get; set; } = string.Empty;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(Property) || Property.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            if (Property.ToLower() == "show")
            {
                await ShowProperties();
                return;
            }

            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);
            PropertyInfo? targetProperty = typeof(AccountDBServer).GetProperty(Property) ?? typeof(AccountDBServer).GetProperty(Property.Capitalize());

            if (targetProperty != null)
            {
                TypeConverter converter = TypeDescriptor.GetConverter(targetProperty.PropertyType);

                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    try
                    {
                        object targetValue = converter.ConvertFromString(Value);

                        targetProperty.SetValue(account, targetValue);
                        await context.SaveChangesAsync();

                        await connection.SendChatMessage($"Set Player with UID {connection.AccountServerId}'s {Property} to {Value}");
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Invalid Value");
                        throw new ArgumentException("Invalid Value");
                    }
                }
            }
            else
            {
                await connection.SendChatMessage("Invalid Account Property!");
                ShowHelp();
            }
        }

        private async Task ShowProperties()
        {
            await connection.SendChatMessage("Available Account Properties:");
            var properties = typeof(AccountDB).GetProperties()
                .Where(p => p.CanWrite && TypeDescriptor.GetConverter(p.PropertyType).CanConvertFrom(typeof(string)))
                .OrderBy(p => p.Name);
                
            foreach (var prop in properties)
            {
                await connection.SendChatMessage($"{prop.Name} - {prop.PropertyType.Name}");
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/setaccount - Command to change player's account data");
            await connection.SendChatMessage("Usage: /setaccount [type] [value]");
            await connection.SendChatMessage("Type: level | nickname | raidseasonid | property | ...");
            await connection.SendChatMessage("/setaccount show - List all available properties");
        }
    }
}
