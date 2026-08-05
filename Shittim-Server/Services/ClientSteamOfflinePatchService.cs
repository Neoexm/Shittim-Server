using System.Text.Json;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientSteamOfflinePatchService : IHostedService
    {
        // NPA.Ex.Steam.ExternalPlatformSteam is where the login flow reaches Steamworks, and the two auth sites are inside its GetAuthToken. Each signature sits clear of the bytes it patches, so it still matches once applied and one lookup serves both apply and revert.
        // Steam itself still has to be running - offline mode is fine, but with SteamAPI_Init never called the SDK version reads back as 0 and NXPSteamHelper.ThrowIfPlatformNotSupported kills the prologue coroutine long before login.
        private static readonly SteamOfflinePatch[] Patches =
        [
            // BLoggedOn wants a live connection, and offline it returns false, which lands on the branch that builds a failed result object.
            new(
                "steam.GetAuthToken.bypassBLoggedOn",
                Hex("48 8B 15 67 D6 1B 03 48 89 F1 E8 77 82 FE F8 48 89 C6 31 C9"),
                20,
                Hex("E8 AD 35 01 00 84 C0"),
                Hex("90 90 90 90 90 31 C0")),

            // that branch sets Code 70010006 and Message "GetAuthToken Failed - SteamUser() is offline.", so zero the code and move both stores off Message (+18) onto AuthToken (+20). Message is only read back when Code is non-zero, and inface hands AuthToken straight on as the external ticket without looking at it. Reusing that literal rather than a nicer one is deliberate: il2cpp pins string literals per method and it is the only one this method already pins.
            new(
                "steam.GetAuthToken.tokenFromMessageLiteral",
                Hex("48 8B 0D 8B ED 0C 03 E8 96 ED BA F6 48 85 C0 0F 84 8B 00 00 00 48 89 C7 48 89 C1 31 D2 E8 E0 FE EB FF"),
                34,
                Hex("C7 47 10 96 44 2C 04 48 8B 15 E2 73 20 03 48 89 F9 48 83 C1 18 48 89 57 18"),
                Hex("C7 47 10 00 00 00 00 48 8B 15 E2 73 20 03 48 89 F9 48 83 C1 20 48 89 57 20")),

            // the cash shop will not draw until Steam quotes prices, and RequestPrices needs a live connection, so offline the callback reports failure and GetPurchasableProduct answers 70012005 with no products at all. skip the failure branch and let it carry on to LoadItemDefinitions.
            new(
                "steam.GetPurchasableProduct.bypassPriceFailure",
                Hex("48 8B 0D D7 C3 0C 03 83 B9 E0 00 00 00 00 75 05 E8 19 47 B9 F6 48 89 F9 31 D2 E8 CF F7 E9 FF 84 DB"),
                33,
                Hex("74 69"),
                Hex("90 90")),

            // GetItemDefinitionIDs then fails the same way (70012001), so jump straight to the callback invoke. it sits after Products has been allocated but before anything that needs Steam, so the shop gets an empty dictionary and a zero Code - the tabs draw and the entries have no price. falling through into the item loop instead would reach Double.Parse on a price string Steam never filled in.
            new(
                "steam.GetPurchasableProduct.emptyItemDefinitions",
                Hex("0F 84 C3 03 00 00 4C 8D 44 24 44 31 D2 45 31 C9 E8 4B C2 FF FF 84 C0"),
                23,
                Hex("0F 84 4C 03 00 00"),
                Hex("E9 6F 03 00 00 90")),

            // GetEntitlementsAsJsonArray reaches RequestPrices through a second display class of its own, so with only GetPurchasableProduct patched the shop still throws a 70012005 notice over the lobby.
            new(
                "steam.GetEntitlementsAsJsonArray.bypassPriceFailure",
                Hex("48 8B 0D F7 B1 0C 03 83 B9 E0 00 00 00 00 75 05 E8 39 35 B9 F6 48 89 F9 31 D2 E8 EF E5 E9 FF 84 DB"),
                33,
                Hex("74 69"),
                Hex("90 90")),

            // that leaves 70013004 from the GetAllItems leg: OnSteamInventoryResultReady only builds the details list when the Steam callback reports k_EResultOK, so offline it stays null. every failure path still carries Array.Empty in r12, so taking the branch unconditionally yields an empty list rather than null and the entitlements result comes back with a zero Code.
            new(
                "steam.OnSteamInventoryResultReady.detailsWhenResultFailed",
                Hex("49 83 C7 10 49 C7 46 10 00 00 00 00 4C 89 F9 31 D2 E8 AD 02 BB F6 83 FB 01"),
                25,
                Hex("75 38"),
                Hex("90 90")),

            // none of the above is reached with the adapter itself down rather than Steam merely put into offline mode: Application.internetReachability reads NotReachable and UIPatchDownload's enter-game precheck opens popup_message_network_error over the title screen before a single request goes out. it is the only read of internetReachability in the whole assembly, and loopback stays up with every adapter disabled, so dropping the branch lets the flow carry on to the server config it fetches from us.
            new(
                "patchDownload.PreCheckForEnterGame.ignoreUnreachable",
                Hex("48 8B 15 E1 01 E1 0B 48 8B 0D AA 83 E5 0B E8 C5 88 C4 01 48 89 C7 31 C9 E8 8B 8A 10 09 85 C0"),
                31,
                Hex("74 3D"),
                Hex("90 90"))
        ];

        private readonly ILogger<ClientSteamOfflinePatchService> logger;
        private string gameAssemblyPath;

        public ClientSteamOfflinePatchService(ILogger<ClientSteamOfflinePatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                gameAssemblyPath = GetGameAssemblyPath();

                if (string.IsNullOrWhiteSpace(gameAssemblyPath) || !File.Exists(gameAssemblyPath))
                {
                    logger.LogWarning("GameAssembly.dll not found for the Steam offline patch: {GameAssemblyPath}", gameAssemblyPath);
                    return Task.CompletedTask;
                }

                if (!IsEnabled())
                {
                    Revert();
                    logger.LogInformation("Steam offline patch disabled");
                    return Task.CompletedTask;
                }

                Apply();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply the Steam offline patch");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                Revert();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to revert the Steam offline patch");
            }

            return Task.CompletedTask;
        }

        private void Apply()
        {
            var data = File.ReadAllBytes(gameAssemblyPath);
            var applied = new List<SteamOfflinePatchEntry>();
            var pending = new List<SteamOfflinePatchEntry>();

            foreach (var patch in Patches)
            {
                var offset = Locate(data, patch);
                if (offset < 0)
                    continue;

                var entry = new SteamOfflinePatchEntry
                {
                    Name = patch.Name,
                    Offset = offset,
                    Original = Convert.ToBase64String(patch.Original),
                    Patched = Convert.ToBase64String(patch.Patched)
                };

                applied.Add(entry);

                if (!data.AsSpan((int)offset, patch.Patched.Length).SequenceEqual(patch.Patched))
                    pending.Add(entry);
            }

            if (applied.Count == 0)
            {
                logger.LogWarning("No Steam offline patch signatures matched: {GameAssemblyPath}", gameAssemblyPath);
                return;
            }

            if (pending.Count > 0)
            {
                using var stream = File.Open(gameAssemblyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                foreach (var entry in pending)
                {
                    var patched = Convert.FromBase64String(entry.Patched);
                    stream.Position = entry.Offset;
                    stream.Write(patched, 0, patched.Length);
                }

                stream.Flush(true);
            }

            File.WriteAllText(GetStatePath(), JsonSerializer.Serialize(new SteamOfflinePatchState
            {
                GameAssemblyPath = gameAssemblyPath,
                Patches = applied
            }, JsonOptions));

            if (pending.Count == 0)
                logger.LogInformation("Steam offline patch already applied at {SiteCount} sites: {GameAssemblyPath}", applied.Count, gameAssemblyPath);
            else
                logger.LogInformation("Applied the Steam offline patch at {SiteCount} sites: {GameAssemblyPath}", pending.Count, gameAssemblyPath);
        }

        private void Revert()
        {
            if (!File.Exists(gameAssemblyPath))
                return;

            var data = File.ReadAllBytes(gameAssemblyPath);
            var restore = new List<(long Offset, byte[] Original)>();

            foreach (var patch in Patches)
            {
                var offset = Locate(data, patch);
                if (offset < 0)
                    continue;

                if (data.AsSpan((int)offset, patch.Patched.Length).SequenceEqual(patch.Patched))
                    restore.Add((offset, patch.Original));
            }

            if (restore.Count > 0)
            {
                using var stream = File.Open(gameAssemblyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                foreach (var (offset, original) in restore)
                {
                    stream.Position = offset;
                    stream.Write(original, 0, original.Length);
                }

                stream.Flush(true);
                logger.LogInformation("Reverted the Steam offline patch at {SiteCount} sites: {GameAssemblyPath}", restore.Count, gameAssemblyPath);
            }

            var statePath = GetStatePath();
            if (File.Exists(statePath))
                File.Delete(statePath);
        }

        private long Locate(byte[] data, SteamOfflinePatch patch)
        {
            var matches = FindAll(data, patch.Signature);
            if (matches.Count != 1)
            {
                logger.LogWarning("Steam offline patch signature {PatchName} matched {MatchCount} locations", patch.Name, matches.Count);
                return -1;
            }

            var offset = matches[0] + patch.PatchOffset;
            var current = data.AsSpan((int)offset, patch.Original.Length);

            if (!current.SequenceEqual(patch.Original) && !current.SequenceEqual(patch.Patched))
            {
                logger.LogWarning("Steam offline patch target bytes did not match {PatchName}", patch.Name);
                return -1;
            }

            return offset;
        }

        private static List<long> FindAll(byte[] data, byte[] pattern)
        {
            var matches = new List<long>();
            var offset = 0;

            while (offset <= data.Length - pattern.Length)
            {
                var index = data.AsSpan(offset).IndexOf(pattern);
                if (index < 0)
                    break;

                matches.Add(offset + index);
                offset += index + 1;
            }

            return matches;
        }

        private static bool IsEnabled()
        {
            var value = Environment.GetEnvironmentVariable("SHITTIM_AUTO_PATCH_STEAM_OFFLINE");
            return bool.TryParse(value, out var enabled)
                ? enabled
                : Config.Instance.ServerConfiguration.AutoPatchClientSteamOffline;
        }

        private static string GetGameAssemblyPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_GAMEASSEMBLY_PATH");
            if (string.IsNullOrWhiteSpace(configuredPath))
                configuredPath = Config.Instance.ServerConfiguration.ClientGameAssemblyPath;

            if (!string.IsNullOrWhiteSpace(configuredPath))
                return Path.IsPathRooted(configuredPath) ? configuredPath : Path.GetFullPath(configuredPath);

            return SteamGameLocator.FindGameFile("GameAssembly.dll") ?? "";
        }

        private string GetStatePath()
        {
            return $"{gameAssemblyPath}.shittim_steam_offline_patch.json";
        }

        private static byte[] Hex(string hex)
        {
            return hex
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Convert.ToByte(x, 16))
                .ToArray();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private sealed record SteamOfflinePatch(string Name, byte[] Signature, int PatchOffset, byte[] Original, byte[] Patched);

        private sealed class SteamOfflinePatchState
        {
            public string GameAssemblyPath { get; set; } = "";
            public List<SteamOfflinePatchEntry> Patches { get; set; } = [];
        }

        private sealed class SteamOfflinePatchEntry
        {
            public string Name { get; set; } = "";
            public long Offset { get; set; }
            public string Original { get; set; } = "";
            public string Patched { get; set; } = "";
        }
    }
}
