using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Configuration;
using Schale.Data;

namespace Shittim.Extensions
{
    /// <summary>
    /// Extension methods for configuring database services based on SQL provider configuration.
    /// Supports SQLite3 and SQLServer with automatic connection string validation.
    /// </summary>
    public static class DBServiceExtensions
    {
        public static Exception NoConnectionStringException = new ArgumentNullException($"ConnectionString in appsettings is missing");

        /// <summary>
        /// Configures the database context factory based on the SQL provider specified in server configuration.
        /// Automatically enables lazy loading proxies and retry logic for SQLServer.
        /// </summary>
        /// <param name="services">The service collection to add the database provider to.</param>
        /// <exception cref="ArgumentNullException">Thrown when connection string is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when SQL provider is not supported.</exception>
        public static void AddDbProvider(this IServiceCollection services)
        {
            var sqlProvider = Config.Instance.ServerConfiguration.SQLProvider;
            var sqlConnectionString = Config.Instance.ServerConfiguration.SQLConnectionString;
            if (string.IsNullOrEmpty(sqlConnectionString)) throw NoConnectionStringException;
            switch (sqlProvider)
            {
                case "SQLite3":
                    services.AddDbContextFactory<SchaleDataContext>(opt =>
                        opt.UseSqlite(sqlConnectionString).UseLazyLoadingProxies());
                    break;
                case "SQLServer":
                    services.AddDbContextFactory<SchaleDataContext>(opt =>
                        opt.UseSqlServer(sqlConnectionString, actions =>
                            actions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null))
                            .UseLazyLoadingProxies());
                    break;
                default:
                    throw new ArgumentException($"SQL Provider '{sqlProvider}' is not valid");
            }
        }
    }
}
