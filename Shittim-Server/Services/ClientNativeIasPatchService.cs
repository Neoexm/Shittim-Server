using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    public class ClientNativeIasPatchService : IHostedService
    {
        private const string DefaultPatchHost = "127.0.0.1";

        private static readonly Encoding Ascii = Encoding.ASCII;

        private static readonly byte[] HttpsSchemeBuilder = Convert.FromBase64String("SMdF9wgAAABIuGh0dHBzOi8vSIlF58ZF7wA=");
        private static readonly byte[] HttpSchemeBuilder = Convert.FromBase64String("SMdF9wcAAABIuGh0dHA6Ly8ASIlF58ZF7wA=");
        private static readonly BinaryPatchDefinition[] NoBinaryPatches = [];

        // IMS base URLs used by gamescale's alternative URL builder (sub_1806FC110 / sub_18007F550).
        // These build the IMS base WITHOUT /v1 appended; the caller appends /v1/<path>.
        // The null terminator is included in the 36/40-byte signatures to avoid false matches
        // with the /v1 variants which have '/' at that position rather than '\0'.
        private static readonly BinaryPatchDefinition[] GamescaleBinaryPatches =
        [
            new(
                "ims-primary-link-request-status-bypass-v2",
                Convert.FromBase64String("QYH8yAAAAA+FhwAAAIA9HyqRAAAPhP8AAABMi85Ig34="),
                Convert.FromBase64String("QYH8yAAAAEG8yAAAAIA9HyqRAAAPhP8AAABMi85Ig34="),
                "require RequestBase status 200",
                "force local RequestBase status 200"),
            new(
                "ims-live-base",
                [.. Ascii.GetBytes("https://signin.nexon.com/ims/public"), 0],
                [.. Ascii.GetBytes("http://127.0.0.1:5000/ims/public"), 0, 0, 0, 0],
                "https://signin.nexon.com/ims/public",
                "http://127.0.0.1:5000/ims/public"),
            new(
                "ims-pre-base",
                [.. Ascii.GetBytes("https://signin.nexon.com/ims/pre/public"), 0],
                [.. Ascii.GetBytes("http://127.0.0.1:5000/ims/pre/public"), 0, 0, 0, 0],
                "https://signin.nexon.com/ims/pre/public",
                "http://127.0.0.1:5000/ims/pre/public"),
            new(
                "ims-dev-base",
                [.. Ascii.GetBytes("https://dev-signin.nexon.com/ims/public"), 0],
                [.. Ascii.GetBytes("http://127.0.0.1:5000/ims/public"), 0, 0, 0, 0, 0, 0, 0, 0],
                "https://dev-signin.nexon.com/ims/public",
                "http://127.0.0.1:5000/ims/public"),
            // The native Bolt sign-in (IFGameAuth signInWithTicket worker sub_18006A500) builds its
            // URL as <scheme><toy-host>/signInWithTicket.nx. The live toy host is this static string,
            // appended by the toy base builder (sub_180046230) with a FIXED length of 0x1C (28). The
            // native gamescale HTTP client (a) refuses to send an http:// request to an EXTERNAL host
            // and (b) pins its TLS so mitm cannot intercept its https:// request — so the Bolt POST to
            // public.api.nexon.com never reaches our server either way and login hangs on "Now Loading"
            // right after get_primary_link. Redirect the host to our LOOPBACK server (where plain http
            // works, exactly like the IAS/IMS loopback calls). The replacement MUST stay exactly 28
            // bytes (the append copies a fixed 0x1C) so we keep the same length with a harmless userinfo
            // prefix: curl connects to 127.0.0.1:5000 and POSTs /toy/sdk/signInWithTicket.nx; the
            // "userx@" userinfo is never sent to the server. Requires the toy scheme builder to be
            // http:// (see ResolveSchemePatches — all gamescale scheme builders are http).
            new(
                "toy-bolt-host-loopback",
                [.. Ascii.GetBytes("public.api.nexon.com/toy/sdk")],
                [.. Ascii.GetBytes("userx@127.0.0.1:5000/toy/sdk")],
                "public.api.nexon.com/toy/sdk",
                "userx@127.0.0.1:5000/toy/sdk"),
            // IFInpay (in-app-pay / cash shop) "Enter" precondition suppress.
            // gamescale.core.dll's IFInpay::CheckPreConditions reports engine error 0x2FCDB393
            // (802010003, "Failed Enter Request") on the Steam build: the cash-shop product catalog
            // loads from Steam IAP (no products configured for an offline/private server) and the
            // precondition's GTable/Stamp-loaded checks fail. The error is generated entirely
            // client-side (no HTTP we can serve, verified via live capture across 5 triggers), and the
            // game shows a recurring "Failed Enter Request(802010003)" Notice popup on EVERY lobby
            // entry. The cash shop cannot function offline regardless. CheckPreConditions loads the
            // code as an immediate (`mov edx, 0x2FCDB393`) in three failure branches just before
            // building the error result; nulling the immediate makes the code 0 (success) -> no error
            // -> no popup. Confirmed live: lobby + menu re-entry stay clean, no side effects.
            // Anchored with surrounding bytes so the patched form (which contains the very common
            // `mov edx,0` / BA 00 00 00 00) cannot match unrelated code and corrupt the DLL on
            // restore. We deliberately do NOT patch the 4th occurrence (`mov eax,0x2FCDB393`/B8 at a
            // separate transaction-status function) — patching it triggers a spurious
            // "Your product is on the way" delivery popup.
            new(
                "ifinpay-checkprecond-enter-suppress-1",
                Hex("4C 8D 85 90 00 00 00 BA 93 B3 CD 2F 48 8D 4D 80"),
                Hex("4C 8D 85 90 00 00 00 BA 00 00 00 00 48 8D 4D 80"),
                "mov edx,802010003 (IFInpay Failed Enter Request #1)",
                "mov edx,0 (suppress IAP enter precondition error)"),
            new(
                "ifinpay-checkprecond-enter-suppress-2",
                Hex("E8 D0 85 72 00 BA 93 B3 CD 2F 48 8D 8D B0 00 00 00"),
                Hex("E8 D0 85 72 00 BA 00 00 00 00 48 8D 8D B0 00 00 00"),
                "mov edx,802010003 (IFInpay Failed Enter Request #2)",
                "mov edx,0 (suppress IAP enter precondition error)"),
            new(
                "ifinpay-checkprecond-enter-suppress-3",
                Hex("48 89 44 24 40 BA 93 B3 CD 2F 48 8D 8D B0 00 00 00"),
                Hex("48 89 44 24 40 BA 00 00 00 00 48 8D 8D B0 00 00 00"),
                "mov edx,802010003 (IFInpay Failed Enter Request #3)",
                "mov edx,0 (suppress IAP enter precondition error)"),
        ];

        private static readonly StringPatchDefinition[] NativeStringPatches =
        [
            new("ias-live-full-base", true, "/ias/live/public", "", 44, PatchPadding.Null,
            [
                "https://public.api.nexon.com/ias/live/public",
                "http://192.168.20.1:5000/ias/live/public/xxx"
            ]),
            new("ias-pre-full-base", true, "/ias/pre/public", "", 43, PatchPadding.Null,
            [
                "https://public.api.nexon.com/ias/pre/public",
                "http://192.168.20.1:5000/ias/pre/public/xxx"
            ]),
            new("ias-alpha-full-base", true, "/ias/alpha/public", "", 46, PatchPadding.Null,
            [
                "https://public.api.nexon.com/ias/alpha/public",
                "https://sandbox.api.nexon.com/ias/alpha/public",
                "http://192.168.20.1:5000/ias/alpha/public/xxx",
                "http://192.168.20.1:5000/ias/alpha/public/xxxx"
            ]),
            new("ias-qa-full-base", true, "/ias/qa/public", "", 43, PatchPadding.Null,
            [
                "https://public.api.nexon.com/ias/qa/public",
                "https://sandbox.api.nexon.com/ias/qa/public",
                "http://192.168.20.1:5000/ias/qa/public/xxx",
                "http://192.168.20.1:5000/ias/qa/public/xxxx"
            ]),
            new("ias-live-host-base", false, "/ias/live/public", "", 36, PatchPadding.Null,
            [
                "public.api.nexon.com/ias/live/public",
                "192.168.20.1:5000/ias/live/public/xx"
            ]),
            new("ias-pre-host-base", false, "/ias/pre/public", "", 35, PatchPadding.Null,
            [
                "public.api.nexon.com/ias/pre/public",
                "192.168.20.1:5000/ias/pre/public/xx"
            ]),
            new("ias-alpha-host-base", false, "/ias/alpha/public", "", 38, PatchPadding.Null,
            [
                "public.api.nexon.com/ias/alpha/public",
                "sandbox.api.nexon.com/ias/alpha/public",
                "192.168.20.1:5000/ias/alpha/public/xx",
                "192.168.20.1:5000/ias/alpha/public/xxx"
            ]),
            new("ias-qa-host-base", false, "/ias/qa/public", "", 35, PatchPadding.Null,
            [
                "public.api.nexon.com/ias/qa/public",
                "sandbox.api.nexon.com/ias/qa/public",
                "192.168.20.1:5000/ias/qa/public/xx",
                "192.168.20.1:5000/ias/qa/public/xxx"
            ]),
            new("ims-live-v1-base", true, "/ims/public", "/v1", 38, PatchPadding.Null,
            [
                "https://signin.nexon.com/ims/public/v1",
                "http://192.168.20.1:5000/ims/public/v1"     // in case previously patched to stale LAN IP
            ]),
            new("ims-pre-v1-base", true, "/ims/pre/public", "/v1", 42, PatchPadding.Null,
            [
                "https://signin.nexon.com/ims/pre/public/v1",
                "http://192.168.20.1:5000/ims/pre/public/v1" // in case previously patched to stale LAN IP
            ]),
            new("ims-dev-test-v1-base", true, "/ims/public", "/v1", 43, PatchPadding.Null,
            [
                "https://dev-signin.nexon.com/ims/public/v1",
                "https://test-signin.nexon.com/ims/public/v1",
                "http://192.168.20.1:5000/ims/public/v1"     // in case previously patched to stale LAN IP (fits within 43-byte slot)
            ],
            false,
            false)
        ];

        private static readonly StringPatchDefinition[] InfaceStringPatches =
        [
            new("ias-live-v1-base", true, "/ias/live/public", "/v1", 47, PatchPadding.PathSegment,
            [
                "https://public.api.nexon.com/ias/live/public/v1",
                "http://192.168.20.1:5000/ias/live/public/xxx/v1"
            ]),
            new("ias-pre-v1-base", true, "/ias/pre/public", "/v1", 46, PatchPadding.PathSegment,
            [
                "https://public.api.nexon.com/ias/pre/public/v1",
                "http://192.168.20.1:5000/ias/pre/public/xxx/v1"
            ]),
            new("ias-qa-v1-base", true, "/ias/qa/public", "/v1", 46, PatchPadding.PathSegment,
            [
                "https://public.api.nexon.com/ias/qa/public/v1",
                "https://sandbox.api.nexon.com/ias/qa/public/v1",
                "http://192.168.20.1:5000/ias/qa/public/xxxx/v1"
            ]),
            new("ias-alpha-v1-base", true, "/ias/alpha/public", "/v1", 49, PatchPadding.PathSegment,
            [
                "https://public.api.nexon.com/ias/alpha/public/v1",
                "https://sandbox.api.nexon.com/ias/alpha/public/v1",
                "http://192.168.20.1:5000/ias/alpha/public/xxxx/v1"
            ])
        ];

        private static readonly ModulePatchDefinition[] Modules =
        [
            new(
                "gamescale",
                "gamescale.core.dll",
                "SHITTIM_AUTO_PATCH_GAMESCALE_IAS",
                () => Config.Instance.ServerConfiguration.AutoPatchClientGamescaleIas,
                "SHITTIM_CLIENT_GAMESCALE_CORE_PATH",
                () => Config.Instance.ServerConfiguration.ClientGamescaleCorePath,
                () =>
                [
                    SteamGameLocator.CombineGamePath(Path.Combine("BlueArchive_Data", "Plugins", "x86_64", "gamescale.core.dll")) ?? "",
                    @"F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive_Data\Plugins\x86_64\gamescale.core.dll",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "BlueArchive_Data", "Plugins", "x86_64", "gamescale.core.dll")
                ],
                true,
                GamescaleBinaryPatches,
                NativeStringPatches),
            new(
                "nexon-platform",
                "NexonPlatformModules.dll",
                "SHITTIM_AUTO_PATCH_NEXON_PLATFORM_IAS",
                () => Config.Instance.ServerConfiguration.AutoPatchClientNexonPlatformIas,
                "SHITTIM_CLIENT_NEXON_PLATFORM_MODULES_PATH",
                () => Config.Instance.ServerConfiguration.ClientNexonPlatformModulesPath,
                () =>
                [
                    SteamGameLocator.CombineGamePath(Path.Combine("BlueArchive_Data", "Plugins", "x86_64", "NexonPlatformModules.dll")) ?? "",
                    @"F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive_Data\Plugins\x86_64\NexonPlatformModules.dll",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "BlueArchive_Data", "Plugins", "x86_64", "NexonPlatformModules.dll")
                ],
                true,
                NoBinaryPatches,
                NativeStringPatches),
            new(
                "inface",
                "inface.dll",
                "SHITTIM_AUTO_PATCH_INFACE_IAS",
                () => Config.Instance.ServerConfiguration.AutoPatchClientInfaceIas,
                "SHITTIM_CLIENT_INFACE_PATH",
                () => Config.Instance.ServerConfiguration.ClientInfacePath,
                () =>
                [
                    SteamGameLocator.CombineGamePath(Path.Combine("BlueArchive_Data", "Plugins", "x86_64", "inface.dll")) ?? "",
                    @"F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive_Data\Plugins\x86_64\inface.dll",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlueArchive", "BlueArchive_Data", "Plugins", "x86_64", "inface.dll")
                ],
                false,
                NoBinaryPatches,
                InfaceStringPatches)
        ];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly ILogger<ClientNativeIasPatchService> logger;
        private readonly List<ModulePatchState> patchStates = [];

        public ClientNativeIasPatchService(ILogger<ClientNativeIasPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var target = GetPatchTarget();

            foreach (var module in Modules)
            {
                if (!IsEnabled(module))
                {
                    RestoreDisabledModule(module);
                    continue;
                }

                try
                {
                    var path = GetModulePath(module);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        logger.LogWarning("{ModuleName} IAS auto-patch is enabled, but no path was configured", module.DisplayName);
                        continue;
                    }

                    if (!File.Exists(path))
                    {
                        logger.LogWarning("{ModuleName} file not found: {ModulePath}", module.DisplayName, path);
                        continue;
                    }

                    var state = PatchModule(module, path, target);
                    if (state != null)
                        patchStates.Add(state);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to patch {ModuleName} IAS routing", module.DisplayName);
                }
            }

            return Task.CompletedTask;
        }

        private void RestoreDisabledModule(ModulePatchDefinition module)
        {
            try
            {
                var path = GetModulePath(module);
                if (string.IsNullOrWhiteSpace(path))
                {
                    logger.LogInformation("{ModuleName} IAS patch disabled", module.DisplayName);
                    return;
                }

                var statePath = GetStatePath(path);
                var state = LoadState(statePath);
                if (state == null)
                {
                    logger.LogInformation("{ModuleName} IAS patch disabled", module.DisplayName);
                    return;
                }

                logger.LogInformation("{ModuleName} IAS patch disabled; restoring existing patch state: {StatePath}", module.DisplayName, statePath);
                RestoreModule(state);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore disabled {ModuleName} IAS routing", module.DisplayName);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var state in patchStates.AsEnumerable().Reverse())
            {
                try
                {
                    RestoreModule(state);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to restore {ModuleName} IAS routing", state.DisplayName);
                }
            }

            return Task.CompletedTask;
        }

        private ModulePatchState PatchModule(ModulePatchDefinition module, string path, IasPatchTarget target)
        {
            var statePath = GetStatePath(path);
            var existingState = LoadState(statePath);
            var data = File.ReadAllBytes(path);
            var activePatches = new List<PatchResult>();

            if (module.PatchSchemeBuilder)
                activePatches.AddRange(ResolveSchemePatches(module, data, existingState));

            foreach (var definition in module.BinaryPatches)
            {
                var results = ResolveBinaryPatch(module, data, existingState, definition);
                if (results.Count == 0)
                    logger.LogWarning("{ModuleName} IAS binary patch target was not found: {PatchName}", module.DisplayName, definition.Name);

                activePatches.AddRange(results);
            }

            foreach (var definition in module.StringPatches)
            {
                var results = ResolveStringPatch(module, data, existingState, definition, target);
                if (results.Count == 0)
                    logger.LogWarning("{ModuleName} IAS patch target was not found: {PatchName}", module.DisplayName, definition.Name);

                activePatches.AddRange(results);
            }

            RestoreStalePatches(module, path, existingState, activePatches);

            if (activePatches.Count == 0)
            {
                logger.LogWarning("No {ModuleName} IAS patch targets matched: {ModulePath}", module.DisplayName, path);
                return null;
            }

            var writePlans = activePatches
                .Where(x => !x.Current.SequenceEqual(Convert.FromBase64String(x.Entry.Patched)))
                .ToList();

            logger.LogInformation("{ModuleName} IAS patch target is http://{Host}:{Port}", module.DisplayName, target.Host, target.Port);

            if (writePlans.Count > 0)
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                foreach (var plan in writePlans)
                {
                    var patched = Convert.FromBase64String(plan.Entry.Patched);
                    stream.Position = plan.Entry.Offset;
                    stream.Write(patched, 0, patched.Length);
                    logger.LogInformation(
                        "Patched {ModuleName} IAS {PatchName} at 0x{Offset:X}: {Before} -> {After}",
                        module.DisplayName,
                        plan.Entry.Name,
                        plan.Entry.Offset,
                        plan.Entry.Before,
                        plan.Entry.After);
                }

                stream.Flush(true);
            }

            foreach (var plan in activePatches.Except(writePlans))
            {
                logger.LogInformation(
                    "{ModuleName} IAS already patched {PatchName} at 0x{Offset:X}: {After}",
                    module.DisplayName,
                    plan.Entry.Name,
                    plan.Entry.Offset,
                    plan.Entry.After);
            }

            var state = new ModulePatchState
            {
                ModuleName = module.Name,
                DisplayName = module.DisplayName,
                ModulePath = path,
                TargetHost = target.Host,
                TargetPort = target.Port,
                Patches = activePatches.Select(x => x.Entry).ToList()
            };

            SaveState(statePath, state);
            logger.LogInformation("{ModuleName} IAS patch state saved: {StatePath}", module.DisplayName, statePath);
            return state;
        }

        private void RestoreStalePatches(ModulePatchDefinition module, string path, ModulePatchState existingState, List<PatchResult> activePatches)
        {
            if (existingState?.Patches == null || existingState.Patches.Count == 0)
                return;

            var stalePatches = existingState.Patches
                .Where(existing => activePatches.All(active => active.Entry.Name != existing.Name || active.Entry.Offset != existing.Offset))
                .ToList();

            if (stalePatches.Count == 0)
                return;

            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            foreach (var patch in stalePatches)
            {
                var original = Convert.FromBase64String(patch.Original);
                var patched = Convert.FromBase64String(patch.Patched);

                if (patch.Offset < 0 || patch.Offset + original.Length > stream.Length)
                {
                    logger.LogWarning("Skipping stale {ModuleName} IAS restore for {PatchName}; saved offset is outside the file", module.DisplayName, patch.Name);
                    continue;
                }

                var current = new byte[original.Length];
                stream.Position = patch.Offset;
                stream.ReadExactly(current, 0, current.Length);

                if (current.SequenceEqual(original))
                    continue;

                if (!current.SequenceEqual(patched))
                {
                    logger.LogWarning("Skipping stale {ModuleName} IAS restore for {PatchName}; bytes changed after startup", module.DisplayName, patch.Name);
                    continue;
                }

                stream.Position = patch.Offset;
                stream.Write(original, 0, original.Length);
                logger.LogInformation(
                    "Restored stale {ModuleName} IAS {PatchName} at 0x{Offset:X}: {After} -> {Before}",
                    module.DisplayName,
                    patch.Name,
                    patch.Offset,
                    patch.After,
                    patch.Before);
            }

            stream.Flush(true);
        }

        private void RestoreModule(ModulePatchState state)
        {
            var path = state.ModulePath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var statePath = GetStatePath(path);
            if (!File.Exists(path))
            {
                logger.LogWarning("{ModuleName} disappeared before restore: {ModulePath}", state.DisplayName, path);
                return;
            }

            var skipped = 0;

            using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                foreach (var patch in state.Patches)
                {
                    var original = Convert.FromBase64String(patch.Original);
                    var patched = Convert.FromBase64String(patch.Patched);

                    if (patch.Offset < 0 || patch.Offset + original.Length > stream.Length)
                    {
                        skipped++;
                        logger.LogWarning("Skipping {ModuleName} IAS restore for {PatchName}; saved offset is outside the file", state.DisplayName, patch.Name);
                        continue;
                    }

                    var current = new byte[original.Length];
                    stream.Position = patch.Offset;
                    stream.ReadExactly(current, 0, current.Length);

                    if (current.SequenceEqual(original))
                        continue;

                    if (!current.SequenceEqual(patched))
                    {
                        skipped++;
                        logger.LogWarning("Skipping {ModuleName} IAS restore for {PatchName}; bytes changed after startup", state.DisplayName, patch.Name);
                        continue;
                    }

                    stream.Position = patch.Offset;
                    stream.Write(original, 0, original.Length);
                    logger.LogInformation(
                        "Restored {ModuleName} IAS {PatchName} at 0x{Offset:X}: {After} -> {Before}",
                        state.DisplayName,
                        patch.Name,
                        patch.Offset,
                        patch.After,
                        patch.Before);
                }

                stream.Flush(true);
            }

            if (skipped == 0 && File.Exists(statePath))
                File.Delete(statePath);

            logger.LogInformation("Restored {ModuleName} IAS routing: {ModulePath}", state.DisplayName, path);
        }

        private List<PatchResult> ResolveSchemePatches(ModulePatchDefinition module, byte[] data, ModulePatchState existingState)
        {
            // The scheme builder is the compiled code `mov rax,'https://'; len=8` (HttpsSchemeBuilder
            // pattern) that gamescale.core.dll / NexonPlatformModules.dll use to prepend a scheme to a
            // HOST-only base (e.g. "127.0.0.1:5000/ias/live/public"). gamescale builds several IAS/IMS
            // calls this way, so if the builder stays https:// it produces https://127.0.0.1:5000/ias/...
            // and the native curl client does a TLS handshake against our plain-HTTP Kestrel — which
            // fails with no response, surfacing in-game as "ias api failed. StatusCode: 0(803020008)".
            // (The IAS LoginLink survives only because it uses a separate FULL http:// base string.)
            // It MUST be patched to http:// here, exactly like NexonPlatformModules. This does NOT
            // affect the Bolt sign-in / toy calls to public.api.nexon.com: their TLS is not pinned, so
            // mitm intercepts and rewrites them to our server whether they go out as http or https.
            // (An earlier build skipped gamescale on a "bolt needs native https" theory; that was wrong
            // — gamescale has no native bolt URL string, bolt_url comes from the JS bundle.)
            var offsets = new SortedSet<long>();

            foreach (var entry in existingState?.Patches.Where(x => x.Name.StartsWith("ias-http-scheme-builder", StringComparison.Ordinal)) ?? [])
            {
                if (entry.Offset >= 0 && entry.Offset + HttpSchemeBuilder.Length <= data.LongLength)
                    offsets.Add(entry.Offset);
            }

            foreach (var offset in FindAll(data, HttpsSchemeBuilder))
                offsets.Add(offset);

            foreach (var offset in FindAll(data, HttpSchemeBuilder))
                offsets.Add(offset);

            if (offsets.Count == 0)
            {
                logger.LogWarning("{ModuleName} IAS https/http scheme builder patch target was not found", module.DisplayName);
                return [];
            }

            // ALL scheme-builder constants (each the compiled `mov [len],8; mov rax,'https://'`
            // sequence) are patched to http://. gamescale has three of these inside sibling base-URL
            // builders (toy/Bolt = sub_180046230, IAS = sub_180049870, IMS = sub_18004B650); every one
            // of those bases is routed to our LOOPBACK server (the IAS/IMS bases via string patches and
            // the toy/Bolt host via the "toy-bolt-host-loopback" binary patch), and the loopback server
            // is plain HTTP, so the scheme MUST be http:// for all of them. (An earlier build kept the
            // toy/Bolt scheme https:// hoping mitm would intercept the external Bolt request, but the
            // native gamescale HTTP client pins its TLS so mitm cannot — hence the loopback approach.)
            long keepHttpsOffset = -1;

            var results = new List<PatchResult>();
            var index = 1;

            foreach (var offset in offsets)
            {
                var current = Slice(data, offset, HttpSchemeBuilder.Length);
                var isHttps = current.SequenceEqual(HttpsSchemeBuilder);
                var isHttp = current.SequenceEqual(HttpSchemeBuilder);
                if (!isHttps && !isHttp)
                    continue;

                var keepHttps = offset == keepHttpsOffset;
                var desired = keepHttps ? HttpsSchemeBuilder : HttpSchemeBuilder;

                var existingEntry = FindExistingPatch(existingState, "ias-http-scheme-builder", offset);
                // Clean (unpatched) form of this constant is always https://; record that as Original
                // so an on-shutdown restore returns the DLL to its pristine state regardless of which
                // scheme we currently want.
                var original = existingEntry?.Original ?? Convert.ToBase64String(HttpsSchemeBuilder);
                var name = existingEntry?.Name ?? $"ias-http-scheme-builder-{index}";

                results.Add(new PatchResult(new NativeIasPatchEntry
                {
                    Name = name,
                    Offset = offset,
                    Original = original,
                    Patched = Convert.ToBase64String(desired),
                    Before = isHttps ? "https://" : "http://",
                    After = keepHttps ? "https:// (kept for external Bolt)" : "http://"
                }, current));

                index++;
            }

            return results;
        }

        private List<PatchResult> ResolveBinaryPatch(ModulePatchDefinition module, byte[] data, ModulePatchState existingState, BinaryPatchDefinition definition)
        {
            if (definition.Original.Length != definition.Patched.Length)
                throw new InvalidOperationException($"native IAS binary patch {definition.Name} has mismatched byte lengths");

            var offsets = new SortedSet<long>();

            foreach (var entry in existingState?.Patches.Where(x => x.Name.StartsWith(definition.Name, StringComparison.Ordinal)) ?? [])
            {
                if (entry.Offset >= 0 && entry.Offset + definition.Original.Length <= data.LongLength)
                    offsets.Add(entry.Offset);
            }

            foreach (var offset in FindAll(data, definition.Original))
                offsets.Add(offset);

            foreach (var offset in FindAll(data, definition.Patched))
                offsets.Add(offset);

            var results = new List<PatchResult>();
            foreach (var offset in offsets)
            {
                var current = Slice(data, offset, definition.Original.Length);
                var existingEntry = FindExistingPatch(existingState, definition.Name, offset);

                if (!current.SequenceEqual(definition.Original) && !current.SequenceEqual(definition.Patched))
                {
                    if (existingEntry == null)
                        continue;

                    var existingOriginal = Convert.FromBase64String(existingEntry.Original);
                    var existingPatched = Convert.FromBase64String(existingEntry.Patched);
                    if (!current.SequenceEqual(existingOriginal) && !current.SequenceEqual(existingPatched))
                        continue;
                }

                results.Add(new PatchResult(new NativeIasPatchEntry
                {
                    Name = definition.Name,
                    Offset = offset,
                    Original = existingEntry?.Original ?? Convert.ToBase64String(current),
                    Patched = Convert.ToBase64String(definition.Patched),
                    Before = definition.Before,
                    After = definition.After
                }, current));
            }

            if (results.Count > 1)
                logger.LogWarning("{ModuleName} IAS binary patch {PatchName} matched {MatchCount} locations", module.DisplayName, definition.Name, results.Count);

            return results;
        }

        private List<PatchResult> ResolveStringPatch(ModulePatchDefinition module, byte[] data, ModulePatchState existingState, StringPatchDefinition definition, IasPatchTarget target)
        {
            var targetText = BuildTargetText(definition, target);
            var patched = BuildAsciiPatch(definition.Name, targetText, definition.SlotLength, definition.Padding);
            var offsets = new SortedSet<long>();

            foreach (var entry in existingState?.Patches.Where(x => x.Name.StartsWith(definition.Name, StringComparison.Ordinal)) ?? [])
            {
                if (entry.Offset >= 0 && entry.Offset + definition.SlotLength <= data.LongLength)
                    offsets.Add(entry.Offset);
            }

            var candidates = definition.SearchTargetText
                ? definition.Candidates.Append(targetText)
                : definition.Candidates;

            foreach (var candidate in candidates)
            {
                var candidateBytes = Ascii.GetBytes(candidate);
                foreach (var offset in FindAll(data, candidateBytes))
                {
                    if (offset + definition.SlotLength <= data.LongLength && IsLikelyStringStart(data, offset))
                        offsets.Add(offset);
                }
            }

            if (definition.SearchRoute)
            {
                foreach (var offset in FindRouteBasedOffsets(data, definition))
                    offsets.Add(offset);
            }

            var results = new List<PatchResult>();
            var orderedOffsets = offsets.ToList();

            for (var i = 0; i < orderedOffsets.Count; i++)
            {
                var offset = orderedOffsets[i];
                var current = Slice(data, offset, definition.SlotLength);
                var existingEntry = FindExistingPatch(existingState, definition.Name, offset);
                var currentText = ReadAsciiSlot(current);

                if (!IsKnownStringSlot(current, existingEntry, definition, targetText, patched))
                    continue;

                results.Add(new PatchResult(new NativeIasPatchEntry
                {
                    Name = definition.Name,
                    Offset = offset,
                    Original = existingEntry?.Original ?? Convert.ToBase64String(current),
                    Patched = Convert.ToBase64String(patched),
                    Before = currentText,
                    After = targetText
                }, current));
            }

            if (results.Count > 1)
                logger.LogWarning("{ModuleName} IAS patch {PatchName} matched {MatchCount} locations", module.DisplayName, definition.Name, results.Count);

            return results;
        }

        private static string BuildTargetText(StringPatchDefinition definition, IasPatchTarget target)
        {
            var baseText = definition.IncludeScheme
                ? $"http://{target.Host}:{target.Port}{definition.Route}"
                : $"{target.Host}:{target.Port}{definition.Route}";

            var cleanText = $"{baseText}{definition.Suffix}";
            if (definition.Padding == PatchPadding.Null)
                return cleanText;

            if (cleanText.Length > definition.SlotLength)
                throw new InvalidOperationException($"native IAS patch {definition.Name} replacement is too long: {cleanText}");

            var paddingLength = definition.SlotLength - cleanText.Length;
            if (paddingLength == 0)
                return cleanText;

            return $"{baseText}/{new string('x', paddingLength - 1)}{definition.Suffix}";
        }

        private static byte[] BuildAsciiPatch(string name, string value, int length, PatchPadding padding)
        {
            var bytes = Ascii.GetBytes(value);
            if (bytes.Length > length)
                throw new InvalidOperationException($"native IAS patch {name} replacement is too long: {value}");

            if (padding == PatchPadding.PathSegment && bytes.Length != length)
                throw new InvalidOperationException($"native IAS patch {name} replacement has the wrong padded length: {value}");

            var output = new byte[length];
            Array.Copy(bytes, output, bytes.Length);
            return output;
        }

        private static bool IsKnownStringSlot(byte[] current, NativeIasPatchEntry existingEntry, StringPatchDefinition definition, string targetText, byte[] patched)
        {
            if (current.SequenceEqual(patched))
                return true;

            if (existingEntry != null)
            {
                var existingOriginal = Convert.FromBase64String(existingEntry.Original);
                var existingPatched = Convert.FromBase64String(existingEntry.Patched);
                if (current.SequenceEqual(existingOriginal) || current.SequenceEqual(existingPatched))
                    return true;
            }

            var text = ReadAsciiSlot(current);
            if (text == targetText || definition.Candidates.Any(candidate => text == candidate))
                return true;

            return LooksLikeSameIasSlot(text, definition);
        }

        private static bool LooksLikeSameIasSlot(string text, StringPatchDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.Contains(definition.Route, StringComparison.Ordinal))
                return false;

            if (!string.IsNullOrEmpty(definition.Suffix) && !text.Contains(definition.Suffix, StringComparison.Ordinal))
                return false;

            if (definition.IncludeScheme)
                return text.StartsWith("http://", StringComparison.Ordinal) || text.StartsWith("https://", StringComparison.Ordinal);

            return !text.StartsWith("http://", StringComparison.Ordinal)
                && !text.StartsWith("https://", StringComparison.Ordinal)
                && text.Contains(':', StringComparison.Ordinal);
        }

        private static List<long> FindRouteBasedOffsets(byte[] data, StringPatchDefinition definition)
        {
            var offsets = new SortedSet<long>();
            var markers = new[] { $"{definition.Route}{definition.Suffix}", definition.Route }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Select(Ascii.GetBytes);

            foreach (var marker in markers)
            {
                foreach (var markerOffset in FindAll(data, marker))
                {
                    var start = FindStringStart(data, markerOffset);
                    if (start + definition.SlotLength <= data.LongLength)
                        offsets.Add(start);
                }
            }

            return offsets.ToList();
        }

        private static long FindStringStart(byte[] data, long offset)
        {
            var start = offset;
            while (start > 0 && IsUrlCharacter(data[start - 1]))
                start--;

            return start;
        }

        private static NativeIasPatchEntry FindExistingPatch(ModulePatchState state, string namePrefix, long offset)
        {
            return state?.Patches.FirstOrDefault(x =>
                x.Offset == offset &&
                x.Name.StartsWith(namePrefix, StringComparison.Ordinal));
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

        private static byte[] Hex(string value)
        {
            var clean = value.Replace(" ", "", StringComparison.Ordinal);
            var bytes = new byte[clean.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);

            return bytes;
        }

        private static byte[] Slice(byte[] data, long offset, int length)
        {
            var buffer = new byte[length];
            Array.Copy(data, offset, buffer, 0, length);
            return buffer;
        }

        private static string ReadAsciiSlot(byte[] bytes)
        {
            var length = Array.IndexOf(bytes, (byte)0);
            if (length < 0)
                length = bytes.Length;

            return Ascii.GetString(bytes, 0, length);
        }

        private static bool IsLikelyStringStart(byte[] data, long offset)
        {
            if (offset <= 0)
                return true;

            var previous = data[offset - 1];
            return previous == 0 || previous < 0x21 || previous > 0x7E || !IsUrlCharacter(previous);
        }

        private static bool IsUrlCharacter(byte value)
        {
            return value is >= (byte)'a' and <= (byte)'z'
                or >= (byte)'A' and <= (byte)'Z'
                or >= (byte)'0' and <= (byte)'9'
                or (byte)'.'
                or (byte)'/'
                or (byte)':'
                or (byte)'-'
                or (byte)'_';
        }

        private static bool IsEnabled(ModulePatchDefinition module)
        {
            var globalValue = Environment.GetEnvironmentVariable("SHITTIM_AUTO_PATCH_NATIVE_IAS");
            if (bool.TryParse(globalValue, out var globalEnabled) && !globalEnabled)
                return false;

            var value = Environment.GetEnvironmentVariable(module.ToggleEnvironmentVariable);
            return bool.TryParse(value, out var enabled)
                ? enabled
                : module.IsEnabled();
        }

        private static string GetModulePath(ModulePatchDefinition module)
        {
            var configuredPath = Environment.GetEnvironmentVariable(module.PathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredPath))
                configuredPath = module.GetConfiguredPath();

            if (!string.IsNullOrWhiteSpace(configuredPath))
                return ResolvePath(configuredPath);

            return module.GetDefaultPaths().FirstOrDefault(File.Exists);
        }

        private static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            return File.Exists(basePath) ? basePath : Path.GetFullPath(path);
        }

        private static IasPatchTarget GetPatchTarget()
        {
            var config = Config.Instance.ServerConfiguration;
            var port = Environment.GetEnvironmentVariable("SHITTIM_IAS_PATCH_PORT");
            if (string.IsNullOrWhiteSpace(port))
                port = config.HostPort;

            return new IasPatchTarget(DefaultPatchHost, string.IsNullOrWhiteSpace(port) ? "5000" : port.Trim());
        }

        private static string GetStatePath(string path)
        {
            return $"{path}.shittim_native_ias_patch.json";
        }

        private static ModulePatchState LoadState(string statePath)
        {
            if (!File.Exists(statePath))
                return null;

            return JsonSerializer.Deserialize<ModulePatchState>(File.ReadAllText(statePath));
        }

        private static void SaveState(string statePath, ModulePatchState state)
        {
            File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        private enum PatchPadding
        {
            Null,
            PathSegment
        }

        private sealed record IasPatchTarget(string Host, string Port);

        private sealed record ModulePatchDefinition(
            string Name,
            string DisplayName,
            string ToggleEnvironmentVariable,
            Func<bool> IsEnabled,
            string PathEnvironmentVariable,
            Func<string> GetConfiguredPath,
            Func<string[]> GetDefaultPaths,
            bool PatchSchemeBuilder,
            BinaryPatchDefinition[] BinaryPatches,
            StringPatchDefinition[] StringPatches);

        private sealed record BinaryPatchDefinition(
            string Name,
            byte[] Original,
            byte[] Patched,
            string Before,
            string After);

        private sealed record StringPatchDefinition(
            string Name,
            bool IncludeScheme,
            string Route,
            string Suffix,
            int SlotLength,
            PatchPadding Padding,
            string[] Candidates,
            bool SearchRoute = true,
            bool SearchTargetText = true);

        private sealed record PatchResult(NativeIasPatchEntry Entry, byte[] Current);

        private sealed class ModulePatchState
        {
            public string ModuleName { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string ModulePath { get; set; } = "";
            public string TargetHost { get; set; } = "";
            public string TargetPort { get; set; } = "";
            public List<NativeIasPatchEntry> Patches { get; set; } = [];
        }

        private sealed class NativeIasPatchEntry
        {
            public string Name { get; set; } = "";
            public long Offset { get; set; }
            public string Original { get; set; } = "";
            public string Patched { get; set; } = "";
            public string Before { get; set; } = "";
            public string After { get; set; } = "";
        }
    }
}
