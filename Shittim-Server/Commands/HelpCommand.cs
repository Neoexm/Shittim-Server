using Shittim.Services.Client;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Shittim.Commands
{

    [CommandHandler("help", "Show this help.", "/help [command]")]
    internal class HelpCommand : Command
    {
        public HelpCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }
        
        [Argument(0, @"^[a-zA-Z]+$", "The command to display the help message", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string CommandName { get; set; } = string.Empty;

        public override async Task Execute()
        {   
            if (CommandName != string.Empty)
            {
                if (CommandFactory.commands.TryGetValue(CommandName, out Type? value))
                {
                    var cmdAtr = (CommandHandlerAttribute?)Attribute.GetCustomAttribute(value, typeof(CommandHandlerAttribute));
                    Command? cmd = CommandFactory.CreateCommand(CommandName, connection, args, false);

                    if (cmd is not null)
                    {
                        await connection.SendChatMessage($"{CommandName} - {cmdAtr.Hint} (Usage: {cmdAtr.Usage})");

                        List<PropertyInfo> argsProperties = cmd.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.GetCustomAttribute(typeof(ArgumentAttribute)) is not null).ToList();
                        
                        foreach (var argProp in argsProperties)
                        {
                            ArgumentAttribute attr = (ArgumentAttribute)argProp.GetCustomAttribute(typeof(ArgumentAttribute))!;
                            var arg = Regex.Replace(attr.Pattern.ToString(), @"[\^\$\+]", "");

                            await connection.SendChatMessage($"<{arg}> - {attr.Description}");
                        }
                    }
                } else
                {
                    throw new ArgumentException("Invalid Argument.");
                }

                return;
            }

            foreach (var command in CommandFactory.commands.Keys)
            {
                var cmdAtr = (CommandHandlerAttribute?)Attribute.GetCustomAttribute(CommandFactory.commands[command], typeof(CommandHandlerAttribute));

                Command? cmd = CommandFactory.CreateCommand(command, connection, args, false);

                if (cmd is not null)
                {
                    await connection.SendChatMessage($"{command} - {cmdAtr.Hint} (Usage: {cmdAtr.Usage})");
                }
            }
            
            await connection.SendChatMessage("/{command} help - To get more information about that command");
        }
    }

}
