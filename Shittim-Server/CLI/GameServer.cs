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
using Shittim.Utils;
using Serilog;
using AutoMapper;

namespace Shittim.CLI
{
    public class GameServer
    {
        public static async Task Main(bool update, bool console, long? id)
        {
            // Prevent console freezes: disables QuickEdit mode on Windows and replaces Console.Out with a non-blocking async writer so that pipe buffer saturation can never block request threads.
            ConsoleHelper.Harden();

            var config = ConfigLogger.LogConfiguration();

            Console.WriteLine("===========================================");
            Console.WriteLine("    Shittim Server - Blue Archive");
            Console.WriteLine("===========================================");

            Log.Information("Starting Game Server...");

            try
            {
                Config.Load();
                Shittim_Server.Services.ServerNoticeService.Load();
                Shittim_Server.Services.EventScheduleService.Load();
                Shittim_Server.Services.WorldRaidService.Load();

                Shittim_Server.Core.Diagnostics.GatewayWireLog.Configure(
                    Config.Instance.ServerConfiguration.PacketLogging.WireDump);
                if (Shittim_Server.Core.Diagnostics.GatewayWireLog.Enabled)
                    Log.Information("Gateway wire dump is Enabled (logs/wire-{Date:yyyy-MM-dd}.txt)", DateTime.Now);

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
                Console.WriteLine("Console commands loaded");

                Console.WriteLine("\n[Resource Manager] Checking Excel tables...");
                await ResourceService.LoadResources(Config.Instance.ServerConfiguration.UseCustomExcel);

                // Fail fast with a clear message if the ExcelDB SQLCipher key cannot decrypt the ExcelDB.db for this client version (the key rotates between some game updates).
                ExcelTableService.ValidateExcelDbKey();

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

                builder.Services.AddHostedService<ClientMetadataPatchService>();
                builder.Services.AddHostedService<ClientSteamOfflinePatchService>();
                builder.Services.AddHostedService<ClientGrap64ManagementService>();
                builder.Services.AddHostedService<ClientInfaceConfigPatchService>();
                builder.Services.AddHostedService<ClientNativeIasPatchService>();
                builder.Services.AddHostedService<ClientExcelBannerPatchService>();
                builder.Services.AddHostedService<ClientRegionLabelPatchService>();
                builder.Services.AddHostedService<ClientStoreUrlPatchService>();
                builder.Services.AddHostedService<WorldRaidSyncService>();
                builder.Services.AddGameClient();
                builder.Services.AddManagers();
                builder.Services.AddHandlers();

                builder.Services.AddSingleton<CafeService>();
                builder.Services.AddSingleton<HandlerManager>();
                builder.Services.AddSingleton<CustomCharacterService>();
                builder.Services.AddSingleton<ModCatalogService>();

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
                        Console.WriteLine($"Loaded certificate for HTTPS: {Path.GetFileName(certPath)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load certificate: {ex.Message}");
                    }
                }

                var apiPort = ParsePort(Config.Instance.ServerConfiguration.HostPort, 5000, nameof(Config.Instance.ServerConfiguration.HostPort));
                var gatewayPort = ParsePort(Config.Instance.ServerConfiguration.GatewayPort, 5100, nameof(Config.Instance.ServerConfiguration.GatewayPort));

                builder.WebHost.ConfigureKestrel(options =>
                {
                    if (httpsCert != null)
                    {
                        options.Listen(System.Net.IPAddress.Any, 443, listenOptions =>
                        {
                            listenOptions.UseHttps(httpsCert);
                        });
                        Console.WriteLine("HTTPS on port 443 for SDK endpoints");
                    }
                    else
                    {
                        Console.WriteLine("HTTPS on port 443 disabled (no certificate)");
                    }
                    
                    options.Listen(System.Net.IPAddress.Any, apiPort);
                    if (gatewayPort != apiPort)
                        options.Listen(System.Net.IPAddress.Any, gatewayPort);
                });
                Console.WriteLine($"HTTP on ports {apiPort} (API) & {gatewayPort} (Gateway)");

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

                    // EnsureCreated only builds the schema for a brand-new database and does nothing to one that already exists, and the project has no migrations.
                    // SchemaReconciler derives both tables and columns from the EF model, so adding a property is enough - hand-written CREATE TABLE IF NOT EXISTS blocks would cover new tables but silently miss new columns.
                    var schemaChanges = await SchemaReconciler.ReconcileAsync(context);
                    foreach (var change in schemaChanges)
                        Console.WriteLine($"Schema: {change}");

                    var parcelHandler = scope.ServiceProvider.GetRequiredService<ParcelHandler>();
                    AccountInitializationService.Initialize(excelService, parcelHandler);

                    // A client update replaces ExcelDB.db and takes the forced dates with it, so re-apply whatever the operator left switched on. The cached world raid manifest rides the same pass.
                    if (Shittim_Server.Services.EventScheduleService.Configured || Shittim_Server.Services.WorldRaidService.Manifest != null)
                    {
                        try { Shittim_Server.Services.EventScheduleService.Apply(excelService); }
                        catch (Exception ex) { Log.Warning(ex, "Could not re-apply the event schedule override"); }
                    }

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
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path.Value?.Contains("/ias", StringComparison.OrdinalIgnoreCase) == true)
                        Log.Information("[IAS Raw] {Method} {Scheme}://{Host}{Path}{QueryString}", context.Request.Method, context.Request.Scheme, context.Request.Host, context.Request.Path, context.Request.QueryString);

                    await next();
                });

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

        private static int ParsePort(string value, int fallback, string configName)
        {
            if (int.TryParse(value, out var port) && port > 0 && port <= ushort.MaxValue)
                return port;

            Log.Warning("Invalid {ConfigName} value {PortValue}; using {FallbackPort}", configName, value, fallback);
            return fallback;
        }
    }
}
