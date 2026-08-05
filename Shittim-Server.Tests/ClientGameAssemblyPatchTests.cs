using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The signature shipped in ClientGameAssemblyIasPatchService is one fixed byte string lifted from a single client build, and it carries a rip-relative displacement inside the il2cpp class-init check as literal bytes, so it can only ever match the build it came from. Against the client installed here (225,592,304 bytes, sha256 a369337f81..) it matches nothing - not the exact pattern, not the pattern with the displacement wildcarded, not even the bare prologue - while FetchPrimaryLinkResult and HasPrimaryLink are both still present in global-metadata.dat. So the patch is stale rather than gone, and the only thing the operator ever saw was one warning line followed later by the client refusing to authenticate.
public class ClientGameAssemblyPatchTests : IDisposable
{
    private static readonly byte[] Signature =
    [
        0x55, 0x56, 0x57, 0x48, 0x83, 0xEC, 0x70, 0x48, 0x8D, 0x6C, 0x24, 0x70, 0x48, 0xC7, 0x45, 0xF0,
        0xFE, 0xFF, 0xFF, 0xFF, 0x48, 0x89, 0xCE, 0x80, 0x3D, 0x4E, 0x64, 0x4D, 0x03, 0x00, 0x75, 0x43
    ];

    private static readonly byte[] Patched = [0xB0, 0x01, 0xC3, 0x90, 0x90];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-ga-{Guid.NewGuid():N}");
    private readonly RecordingLogger _log = new();

    public ClientGameAssemblyPatchTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_GAMEASSEMBLY_IAS", "true");
    }

    [Fact]
    public async Task ABinaryNoSignatureMatchesIsLeftExactlyAsItWas()
    {
        var path = WriteFakeAssembly(Filler(8192));
        var before = File.ReadAllBytes(path);

        await Start(path);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists($"{path}.shittim_ias_patch.json"));
    }

    // A patch the operator explicitly turned on and that then matched nothing is a failure, not a note. It used to be one LogWarning naming the path, which is the whole of what anyone had to work with before the client started refusing to authenticate; the size and hash are what makes a report of this diagnosable at all, since the only thing that varies is which build they have.
    [Fact]
    public async Task AnEnabledPatchThatMatchesNothingSaysSoAndIdentifiesTheBuild()
    {
        var path = WriteFakeAssembly(Filler(8192));

        await Start(path);

        var complaint = Assert.Single(_log.Entries, x => x.Message.Contains("signature", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(LogLevel.Error, complaint.Level);
        Assert.Contains("8192", complaint.Message);
        Assert.Contains("authenticat", complaint.Message, StringComparison.OrdinalIgnoreCase);

        // sha256 of 8192 bytes of the filler below, so the line carries something a build can actually be identified by.
        Assert.Contains("D0F32F31694148E389AC5746526DEF5A2E09770256EF60D6EED861EC0570CFF7", complaint.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AMatchingBinaryIsPatchedAndPutBackByteForByteOnShutdown()
    {
        var body = Filler(4096).Concat(Signature).Concat(Filler(512)).ToArray();
        var path = WriteFakeAssembly(body);

        var service = await Start(path);

        Assert.Equal(Patched, File.ReadAllBytes(path).Skip(4096).Take(Patched.Length));
        Assert.True(File.Exists($"{path}.shittim_ias_patch.json"));

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(body, File.ReadAllBytes(path));
        Assert.False(File.Exists($"{path}.shittim_ias_patch.json"));
    }

    private async Task<ClientGameAssemblyIasPatchService> Start(string path)
    {
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_GAMEASSEMBLY_PATH", path);
        var service = new ClientGameAssemblyIasPatchService(_log);
        await service.StartAsync(CancellationToken.None);
        return service;
    }

    private string WriteFakeAssembly(byte[] body)
    {
        var path = Path.Combine(_dir, "GameAssembly.dll");
        File.WriteAllBytes(path, body);
        return path;
    }

    // Deterministic and free of the signature: every byte is above 0x80 except the low nibble walk, and the run never
    // reproduces the 55 56 57 prologue.
    private static byte[] Filler(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
            bytes[i] = (byte)(0x90 + (i % 7));
        return bytes;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_GAMEASSEMBLY_IAS", null);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_GAMEASSEMBLY_PATH", null);
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class RecordingLogger : ILogger<ClientGameAssemblyIasPatchService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
