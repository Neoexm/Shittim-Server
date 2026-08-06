using System.Text;
using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// Both stamp base builders sit next to the sandbox and pre spellings of the same URL, and the live route is a prefix of live01, so a definition that is even slightly too loose eats a neighbour.
// The offsets below keep the real spacing gamescale.core.dll has so the slot lengths are tested against the layout they were measured from.
[Collection("native-ias-patch")]
public class StampBaseUrlPatchTests : IDisposable
{
    private const int HostLive01 = 0x100;
    private const int HostPre = 0x128;
    private const int HostLive = 0x148;
    private const int FullLive01 = 0x200;
    private const int FullPre = 0x230;
    private const int FullLive = 0x258;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-stamp-{Guid.NewGuid():N}");
    private readonly RecordingLogger _log = new();
    private readonly string _path;

    public StampBaseUrlPatchTests()
    {
        Directory.CreateDirectory(_dir);

        var data = new byte[4096];
        Put(data, HostLive01, "public.api.nexon.com/stamp/live01");
        Put(data, HostPre, "public.api.nexon.com/stamp/pre");
        Put(data, HostLive, "public.api.nexon.com/stamp/live");
        Put(data, FullLive01, "https://public.api.nexon.com/stamp/live01");
        Put(data, FullPre, "https://public.api.nexon.com/stamp/pre");
        Put(data, FullLive, "https://public.api.nexon.com/stamp/live");

        _path = Path.Combine(_dir, "gamescale.core.dll");
        File.WriteAllBytes(_path, data);

        Environment.SetEnvironmentVariable("SHITTIM_IAS_PATCH_PORT", "5000");
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_GAMESCALE_IAS", "true");
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_NEXON_PLATFORM_IAS", "false");
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_INFACE_IAS", "false");
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_GAMESCALE_CORE_PATH", _path);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_NEXON_PLATFORM_MODULES_PATH", Path.Combine(_dir, "absent-nexon.dll"));
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_INFACE_PATH", Path.Combine(_dir, "absent-inface.dll"));
    }

    [Fact]
    public async Task BothLiveStampBasesEndUpOnTheLoopbackServer()
    {
        await new ClientNativeIasPatchService(_log).StartAsync(CancellationToken.None);

        var data = File.ReadAllBytes(_path);
        Assert.Equal("http://127.0.0.1:5000/stamp/live", Read(data, FullLive));
        Assert.Equal("127.0.0.1:5000/stamp/live", Read(data, HostLive));
    }

    [Fact]
    public async Task ThePreEnvironmentBasesAreLeftAlone()
    {
        await new ClientNativeIasPatchService(_log).StartAsync(CancellationToken.None);

        var data = File.ReadAllBytes(_path);
        Assert.Equal("https://public.api.nexon.com/stamp/pre", Read(data, FullPre));
        Assert.Equal("public.api.nexon.com/stamp/pre", Read(data, HostPre));
    }

    [Fact]
    public async Task DisablingThePatchPutsNexonBack()
    {
        var before = File.ReadAllBytes(_path);

        var service = new ClientNativeIasPatchService(_log);
        await service.StartAsync(CancellationToken.None);
        Assert.NotEqual(before, File.ReadAllBytes(_path));

        await service.StopAsync(CancellationToken.None);
        Assert.Equal(before, File.ReadAllBytes(_path));
    }

    private static void Put(byte[] data, int offset, string value) => Encoding.ASCII.GetBytes(value).CopyTo(data, offset);

    private static string Read(byte[] data, int offset)
    {
        var end = Array.IndexOf(data, (byte)0, offset);
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }

    public void Dispose()
    {
        foreach (var name in new[]
        {
            "SHITTIM_IAS_PATCH_PORT", "SHITTIM_AUTO_PATCH_GAMESCALE_IAS", "SHITTIM_AUTO_PATCH_NEXON_PLATFORM_IAS",
            "SHITTIM_AUTO_PATCH_INFACE_IAS", "SHITTIM_CLIENT_GAMESCALE_CORE_PATH",
            "SHITTIM_CLIENT_NEXON_PLATFORM_MODULES_PATH", "SHITTIM_CLIENT_INFACE_PATH"
        })
            Environment.SetEnvironmentVariable(name, null);

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class RecordingLogger : ILogger<ClientNativeIasPatchService>
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
