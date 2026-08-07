using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// Nothing in the Windows probe order resolves under Proton: Environment.SpecialFolder.ProgramFiles and ProgramFilesX86 both come back empty on Linux, there is no registry to read, and the install lives under the user's home. InstallRoot was therefore "" for every Linux user, and each client patch service fell through to its hardcoded F:\SteamLibrary candidate and found nothing to patch.
//
// InstallRoot itself is a static Lazy resolved once per process, so driving the whole resolver from a test would fix the answer for everything that ran after it. These go at the candidate list instead.
public class SteamGameLocatorTests
{
    [Fact]
    public void TheUnixRootsCoverWhereSteamActuallyInstallsItself()
    {
        var roots = SteamGameLocator.UnixSteamRoots("/home/sensei")
            .Select(x => x.Replace('\\', '/'))
            .ToList();

        Assert.Contains("/home/sensei/.steam/steam", roots);
        Assert.Contains("/home/sensei/.local/share/Steam", roots);
        Assert.Contains("/home/sensei/.var/app/com.valvesoftware.Steam/.local/share/Steam", roots);
    }

    [Fact]
    public void AHomeDirectoryWeWereNeverToldAboutYieldsNothing()
    {
        Assert.Empty(SteamGameLocator.UnixSteamRoots(""));
    }

    // The join the resolver performs on each candidate root, against a library laid out the way Steam lays one out.
    [Fact]
    public void AnInstallUnderTheUsersHomeIsReachableFromTheseRoots()
    {
        var home = Path.Combine(Path.GetTempPath(), $"shittim-home-{Guid.NewGuid():N}");
        var install = Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "BlueArchive");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "GameAssembly.dll"), "not a real client");

        try
        {
            var found = SteamGameLocator.UnixSteamRoots(home)
                .Where(Directory.Exists)
                .Select(root => Path.Combine(root, "steamapps", "common", "BlueArchive", "GameAssembly.dll"))
                .FirstOrDefault(File.Exists);

            Assert.NotNull(found);
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }
}
