namespace Shittim_Server.Services
{
    public static class HandlerExtension
    {
        public static void AddHandlers(this IServiceCollection services)
        {
            services.AddSingleton<ParcelHandler>();
            services.AddSingleton<ParcelRefresher>();
            services.AddSingleton<ConsumeHandler>();
        }
    }
}
