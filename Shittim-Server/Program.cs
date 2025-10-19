using BlueArchiveAPI.Handlers;
using BlueArchiveAPI.Models;
using BlueArchiveAPI.Services;
using BlueArchiveAPI.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

#nullable enable

HandlerManager.Initialize();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<BAContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BAContext")));

builder.Services.AddScoped<AccountService>();
builder.Services.AddSingleton<HarLoggingService>();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddControllers();
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Database initialization failed: {ex.Message}");
    }
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

// Add HAR logging middleware
app.UseMiddleware<HarLoggingMiddleware>();

app.UseResponseCompression();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BlueArchiveAPI" }));
app.MapControllers();
app.UseAuthorization();

app.Run();
