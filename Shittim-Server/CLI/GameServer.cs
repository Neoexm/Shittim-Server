using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Schale.Data;
using Shittim.Commands;
using Shittim.Services;
using Shittim.Services.Client;
using Shittim.Services.IrcClient;
using Shittim.Services.WebClient;
using Shittim.Extensions;
using Shittim.Managers;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Shittim_Server.Managers;
using Shittim_Server.GameClient;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Serilog;
using AutoMapper;

namespace Shittim.CLI
{
    public class GameServer
    {
        public static async Task Main(bool update, bool console, long? id)
        {
            var config = ConfigLogger.LogConfiguration();

            Console.WriteLine("===========================================");
            Console.WriteLine("    Shittim Server - Blue Archive");
            Console.WriteLine("===========================================");

            Log.Information("Starting Game Server...");

            try
            {
                Config.Load();

                // Initialize Version State
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
                var resolverLogger = loggerFactory.CreateLogger<BlueArchiveVersionResolver>();
                using var httpClient = new HttpClient();
                var resolver = new BlueArchiveVersionResolver(httpClient, resolverLogger);

                var (versionId, cdnBaseUrl) = await resolver.GetOrUpdateVersionIdAsync(
                    Config.Instance.ServerConfiguration.OverrideVersionId,
                    Config.Instance.ServerConfiguration.OverrideCdnBaseUrl
                );

                BlueArchiveVersionState.Initialise(new BlueArchiveVersionState
                {
                    VersionId = versionId,
                    CdnBaseUrl = cdnBaseUrl
                });

                Console.WriteLine($"[Version Resolver] Initialized with VersionId: {versionId}");

                Console.WriteLine("\n[Command System] Loading commands...");
                CommandFactory.LoadCommands();
                Console.WriteLine("✓ Console commands loaded");

                Console.WriteLine("\n[Resource Manager] Checking Excel tables...");
                await ResourceService.LoadResources(Config.Instance.ServerConfiguration.UseCustomExcel);

                var builder = WebApplication.CreateBuilder(Environment.GetCommandLineArgs());

                builder.Configuration.AddConfiguration(config);
                builder.Host.UseSerilog();

                builder.Services.AddDbProvider();
                builder.Services.AddControllers();
                builder.Services.AddMemoryCache();
                builder.Services.AddAutoMapper(cfg => {}, typeof(Schale.MappingProfiles.GameModelsMappingProfile).Assembly);

                builder.Services.AddProtocolHandlers();
                builder.Services.AddMemorySessionKeyService();
                builder.Services.AddExcelTableService();
                builder.Services.AddWebService();
                builder.Services.AddIrcService();
                builder.Services.AddHexaMapService();
                builder.Services.AddSharedDataCache();

                builder.Services.AddGameClient();
                builder.Services.AddManagers();
                builder.Services.AddHandlers();

                builder.Services.AddSingleton<CafeService>();
                builder.Services.AddSingleton<HandlerManager>();

                builder.Services.AddHostedService<Shittim_Server.GameClient.GameClientService>();

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("ShittimGM", policy =>
                    {
                        policy
                            .WithOrigins("http://localhost:3000", "https://tauri.localhost")
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    });
                });

                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                builder.Configuration["Kestrel:Certificates:Default:Path"] = null;
                builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = null;

                X509Certificate2? httpsCert = null;
                var certPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "certs", "selfsigned_cert.pem");
                var keyPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "certs", "selfsigned_key.pem");

                if (File.Exists(certPath) && File.Exists(keyPath))
                {
                    try
                    {
                        var certPem = File.ReadAllText(certPath);
                        var keyPem = File.ReadAllText(keyPath);
                        var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
                        httpsCert = new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
                        Console.WriteLine($"✓ Loaded certificate for HTTPS: {Path.GetFileName(certPath)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Failed to load certificate: {ex.Message}");
                    }
                }

                builder.WebHost.ConfigureKestrel(options =>
                {
                    if (httpsCert != null)
                    {
                        options.Listen(System.Net.IPAddress.Any, 443, listenOptions =>
                        {
                            listenOptions.UseHttps(httpsCert);
                        });
                        Console.WriteLine("✓ HTTPS on port 443 for SDK endpoints");
                    }
                    else
                    {
                        Console.WriteLine("✗ HTTPS on port 443 disabled (no certificate)");
                    }
                    
                    options.Listen(System.Net.IPAddress.Any, 5000);
                    options.Listen(System.Net.IPAddress.Any, 5100);
                });
                Console.WriteLine("✓ HTTP on ports 5000 (API) & 5100 (Gateway)");

                var app = builder.Build();

                app.InitializeProtocolHandlers();

                using (var scope = app.Services.CreateScope())
                {
                    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchaleDataContext>>();
                    var excelService = scope.ServiceProvider.GetRequiredService<ExcelTableService>();
                    var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

                    using var context = await contextFactory.CreateDbContextAsync();
                    if (!context.Database.CanConnect())
                        context.Database.EnsureCreated();

                    if (context.Database.GetPendingMigrations().Any())
                        context.Database.Migrate();

                    await context.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS ShopFreeRecruitHistories (
                            ServerId INTEGER PRIMARY KEY AUTOINCREMENT,
                            AccountServerId INTEGER NOT NULL,
                            UniqueId INTEGER NOT NULL,
                            RecruitCount INTEGER NOT NULL,
                            LastUpdateDate TEXT NOT NULL,
                            FOREIGN KEY(AccountServerId) REFERENCES Accounts(ServerId)
                        )");

                    var parcelHandler = scope.ServiceProvider.GetRequiredService<ParcelHandler>();
                    AccountInitializationService.Initialize(excelService, parcelHandler);

                    var handlerManager = scope.ServiceProvider.GetRequiredService<HandlerManager>();
                    handlerManager.Initialize();

                    if (console)
                    {
                        var consoleConnection = new ConsoleClientConnection(
                            contextFactory,
                            mapper,
                            excelService,
                            new StreamWriter(Console.OpenStandardOutput()),
                            id ?? 2
                        );
                        _ = Task.Run(() => ConsoleCommand.ConsoleCommandListener(consoleConnection));
                    }
                }

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseAuthorization();
                app.UseSerilogRequestLogging();

                app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BlueArchiveAPI" }));
                app.MapControllers();
                app.UseCors("ShittimGM");

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "An unhandled exception occurred during runtime");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
