using System.Diagnostics;
using System.Text.RegularExpressions;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    // Locates the Steam install of the client (app 3557620, common folder "BlueArchive") wherever it lives.
    // Checking only F:\ and Program Files (x86) misses anyone with Steam or their game library on another drive,
    // and with no install path the patch services never write the gateway public key into global-metadata.dat, leaving the client stuck at "Unpacking game resources" on a 50001 handshake that cannot complete.
    //
    // Cheapest first: SHITTIM_STEAM_PATH, the two Program Files locations, <drive>:\Steam and <drive>:\Program Files (x86)\Steam on each fixed drive, the conventional Linux roots under the user's home, then the HKCU/HKLM Valve\Steam value (spawns reg.exe, so only if nothing else matched).
    // Each install found gets its steamapps\libraryfolders.vdf parsed for extra libraries - the common "games on D:" case - and a final fixed-drive scan catches bare SteamLibrary folders no vdf mentions.
    public static class SteamGameLocator
    {
        private const string GameFolderName = "BlueArchive";

        // Resolved once per process; the install location does not move while the
        // server runs, and several hosted services query it during startup.
        private static readonly Lazy<string> InstallRootLazy = new(ResolveInstallRoot);

        // Absolute path to the Blue Archive install directory, or "" if not found.
        public static string InstallRoot => InstallRootLazy.Value;

        // <InstallRoot>/<relative> if the install was found and the file exists,
        // otherwise null. Use for files that should already be on disk.
        public static string? FindGameFile(string relative)
        {
            var full = CombineGamePath(relative);
            return full != null && File.Exists(full) ? full : null;
        }

        // <InstallRoot>/<relative> if the install was found (no existence check),
        // otherwise null. Use for files/dirs that may be created later.
        public static string? CombineGamePath(string relative)
        {
            var root = ConfiguredInstallRoot();
            if (string.IsNullOrWhiteSpace(root))
                root = InstallRoot;

            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, relative);
        }

        // An explicit choice beats discovery, and it is read per call rather than through InstallRoot's Lazy so that
        // pointing the server at a different install is one setting instead of nine.
        private static string ConfiguredInstallRoot()
        {
            var fromEnv = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_INSTALL_DIR");
            return string.IsNullOrWhiteSpace(fromEnv) ? Config.Instance.ServerConfiguration.ClientInstallDirectory : fromEnv;
        }

        private static string ResolveInstallRoot()
        {
            try
            {
                foreach (var library in EnumerateSteamLibraries())
                {
                    var candidate = Path.Combine(library, "steamapps", "common", GameFolderName);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
                // Discovery is best-effort; the patch services fall back to their
                // own legacy candidates and log when nothing is found.
            }

            return "";
        }

        // Every Steam library root on the machine (a folder that contains a
        // steamapps directory), de-duplicated, lazily so the registry probe is only
        // reached when the standard locations turn up nothing.
        private static IEnumerable<string> EnumerateSteamLibraries()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var steam in EnumerateSteamInstalls())
            {
                var normalized = NormalizeDir(steam);
                if (seen.Add(normalized))
                    yield return normalized;

                foreach (var library in ParseLibraryFolders(steam))
                {
                    if (seen.Add(library))
                        yield return library;
                }
            }

            foreach (var library in ScanDrivesForLibraries())
            {
                if (seen.Add(library))
                    yield return library;
            }
        }

        // Candidate Steam install directories (the ones that may hold
        // steamapps\libraryfolders.vdf). Steam itself is almost always in Program
        // Files (x86) even when games live on another drive.
        private static IEnumerable<string> EnumerateSteamInstalls()
        {
            var overridePath = Environment.GetEnvironmentVariable("SHITTIM_STEAM_PATH");
            if (Directory.Exists(overridePath))
                yield return overridePath!;

            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf86))
            {
                var steam = Path.Combine(pf86, "Steam");
                if (Directory.Exists(steam))
                    yield return steam;
            }

            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(pf))
            {
                var steam = Path.Combine(pf, "Steam");
                if (Directory.Exists(steam))
                    yield return steam;
            }

            foreach (var drive in FixedDriveRoots())
            {
                var direct = Path.Combine(drive, "Steam");
                if (Directory.Exists(direct))
                    yield return direct;

                var nested = Path.Combine(drive, "Program Files (x86)", "Steam");
                if (Directory.Exists(nested))
                    yield return nested;
            }

            foreach (var root in UnixSteamRoots(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
            {
                if (Directory.Exists(root))
                    yield return root;
            }

            // Only reached when no Steam install was found above - spawns reg.exe.
            var fromRegistry = ReadSteamPathFromRegistry();
            if (Directory.Exists(fromRegistry))
                yield return fromRegistry;
        }

        // Nothing above resolves on Linux: both Program Files folders come back empty, there is no registry, and Steam lives under the user's home, so a Proton install is only reachable through these.
        // ~/.steam/steam is normally a symlink into ~/.local/share/Steam, ~/.steam/root the same again on older installs, and the flatpak build keeps a separate tree under ~/.var/app.
        public static IEnumerable<string> UnixSteamRoots(string home)
        {
            if (string.IsNullOrEmpty(home))
                yield break;

            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".steam", "root");
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        }

        // Parse steamapps\libraryfolders.vdf (and the legacy config\ location) for every library "path". Handles the modern keyed format
        //   "0" { "path" "D:\\SteamLibrary" ... }
        // and the pre-2021 flat format
        //   "1" "D:\\SteamLibrary"
        private static IEnumerable<string> ParseLibraryFolders(string steamInstall)
        {
            var vdfCandidates = new[]
            {
                Path.Combine(steamInstall, "steamapps", "libraryfolders.vdf"),
                Path.Combine(steamInstall, "config", "libraryfolders.vdf"),
            };

            foreach (var vdf in vdfCandidates)
            {
                string text;
                try
                {
                    if (!File.Exists(vdf))
                        continue;
                    text = File.ReadAllText(vdf);
                }
                catch
                {
                    continue;
                }

                foreach (Match match in Regex.Matches(text, "\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\""))
                {
                    // vdf escapes backslashes as \\ ; collapse to a real path. The
                    // app-id -> size numeric entries inside modern "apps" blocks also
                    // match the [0-9]+ key, but their pure-number values fail the
                    // separator check and Directory.Exists below.
                    var raw = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (!raw.Contains('\\') && !raw.Contains('/'))
                        continue;

                    if (Directory.Exists(raw))
                        yield return NormalizeDir(raw);
                }
            }
        }

        // Bare Steam library folders by convention name, for installs not referenced
        // by any parsed libraryfolders.vdf.
        private static IEnumerable<string> ScanDrivesForLibraries()
        {
            string[] relativeNames =
            [
                "SteamLibrary",
                "Steam",
                "SteamGames",
                Path.Combine("Games", "SteamLibrary"),
                Path.Combine("Games", "Steam"),
            ];

            foreach (var drive in FixedDriveRoots())
            {
                foreach (var name in relativeNames)
                {
                    var library = Path.Combine(drive, name);
                    if (Directory.Exists(Path.Combine(library, "steamapps")))
                        yield return NormalizeDir(library);
                }
            }
        }

        private static IEnumerable<string> FixedDriveRoots()
        {
            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch
            {
                yield break;
            }

            foreach (var drive in drives)
            {
                bool usable;
                try
                {
                    usable = drive.DriveType == DriveType.Fixed && drive.IsReady;
                }
                catch
                {
                    usable = false;
                }

                if (usable)
                    yield return drive.RootDirectory.FullName;
            }
        }

        // Read Steam's install path from the registry without taking a dependency on
        // Microsoft.Win32.Registry (unavailable on the plain net10.0 TFM) by shelling
        // out to reg.exe. Best-effort: returns "" on any failure.
        private static string ReadSteamPathFromRegistry()
        {
            (string Hive, string Key, string Value)[] sources =
            [
                ("HKCU", @"Software\Valve\Steam", "SteamPath"),
                ("HKLM", @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                ("HKLM", @"SOFTWARE\Valve\Steam", "InstallPath"),
            ];

            foreach (var (hive, key, value) in sources)
            {
                try
                {
                    var psi = new ProcessStartInfo("reg.exe", $"query \"{hive}\\{key}\" /v {value}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                        continue;

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);

                    // A matching line looks like:
                    //   SteamPath    REG_SZ    c:/program files (x86)/steam
                    foreach (var line in output.Split('\n'))
                    {
                        var marker = line.IndexOf("REG_SZ", StringComparison.OrdinalIgnoreCase);
                        if (marker < 0)
                            continue;

                        var path = line[(marker + "REG_SZ".Length)..].Trim();
                        if (!string.IsNullOrWhiteSpace(path))
                            return path.Replace('/', '\\');
                    }
                }
                catch
                {
                    // try the next source
                }
            }

            return "";
        }

        private static string NormalizeDir(string path)
        {
            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch
            {
                return path;
            }
        }
    }
}
