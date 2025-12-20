namespace Shittim_Server.GameClient
{
    public static class ClientExtensions
    {
        public static void AddGameClient(this IServiceCollection services)
        {
            services.AddSingleton<SchaleAI>();
        }
    }
}
