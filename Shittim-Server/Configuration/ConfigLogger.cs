using Serilog;
using Serilog.Events;

namespace BlueArchiveAPI.Configuration
{
    public class ConfigLogger
    {
        public static IConfigurationRoot LogConfiguration()
        {
            var baseDir = AppContext.BaseDirectory;
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(baseDir)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                .Build();

            {
                var logFilePath = Path.Combine(
                    Path.GetDirectoryName(baseDir)!,
                    "logs",
                    "log.txt"
                );

                if (File.Exists(logFilePath))
                {
                    var prevLogFilePath = Path.Combine(
                        Path.GetDirectoryName(logFilePath)!,
                        "log-prev.txt"
                    );
                    if (File.Exists(prevLogFilePath))
                        File.Delete(prevLogFilePath);

                    File.Move(logFilePath, prevLogFilePath);
                }

                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(
                        logFilePath,
                        restrictedToMinimumLevel: LogEventLevel.Verbose,
                        shared: true
                    )
                    .ReadFrom.Configuration(config)
                    .CreateLogger();
            }

            return config;
        }
    }
}
