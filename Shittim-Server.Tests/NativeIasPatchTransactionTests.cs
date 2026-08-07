using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// What has to hold after the server has been anywhere near a client binary: the file is either untouched or completely patched, the state on disk describes it either way, and a restore that does not land back on the original bytes says so instead of leaving the user with a client that sits on "Unpacking game resources" until Steam repairs it.
// Shares the module path environment variables with the ambiguity tests, and those are process-wide.
[Collection("native-ias-patch")]
public class NativeIasPatchTransactionTests : IDisposable
{
    private static readonly byte[] Ifinpay3 = [0xBA, 0x93, 0xB3, 0xCD, 0x2F, 0x48, 0x8D, 0x4D, 0x48, 0x48, 0x8B, 0xD8];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-txn-{Guid.NewGuid():N}");
    private readonly RecordingLogger _log = new();
    private readonly string _path;

    public NativeIasPatchTransactionTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "gamescale.core.dll");

        var data = new byte[4096];
        Array.Fill(data, (byte)0x90);
        Ifinpay3.CopyTo(data, 1000);
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
    public async Task BothSidesOfTheWriteAreRecorded()
    {
        var pristine = Sha(File.ReadAllBytes(_path));

        await Run();

        var state = ReadState();
        Assert.Equal(pristine, state.GetProperty("OriginalSha256").GetString());
        Assert.Equal(Sha(File.ReadAllBytes(_path)), state.GetProperty("PatchedSha256").GetString());
        Assert.NotEqual(pristine, state.GetProperty("PatchedSha256").GetString());
    }

    // The staging file is where the patched module is assembled, so a directory sitting on that name is a write that cannot start. Nothing may reach the module, and the state has to already be there - the ordering is the whole point.
    [Fact]
    public async Task AWriteThatCannotStartLeavesTheModuleAloneAndTheStateBehind()
    {
        var before = File.ReadAllBytes(_path);
        Directory.CreateDirectory(_path + ".shittim_tmp");

        await Run();

        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.Equal(Sha(before), ReadState().GetProperty("OriginalSha256").GetString());
        Assert.Contains(_log.Entries, x => x.Level == LogLevel.Error && x.Message.Contains("Failed to patch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PatchingTwiceIsPatchingOnce()
    {
        await Run();
        var once = File.ReadAllBytes(_path);
        var stateOnce = File.ReadAllText(StatePath());

        await Run();

        Assert.Equal(once, File.ReadAllBytes(_path));
        Assert.Equal(stateOnce, File.ReadAllText(StatePath()));
    }

    [Fact]
    public async Task RestoreReturnsTheExactOriginalBytes()
    {
        var before = File.ReadAllBytes(_path);

        var service = new ClientNativeIasPatchService(_log);
        await service.StartAsync(CancellationToken.None);
        Assert.NotEqual(before, File.ReadAllBytes(_path));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.False(File.Exists(StatePath()));
    }

    // Something else edited the module while the server held it patched.
    // Reverting our own offsets is then not enough to call the file official again, and claiming otherwise is what sends people to Steam's file verification with no idea why.
    [Fact]
    public async Task ARestoreThatDoesNotReachTheOriginalHashIsReported()
    {
        var service = new ClientNativeIasPatchService(_log);
        await service.StartAsync(CancellationToken.None);

        var tampered = File.ReadAllBytes(_path);
        tampered[2000] = 0xCC;
        File.WriteAllBytes(_path, tampered);

        await service.StopAsync(CancellationToken.None);

        var complaint = Assert.Single(_log.Entries, x => x.Message.Contains("did not restore to its original contents", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, complaint.Level);
        Assert.Contains(_path, complaint.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(StatePath()));
    }

    // gamescale routed at the loopback server while NexonPlatformModules still talks to nexon.com is a client that gets through sign-in and dies later somewhere that has nothing to do with the module that failed.
    [Fact]
    public async Task AModuleThatCannotBePatchedTakesTheOnesAlreadyPatchedBackWithIt()
    {
        var before = File.ReadAllBytes(_path);
        var nexon = Path.Combine(_dir, "NexonPlatformModules.dll");
        File.WriteAllBytes(nexon, before);
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_NEXON_PLATFORM_IAS", "true");
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_NEXON_PLATFORM_MODULES_PATH", nexon);

        using (File.Open(nexon, FileMode.Open, FileAccess.Read, FileShare.None))
            await Run();

        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.False(File.Exists(StatePath()));
        Assert.Contains(_log.Entries, x => x.Level == LogLevel.Error && x.Message.Contains("rolling back", StringComparison.Ordinal));
    }

    // State files written before the hashes existed are sitting beside the real client right now. The module they describe is the patched one, so there is no way to work out afterwards what it hashed to before - and recording what is in front of us would make restore verify our own bytes and call the client official.
    [Fact]
    public async Task AStateFileFromBeforeTheHashesCannotInventOne()
    {
        await Run();

        var old = JsonNode.Parse(File.ReadAllText(StatePath())).AsObject();
        old.Remove("OriginalSha256");
        old.Remove("PatchedSha256");
        File.WriteAllText(StatePath(), old.ToJsonString());

        await Run();

        Assert.Equal("", ReadState().GetProperty("OriginalSha256").GetString());
        Assert.NotEqual(Sha(File.ReadAllBytes(_path)), ReadState().GetProperty("OriginalSha256").GetString());
    }

    private async Task Run()
    {
        var service = new ClientNativeIasPatchService(_log);
        await service.StartAsync(CancellationToken.None);
    }

    private string StatePath() => _path + ".shittim_native_ias_patch.json";

    private JsonElement ReadState() => JsonDocument.Parse(File.ReadAllText(StatePath())).RootElement;

    private static string Sha(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

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
