namespace Shittim.CLI
{
    public class Arguments
    {
        public static async Task RunServerAsync(
            bool update = false,
            bool console = false,
            long? id = null
        )
        {
            await GameServer.Main(update, console, id);
        }
    }
}
