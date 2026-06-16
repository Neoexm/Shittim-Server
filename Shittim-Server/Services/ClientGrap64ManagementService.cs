using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    /// <summary>
    /// Replaces the real grap64.dll (Nexon Game Shield / NGS) with a lightweight stub on server start,
    /// then restores the original on stop. The stub provides fake NGSX exports so the game loads
    /// without initiating real NGS kernel-level connections that would bypass mitmproxy interception.
    /// </summary>
    public class ClientGrap64ManagementService : IHostedService
    {
        private readonly ILogger<ClientGrap64ManagementService> logger;
        private string resolvedPath;
        private bool didSwap;

        public ClientGrap64ManagementService(ILogger<ClientGrap64ManagementService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!IsEnabled())
                {
                    logger.LogInformation("grap64.dll management disabled");
                    return Task.CompletedTask;
                }

                resolvedPath = GetGrap64Path();
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    logger.LogInformation("grap64.dll path not configured and not found at default locations; skipping management");
                    return Task.CompletedTask;
                }

                var stubPath = GetStubPath();
                if (string.IsNullOrWhiteSpace(stubPath) || !File.Exists(stubPath))
                {
                    logger.LogWarning("grap64.dll stub not found at {StubPath}; skipping management", stubPath);
                    return Task.CompletedTask;
                }

                var bakPath = resolvedPath + ".bak";

                // Detect if stub is already deployed
                if (!File.Exists(resolvedPath) && File.Exists(bakPath))
                {
                    logger.LogInformation("grap64.dll already swapped to stub (real is at {BakPath})", bakPath);
                    // Re-deploy stub in case it was removed
                    File.Copy(stubPath, resolvedPath, overwrite: false);
                    logger.LogInformation("Re-deployed grap64.dll stub: {Path}", resolvedPath);
                    didSwap = true;
                    return Task.CompletedTask;
                }

                if (File.Exists(resolvedPath) && File.Exists(bakPath))
                {
                    // Both exist — check if current dll is already the stub (small size)
                    var currentSize = new FileInfo(resolvedPath).Length;
                    var stubSize = new FileInfo(stubPath).Length;
                    if (currentSize == stubSize)
                    {
                        logger.LogInformation("grap64.dll is already the stub ({Size} bytes); real is backed up", currentSize);
                        didSwap = true;
                        return Task.CompletedTask;
                    }

                    logger.LogWarning(
                        "Both grap64.dll ({Size} bytes) and .bak exist; overwriting grap64.dll with stub",
                        currentSize);
                    File.Copy(stubPath, resolvedPath, overwrite: true);
                    didSwap = true;
                    logger.LogInformation("Deployed grap64.dll stub (overwrote existing, real preserved in .bak)");
                    return Task.CompletedTask;
                }

                if (!File.Exists(resolvedPath))
                {
                    // No real dll, no bak — just deploy stub
                    File.Copy(stubPath, resolvedPath);
                    didSwap = true;
                    logger.LogInformation("Deployed grap64.dll stub (no original found): {Path}", resolvedPath);
                    return Task.CompletedTask;
                }

                // Normal case: real grap64.dll exists, no .bak
                File.Move(resolvedPath, bakPath);
                File.Copy(stubPath, resolvedPath);
                didSwap = true;
                logger.LogInformation(
                    "Swapped grap64.dll: real → {BakPath}, stub → {Path}",
                    bakPath, resolvedPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to manage grap64.dll");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!didSwap || string.IsNullOrWhiteSpace(resolvedPath))
                return Task.CompletedTask;

            try
            {
                var bakPath = resolvedPath + ".bak";

                // Remove the stub
                if (File.Exists(resolvedPath))
                {
                    File.Delete(resolvedPath);
                    logger.LogInformation("Removed grap64.dll stub: {Path}", resolvedPath);
                }

                // Restore the real dll if backed up
                if (File.Exists(bakPath))
                {
                    File.Move(bakPath, resolvedPath);
                    logger.LogInformation("Restored real grap64.dll from backup: {Path}", resolvedPath);
                }
                else
                {
                    logger.LogInformation("No grap64.dll.bak to restore (stub was deployed without backing up original)");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore grap64.dll");
            }

            return Task.CompletedTask;
        }

        private static bool IsEnabled()
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_AUTO_MANAGE_GRAP64");
            if (!string.IsNullOrWhiteSpace(env) && bool.TryParse(env, out var enabled))
                return enabled;

            return Config.Instance.ServerConfiguration.AutoManageGrap64;
        }

        private static string GetGrap64Path()
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_GRAP64_PATH");
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            var configured = Config.Instance.ServerConfiguration.ClientGrap64Path;
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var pluginDir = Config.Instance.ServerConfiguration.ClientPluginDirectory;
            if (!string.IsNullOrWhiteSpace(pluginDir))
            {
                var candidate = Path.Combine(pluginDir, "grap64.dll");
                if (File.Exists(candidate) || File.Exists(candidate + ".bak"))
                    return candidate;
            }

            var candidates = new[]
            {
                @"F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive_Data\Plugins\x86_64\grap64.dll",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Steam", "steamapps", "common", "BlueArchive",
                    "BlueArchive_Data", "Plugins", "x86_64", "grap64.dll")
            };

            return candidates.FirstOrDefault(p => File.Exists(p) || File.Exists(p + ".bak")) ?? "";
        }

        private static string GetStubPath()
        {
            var env = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_GRAP64_STUB_PATH");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                return env;

            // Look relative to the server binary
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "grap64_stub.dll"),
                Path.Combine(baseDir, "grap64.dll"),
                Path.Combine(baseDir, "..", "..", "Scripts", "grap64.dll"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Scripts", "grap64.dll")),
                @"C:\Users\tomda\Documents\Shittim-Server\Scripts\grap64.dll"
            };

            return candidates.FirstOrDefault(File.Exists) ?? "";
        }
    }
}
