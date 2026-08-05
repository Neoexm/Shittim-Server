using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Shittim_Server.Tests;

// "forgot to change ver number" is a real commit in this history. The server assemblies, the Control Center package and the release tag all used to be free to disagree, so this pins them to Directory.Build.props and fails when one of them is bumped alone.
public class VersionConsistencyTests
{
    [Fact]
    public void TheControlCentreShipsTheSameVersionAsTheServer()
    {
        var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "ShittimControlCenter", "package.json")));

        Assert.Equal(DeclaredVersion(), package.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void TheBuiltServerAssemblyCarriesTheDeclaredVersion()
    {
        var informational = typeof(BlueArchiveAPI.Services.ExcelTableService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        // The SourceLink build appends +<commit>, so compare the version part only.
        Assert.Equal(DeclaredVersion(), informational.Split('+')[0]);
    }

    [Fact]
    public void TheReleaseWorkflowTagsFromTheDeclaredVersionRatherThanTheBuildDate()
    {
        var workflow = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "build-and-release.yml"));

        Assert.Contains("Directory.Build.props", workflow);
        Assert.DoesNotContain("Get-Date -Format 'yyyy.MM.dd'", workflow);
    }

    // Every step the release workflow runs has to exist in the tree. The workflow spent its whole life calling PyInstaller on a shittim_console.py that is not in the repository, so it could never have produced a release.
    [Fact]
    public void EveryScriptTheReleaseWorkflowInvokesExists()
    {
        var workflow = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "build-and-release.yml"));
        var missing = new List<string>();

        foreach (Match m in Regex.Matches(workflow, @"[.\\/]*([A-Za-z0-9_\-./\\]+\.(?:py|ps1|csproj|sln))"))
        {
            var rel = m.Groups[1].Value.Replace('\\', '/').TrimStart('.', '/');
            if (!File.Exists(Path.Combine(RepoRoot(), rel)))
                missing.Add(rel);
        }

        Assert.Empty(missing);
    }

    // Nothing pinned the SDK, so the same tree built against whatever was installed. dotnet resolves global.json before it loads a project, so a machine without a matching SDK stops with "A compatible .NET SDK was not found" instead of failing later inside a compile.
    [Fact]
    public void TheSdkIsPinnedToTheFrameworkTheProjectsTarget()
    {
        var pin = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "global.json")))
            .RootElement.GetProperty("sdk");

        Assert.StartsWith("10.0.", pin.GetProperty("version").GetString());
        Assert.Equal("latestFeature", pin.GetProperty("rollForward").GetString());

        // Schale used to sit on net8.0 while the server that consumes it was on net10.0. MemoryDumper is standalone RE scratch that nothing references and nothing ships, so it is not covered here.
        foreach (var project in new[] { "Shittim-Server", "Schale", "Shittim-Server.Tests" })
            Assert.Contains("<TargetFramework>net10.0</TargetFramework>", File.ReadAllText(Path.Combine(RepoRoot(), project, project + ".csproj")));
    }

    private static string DeclaredVersion()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        return Regex.Match(props, @"<Version>([^<]+)</Version>").Groups[1].Value;
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        return dir!;
    }
}
