using System.Reflection;

namespace Shittim_Server.Core;

public static class ProtocolHandlerExtensions
{
    public static IServiceCollection AddProtocolHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IProtocolHandlerRegistry, ProtocolHandlerRegistry>();

        var handlerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ProtocolHandlerBase)));

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }

        return services;
    }

    public static IApplicationBuilder InitializeProtocolHandlers(this IApplicationBuilder app)
    {
        var registry = app.ApplicationServices.GetRequiredService<IProtocolHandlerRegistry>();
        
        var handlerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ProtocolHandlerBase)));

        foreach (var handlerType in handlerTypes)
        {
            registry.RegisterHandlerType(handlerType);
        }

        return app;
    }
}
