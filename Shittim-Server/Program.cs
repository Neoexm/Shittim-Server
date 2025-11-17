using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;
using Schale.Data;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Shittim_Server.Managers;

#nullable enable

Console.WriteLine("===========================================");
Console.WriteLine("    Shittim Server - Blue Archive");
Console.WriteLine("===========================================");

// Create a temporary logger for startup
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var startupLogger = loggerFactory.CreateLogger("Startup");

// Load Excel tables from CDN
Console.WriteLine("\n[Resource Manager] Checking Excel tables...");
await ResourceService.LoadResources(Config.Instance.ServerConfiguration.UseCustomExcel);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContextFactory<SchaleDataContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BAContext"), 
        b => b.MigrationsAssembly("Shittim-Server")));

builder.Services.AddAutoMapper(typeof(Schale.MappingProfiles.GameModelsMappingProfile));

builder.Services.AddProtocolHandlers();

builder.Services.AddSingleton<ExcelTableService>();
builder.Services.AddSingleton<SessionKeyService>();
builder.Services.AddSingleton<CafeService>();
builder.Services.AddSingleton<HandlerManager>();
builder.Services.AddSingleton<SharedDataCacheService>();
builder.Services.AddSingleton<HexaMapService>();
builder.Services.AddScoped<ParcelHandler>();
builder.Services.AddScoped<CampaignManager>();
builder.Services.AddScoped<CharacterManager>();
builder.Services.AddScoped<CharacterGM>();
builder.Services.AddScoped<MailManager>();
builder.Services.AddScoped<EquipmentManager>();
builder.Services.AddScoped<GearManager>();
builder.Services.AddScoped<ShopManager>();
builder.Services.AddScoped<CafeManager>();
builder.Services.AddScoped<ConsumeHandler>();
builder.Services.AddScoped<SchoolDungeonManager>();
builder.Services.AddScoped<WeekDungeonManager>();
builder.Services.AddScoped<WorldRaidManager>();
builder.Services.AddScoped<TimeAttackDungeonManager>();
builder.Services.AddScoped<EventContentCampaignManager>();
builder.Services.AddScoped<ConcentrateCampaignManager>();
builder.Services.AddScoped<RaidManager>();
builder.Services.AddScoped<EliminateRaidManager>();
builder.Services.AddScoped<EchelonManager>();
builder.Services.AddScoped<ScenarioManager>();
builder.Services.AddScoped<ItemManager>();
builder.Services.AddScoped<AronaService>();
builder.Services.AddScoped<AronaAI>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.WriteIndented = false;
        // options.JsonSerializerOptions.Converters.Add(new BlueArchiveAPI.Core.ProtocolFirstJsonConverterFactory());
        // Add enum converter for enum VALUES (not dictionary keys - those need special handling)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DISABLE default certificate loading from appsettings to avoid crashes
builder.Configuration["Kestrel:Certificates:Default:Path"] = null;
builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = null;

// Load self-signed certificate for HTTPS on port 443
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

// Configure endpoints: 443 (HTTPS SDK), 5000 (HTTP API), 5100 (HTTP Gateway)
builder.WebHost.ConfigureKestrel(options =>
{
    // Port 443: HTTPS for SDK endpoints (getCountry, enterToy, etc.)
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
    
    // Port 5000: HTTP for API endpoints
    options.Listen(System.Net.IPAddress.Any, 5000);
    
    // Port 5100: HTTP for Gateway endpoints
    options.Listen(System.Net.IPAddress.Any, 5100);
});
Console.WriteLine("✓ HTTP on ports 5000 (API) & 5100 (Gateway)");

var app = builder.Build();

app.InitializeProtocolHandlers();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<SchaleDataContext>>();
        using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
        Console.WriteLine("✓ Database initialized and migrated");
        
        var excelService = services.GetRequiredService<ExcelTableService>();
        var parcelHandler = services.GetRequiredService<ParcelHandler>();
        AccountInitializationService.Initialize(excelService, parcelHandler);
        Console.WriteLine("✓ Services initialized with ExcelTableService and ParcelHandler");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Database initialization failed: {ex.Message}");
    }
}

// Initialize HandlerManager after DI container is built
try
{
    var handlerManager = app.Services.GetRequiredService<HandlerManager>();
    handlerManager.Initialize();
    Console.WriteLine("✓ Handler manager initialized");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Handler manager initialization failed: {ex.Message}");
}

// AronaAI is now scoped and will be initialized per-request when needed

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ADD REQUEST LOGGING MIDDLEWARE TO SEE ALL REQUESTS
app.Use(async (context, next) =>
{
    var timestamp = DateTime.Now.ToString("HH:mm:ss");
    Console.WriteLine($"[{timestamp}] {context.Request.Method} {context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}");
    await next();
    Console.WriteLine($"[{timestamp}] -> Response: {context.Response.StatusCode}");
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BlueArchiveAPI" }));
app.MapControllers();
app.UseAuthorization();

app.Run();
