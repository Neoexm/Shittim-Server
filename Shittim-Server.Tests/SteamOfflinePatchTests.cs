using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The reachability gate is the one site that decides whether the client gets as far as the title screen at all, so it is worth a test of its own even though the other six sites share the apply/revert path with it.
[Collection("steam-offline-patch")]
public class SteamOfflinePatchTests : IDisposable
{
    private const int Site = 0x400;

    private static readonly byte[] Precheck = Bytes("48 8B 15 E1 01 E1 0B 48 8B 0D AA 83 E5 0B E8 C5 88 C4 01 48 89 C7 31 C9 E8 8B 8A 10 09 85 C0 74 3D");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-steam-{Guid.NewGuid():N}");
    private readonly string _path;

    public SteamOfflinePatchTests()
    {
        Directory.CreateDirectory(_dir);

        var data = new byte[4096];
        Precheck.CopyTo(data, Site);

        _path = Path.Combine(_dir, "GameAssembly.dll");
        File.WriteAllBytes(_path, data);

        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_GAMEASSEMBLY_PATH", _path);
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_STEAM_OFFLINE", "true");
    }

    [Fact]
    public async Task TheTitleScreenReachabilityGateIsNoppedOut()
    {
        await new ClientSteamOfflinePatchService(new NullLogger()).StartAsync(CancellationToken.None);

        var data = File.ReadAllBytes(_path);
        Assert.Equal(new byte[] { 0x90, 0x90 }, data.AsSpan(Site + Precheck.Length - 2, 2).ToArray());
    }

    [Fact]
    public async Task TurningTheOfflinePatchOffPutsTheGateBack()
    {
        var before = File.ReadAllBytes(_path);

        var service = new ClientSteamOfflinePatchService(new NullLogger());
        await service.StartAsync(CancellationToken.None);
        Assert.NotEqual(before, File.ReadAllBytes(_path));

        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_STEAM_OFFLINE", "false");
        await new ClientSteamOfflinePatchService(new NullLogger()).StartAsync(CancellationToken.None);

        Assert.Equal(before, File.ReadAllBytes(_path));
    }

    private static byte[] Bytes(string hex) => hex.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray();

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_GAMEASSEMBLY_PATH", null);
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_STEAM_OFFLINE", null);

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class NullLogger : ILogger<ClientSteamOfflinePatchService>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
    }
}
