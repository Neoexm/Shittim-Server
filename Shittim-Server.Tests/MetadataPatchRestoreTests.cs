using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// global-metadata.dat carrying our gateway key is what the official client chokes on: it reads the file on the way in and sits on "Unpacking game resources" forever, and the only way out anyone finds is Steam's file verification.
// Turning the patch off has to be enough to get the client back.
public class MetadataPatchRestoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-metadata-{Guid.NewGuid():N}");
    private readonly RecordingLogger _log = new();
    private readonly string _path;
    private readonly byte[] _official;

    public MetadataPatchRestoreTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "global-metadata.dat");

        // Taken off the service rather than copied, so the fixture cannot drift away from the key it scans for.
        var officialPem = (string)typeof(ClientMetadataPatchService)
            .GetField("OfficialGatewayPublicKeyPem", BindingFlags.NonPublic | BindingFlags.Static)
            .GetRawConstantValue();

        var key = Encoding.ASCII.GetBytes(officialPem.Replace("\r\n", "\n").TrimEnd('\n'));
        var lengths = (int[])typeof(ClientMetadataPatchService)
            .GetField("ChunkLengths", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var data = new byte[8192];
        Array.Fill(data, (byte)0x41);
        // The client stores the key as three separate runs, and they are not adjacent.
        var at = 0;
        foreach (var (offset, length) in new[] { 512, 3000, 6400 }.Zip(lengths))
        {
            key.AsSpan(at, length).CopyTo(data.AsSpan(offset));
            at += length;
        }

        File.WriteAllBytes(_path, data);
        _official = data;

        using var rsa = RSA.Create(4096);
        Environment.SetEnvironmentVariable("SHITTIM_GATEWAY_RSA_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem());
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_METADATA_PATH", _path);
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_METADATA", "true");
    }

    [Fact]
    public async Task TurningThePatchOffPutsTheOfficialKeyBack()
    {
        await Run();
        Assert.NotEqual(_official, File.ReadAllBytes(_path));

        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_METADATA", "false");
        await Run();

        Assert.Equal(_official, File.ReadAllBytes(_path));
        Assert.False(File.Exists(_path + ".shittim_patch.json"));
    }

    // A server that was killed rather than shut down leaves the state file behind with no instance holding it. Starting again with the patch off is the only chance to notice, so it has to be taken from disk, not from memory.
    [Fact]
    public async Task ARestoreAfterAKilledServerStillWorks()
    {
        await Run();

        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_METADATA", "false");
        var fresh = new ClientMetadataPatchService(_log);
        await fresh.StartAsync(CancellationToken.None);

        Assert.Equal(_official, File.ReadAllBytes(_path));
    }

    [Fact]
    public async Task ShutdownPutsTheOfficialKeyBack()
    {
        var service = new ClientMetadataPatchService(_log);
        await service.StartAsync(CancellationToken.None);
        Assert.NotEqual(_official, File.ReadAllBytes(_path));

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(_official, File.ReadAllBytes(_path));
    }

    private async Task Run()
    {
        var service = new ClientMetadataPatchService(_log);
        await service.StartAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        foreach (var name in new[] { "SHITTIM_GATEWAY_RSA_PUBLIC_KEY", "SHITTIM_CLIENT_METADATA_PATH", "SHITTIM_AUTO_PATCH_METADATA" })
            Environment.SetEnvironmentVariable(name, null);

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class RecordingLogger : ILogger<ClientMetadataPatchService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
