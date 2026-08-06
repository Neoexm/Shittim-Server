using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// nxinface.enconfig.json is the client's own config, not something the server owns, so the only acceptable outcome of a failed patch is that it still says what it said.
// Patching it without having recorded what it used to say leaves the user with a file nothing can put back.
public class InfaceConfigPatchStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-inface-{Guid.NewGuid():N}");
    private readonly RecordingLogger _log = new();
    private readonly string _configPath;
    private readonly string _original;

    public InfaceConfigPatchStateTests()
    {
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "nxinface.enconfig.json");
        _original = Encode("{\"NXI_DIRS\":[\"C:\\\\Game\\\\Plugins\"],\"NXI_LOG\":\"info\"}");
        File.WriteAllText(_configPath, _original, Encoding.UTF8);

        Environment.SetEnvironmentVariable("SHITTIM_AUTO_PATCH_INFACE_CONFIG", "true");
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_INFACE_CONFIG_PATH", _configPath);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_PLUGIN_DIRECTORY", Path.Combine(_dir, "plugins"));
    }

    // A directory on the state file's name is a save that cannot happen. The config must survive it untouched, which it only can if the state is written first.
    [Fact]
    public async Task AConfigIsNeverPatchedWithoutSomewhereToRecordWhatItWas()
    {
        Directory.CreateDirectory(_configPath + ".shittim_inface_config_patch.json");

        await Run();

        Assert.Equal(_original, File.ReadAllText(_configPath, Encoding.UTF8));
        Assert.Contains(_log.Entries, x => x.Level == LogLevel.Error);
    }

    [Fact]
    public async Task TheOriginalIsPutBackOnShutdown()
    {
        var service = new ClientInfaceConfigPatchService(_log);
        await service.StartAsync(CancellationToken.None);
        Assert.NotEqual(_original, File.ReadAllText(_configPath, Encoding.UTF8));

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(_original, File.ReadAllText(_configPath, Encoding.UTF8));
    }

    [Fact]
    public async Task TheRecordedOriginalSurvivesASecondPatch()
    {
        await Run();
        await Run();

        var state = JsonDocument.Parse(File.ReadAllText(_configPath + ".shittim_inface_config_patch.json")).RootElement;
        Assert.Equal(_original, state.GetProperty("OriginalRaw").GetString());
    }

    private static string Encode(string json)
        => string.Join("&", Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).ToCharArray());

    private async Task Run()
    {
        var service = new ClientInfaceConfigPatchService(_log);
        await service.StartAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        foreach (var name in new[] { "SHITTIM_AUTO_PATCH_INFACE_CONFIG", "SHITTIM_CLIENT_INFACE_CONFIG_PATH", "SHITTIM_CLIENT_PLUGIN_DIRECTORY" })
            Environment.SetEnvironmentVariable(name, null);

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private sealed class RecordingLogger : ILogger<ClientInfaceConfigPatchService>
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
