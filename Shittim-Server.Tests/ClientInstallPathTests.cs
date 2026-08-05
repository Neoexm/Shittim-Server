using System.Text.RegularExpressions;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// There is one game directory, not nine file paths. Each client patch service still has its own override for the odd case where a single file lives somewhere strange, but the directory is what an operator sets, and everything else hangs off the locator.
public class ClientInstallPathTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-install-{Guid.NewGuid():N}");

    [Fact]
    public void TheDirectoryTheOperatorChoseIsWhereEveryClientFileIsLookedFor()
    {
        var metadata = Path.Combine(_dir, "BlueArchive_Data", "il2cpp_data", "Metadata");
        Directory.CreateDirectory(metadata);
        File.WriteAllText(Path.Combine(metadata, "global-metadata.dat"), "not a real client");
        File.WriteAllText(Path.Combine(_dir, "GameAssembly.dll"), "not a real client");

        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_INSTALL_DIR", _dir);

        Assert.Equal(Path.Combine(_dir, "GameAssembly.dll"), SteamGameLocator.FindGameFile("GameAssembly.dll"));
        Assert.Equal(
            Path.Combine(metadata, "global-metadata.dat"),
            SteamGameLocator.FindGameFile(Path.Combine("BlueArchive_Data", "il2cpp_data", "Metadata", "global-metadata.dat")));

        // The one that matters for A035: a file that does not exist yet still resolves under the chosen directory rather than wherever discovery would have gone.
        Assert.Equal(
            Path.Combine(_dir, "BlueArchive_Data", "Plugins", "x86_64", "grap64.dll"),
            SteamGameLocator.CombineGamePath(Path.Combine("BlueArchive_Data", "Plugins", "x86_64", "grap64.dll")));
    }

    [Fact]
    public void AChosenDirectoryBeatsWhateverDiscoveryFound()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_INSTALL_DIR", _dir);

        Assert.StartsWith(_dir, SteamGameLocator.CombineGamePath("GameAssembly.dll"));
    }

    // The client file the services resolve has to come from discovery or from something the operator set, never from a path baked into the source. A drive letter in here is someone else's machine by definition.
    [Fact]
    public void NoClientFileIsLookedForOnSomebodyElsesDrive()
    {
        var services = Path.Combine(RepoRoot(), "Shittim-Server", "Services");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(services, "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"[A-Za-z]:\\[A-Za-z0-9 _.()-]+\\"))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1} {lines[i].Trim()}");
            }
        }

        Assert.Empty(offenders);
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        return dir!;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_INSTALL_DIR", null);
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }
}
