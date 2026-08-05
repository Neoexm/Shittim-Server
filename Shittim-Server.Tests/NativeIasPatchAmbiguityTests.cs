using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// A byte pattern that occurs twice in a module is not a patch target, it is two candidates, and the code that
// picks one is guessing. gamescale's own definitions say as much - the ifinpay suppressions are anchored on
// surrounding instructions because the bare `mov edx,0` they write is common enough to hit unrelated code.
[Collection("native-ias-patch")]
public class NativeIasPatchAmbiguityTests : IDisposable
{
    // ifinpay-checkprecond-enter-suppress-3: mov edx,802010003 / lea rcx,[rbp+48h] / mov rbx,rax
    private static readonly byte[] Ifinpay3 = [0xBA, 0x93, 0xB3, 0xCD, 0x2F, 0x48, 0x8D, 0x4D, 0x48, 0x48, 0x8B, 0xD8];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-native-{Guid.NewGuid():N}");
    private readonly RecordingLogger _log = new();

    public NativeIasPatchAmbiguityTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("SHITTIM_IAS_PATCH_PORT", "5000");
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_GAMESCALE_IAS", "true");
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_NEXON_PLATFORM_IAS", "false");
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_INFACE_IAS", "false");
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_NEXON_PLATFORM_MODULES_PATH", Path.Combine(_dir, "absent-nexon.dll"));
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_INFACE_PATH", Path.Combine(_dir, "absent-inface.dll"));
    }

    [Fact]
    public async Task APatternThatOccursTwiceIsNotPatchedAtEither()
    {
        var path = WriteModule(1000, 3000);
        var before = File.ReadAllBytes(path);

        await Run();

        Assert.Equal(before, File.ReadAllBytes(path));

        var complaint = Assert.Single(_log.Entries, x => x.Message.Contains("ifinpay-checkprecond-enter-suppress-3", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, complaint.Level);
        Assert.Contains("2", complaint.Message);
    }

    // The control. Without it the case above passes just as well against a service that patches nothing at all.
    [Fact]
    public async Task TheSamePatternOccurringOnceIsPatched()
    {
        var path = WriteModule(1000);

        await Run();

        var data = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xBA, 0x00, 0x00, 0x00, 0x00 }, data[1000..1005]);
        Assert.Equal(Ifinpay3[5..], data[1005..1012]);
    }

    [Fact]
    public async Task AnAmbiguousModuleLeavesNoPatchStateBehind()
    {
        var path = WriteModule(1000, 3000);

        await Run();

        Assert.False(File.Exists(path + ".shittim_native_ias_patch.json"));
    }

    private string WriteModule(params int[] offsets)
    {
        var data = new byte[4096];
        Array.Fill(data, (byte)0x90);
        foreach (var offset in offsets)
            Ifinpay3.CopyTo(data, offset);

        var path = Path.Combine(_dir, "gamescale.core.dll");
        File.WriteAllBytes(path, data);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_GAMESCALE_CORE_PATH", path);
        return path;
    }

    private async Task Run()
    {
        var service = new ClientNativeIasPatchService(_log);
        await service.StartAsync(CancellationToken.None);
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
