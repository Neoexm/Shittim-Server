using Shittim.Commands;
using Serilog;

namespace Shittim.Services.Client
{
    public class ConsoleCommand
    {
        public static async Task ConsoleCommandListener(ConsoleClientConnection connection)
        {
            Log.Information("Starting console command with UID: {uid}", connection.AccountServerId);
            while (true)
            {
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || connection == null)
                    continue;

                if (input.StartsWith('/'))
                {
                    try
                    {
                        var args = input.Split(' ');
                        var commandName = args[0].TrimStart('/').ToLower();
                        var commandArgs = args.Skip(1).ToArray();

                        var command = CommandFactory.CreateCommand(commandName, connection, commandArgs);
                        command?.Execute();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing command: {ex.Message}");
                    }
                }
            }
        }
    }
}
