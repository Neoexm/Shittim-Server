using BlueArchiveAPI.Handlers;
using BlueArchiveAPI.Models;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

#nullable enable

HandlerManager.Initialize();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<BAContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BAContext")));

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure HTTPS endpoints with certificate
var certPath = Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path");
var keyPath = Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__KeyPath");

if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(keyPath) && 
    File.Exists(certPath) && File.Exists(keyPath))
{
    try
    {
        var certPem = File.ReadAllText(certPath);
        var keyPem = File.ReadAllText(keyPath);
        
        var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
        
        var exportableCert = new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
        
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Any, 5000, listenOptions =>
            {
                listenOptions.UseHttps(exportableCert);
            });
            options.Listen(System.Net.IPAddress.Any, 5100, listenOptions =>
            {
                listenOptions.UseHttps(exportableCert);
            });
        });
        Console.WriteLine($"✓ Using HTTPS on ports 5000 & 5100 (IPv4) with cert: {Path.GetFileName(certPath)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Failed to load certificate: {ex.Message}");
        Console.WriteLine("✓ Falling back to HTTP on ports 5000 & 5100");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Any, 5000); // HTTP API server
            options.Listen(System.Net.IPAddress.Any, 5100); // HTTP Gateway server
        });
    }
}
else
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Any, 5000); // HTTP API server
        options.Listen(System.Net.IPAddress.Any, 5100); // HTTP Gateway server
    });
    Console.WriteLine("✓ Using HTTP on ports 5000 & 5100 (IPv4, no SSL - cert not found)");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BlueArchiveAPI" }));
app.MapControllers();
app.UseAuthorization();

app.Run();
