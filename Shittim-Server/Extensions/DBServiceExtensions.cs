using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.Data;

namespace Shittim.Extensions
{
    public static class DBServiceExtensions
    {
        public static Exception NoConnectionStringException = new ArgumentNullException($"ConnectionString in appsettings is missing");

        public static void AddDbProvider(this IServiceCollection services)
        {
            SqliteProvider.EnsureInitialized();

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
