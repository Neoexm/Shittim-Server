using BlueArchiveAPI.Handlers;
using BlueArchiveAPI.Models;
using BlueArchiveAPI.Services;
using BlueArchiveAPI.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

#nullable enable

Console.WriteLine("===========================================");
Console.WriteLine("    Shittim Server - Blue Archive");
Console.WriteLine("===========================================");

// Create a temporary logger for startup
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var startupLogger = loggerFactory.CreateLogger("Startup");

// Load Excel tables from CDN
Console.WriteLine("\n[Resource Manager] Checking Excel tables...");
await ResourceManagerSimple.LoadResources(startupLogger);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<BAContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BAContext")));

builder.Services.AddExcelTableService();
builder.Services.AddExcelSqlService();
builder.Services.AddSessionKeyService();
builder.Services.AddCafeService();
builder.Services.AddCurrencyService();
builder.Services.AddSingleton<HandlerManager>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;  // Keep PascalCase for Atrahasis compatibility
        options.JsonSerializerOptions.WriteIndented = false;
        options.JsonSerializerOptions.Converters.Add(new BlueArchiveAPI.Core.ProtocolFirstJsonConverterFactory());
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

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<BAContext>();
        context.Database.Migrate();
        Console.WriteLine("✓ Database initialized and migrated");
        
        // Initialize AccountInitializationService with ExcelTableService and ExcelSqlService
        var excelService = services.GetRequiredService<ExcelTableService>();
        var excelSqlService = services.GetRequiredService<IExcelSqlService>();
        AccountInitializationService.Initialize(excelService, excelSqlService);
        Console.WriteLine("✓ Account initialization service configured");
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
