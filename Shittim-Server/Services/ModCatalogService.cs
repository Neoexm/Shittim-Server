using System.Security.Cryptography;
using System.Text;
using BlueArchiveAPI.Configuration;
using Newtonsoft.Json.Linq;
using Schale.Crypto;

namespace Shittim_Server.Services
{
    // The Steam client never fetches a catalog over the wire - it boots from catalog_Windows.bytes inside the install and refreshes its LocalLow copy from that - so mods reach it by rewriting the installed file in place. The shipped bytes are kept next to it as catalog_Windows.bytes.premods and stay the splice source, reseeded whenever Steam lands a fresh retail catalog.
    public class ModCatalogService
    {
        private const string BundleProvider = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";
        private const string AssetProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";
        private const string BundleResource = "UnityEngine.ResourceManagement.ResourceProviders.IAssetBundleResource";

        private readonly CustomCharacterService characters;
        private readonly ILogger<ModCatalogService> logger;
        private readonly object gate = new();

        private byte[] bytes;
        private string hash;
        private string stamp;

        public ModCatalogService(CustomCharacterService characters, ILogger<ModCatalogService> logger)
        {
            this.characters = characters;
            this.logger = logger;
        }

        public (byte[] Bytes, string Hash) Current()
        {
            lock (gate)
            {
                var source = SourcePath();
                if (source == null)
                    throw new InvalidOperationException("No catalog_Remote.bytes could be located to rewrite - point SHITTIM_ADDRESSABLE_CATALOG_PATH at one, or run the client once so it caches its own");

                var registry = Path.Combine(CustomCharacterService.ModsDir, "characters.json");
                var current = File.GetLastWriteTimeUtc(source).ToString("O") + "|" + (File.Exists(registry) ? File.GetLastWriteTimeUtc(registry).ToString("O") : "-");
                if (bytes != null && stamp == current)
                    return (bytes, hash);

                bytes = Build(source);
                hash = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
                stamp = current;
                return (bytes, hash);
            }
        }

        public static string SourcePath()
        {
            var configured = Environment.GetEnvironmentVariable("SHITTIM_ADDRESSABLE_CATALOG_PATH");
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            var install = InstallCatalogPath();
            if (install != null && File.Exists(install + ".premods"))
                return install + ".premods";

            var cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NEXON Games", "Blue Archive", "catalog_Remote.bytes");
            return File.Exists(cached) ? cached : null;
        }

        public static string InstallCatalogPath()
        {
            const string relative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Catalog\Windows\catalog_Windows.bytes";

            var metaPath = Config.Instance.ServerConfiguration.ClientMetadataPath;
            if (!string.IsNullOrWhiteSpace(metaPath))
            {
                var idx = metaPath.IndexOf(@"\BlueArchive_Data", StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    var candidate = Path.Combine(metaPath[..idx], relative);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return SteamGameLocator.FindGameFile(relative);
        }

        public void SyncClient()
        {
            var install = InstallCatalogPath();
            if (install == null)
                return;

            lock (gate)
            {
                var premods = install + ".premods";
                var mods = characters.List().Where(m => !string.IsNullOrWhiteSpace(m.Bundle) && File.Exists(Path.Combine(CustomCharacterService.ModsDir, m.Id.ToString(), m.Bundle))).ToList();
                var installed = File.ReadAllBytes(install);

                if (mods.Count == 0)
                {
                    if (File.Exists(premods) && !((ReadOnlySpan<byte>)installed).SequenceEqual(File.ReadAllBytes(premods)))
                    {
                        File.Copy(premods, install, true);
                        logger.LogInformation("No modded bundles left - put the shipped catalog back at {Path}", install);
                    }
                    return;
                }

                // a steam update or verify drops a fresh retail catalog over the spliced one; that unspliced file becomes the new source
                var spliced = mods.Any(m => ((ReadOnlySpan<byte>)installed).IndexOf(Encoding.UTF8.GetBytes(m.Bundle)) >= 0);
                if (!File.Exists(premods) || (!spliced && !((ReadOnlySpan<byte>)installed).SequenceEqual(File.ReadAllBytes(premods))))
                    File.Copy(install, premods, true);

                var gameData = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "..", "GameData", "Windows"));
                foreach (var mod in mods)
                {
                    var src = Path.Combine(CustomCharacterService.ModsDir, mod.Id.ToString(), mod.Bundle);
                    var dst = Path.Combine(gameData, mod.Bundle);
                    if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(src).Length || File.GetLastWriteTimeUtc(dst) < File.GetLastWriteTimeUtc(src))
                        File.Copy(src, dst, true);
                }

                var built = Build(premods);
                if (!((ReadOnlySpan<byte>)built).SequenceEqual(installed))
                {
                    File.WriteAllBytes(install, built);
                    var cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NEXON Games", "Blue Archive", "catalog_Remote.bytes");
                    if (File.Exists(cached))
                        File.WriteAllBytes(cached, built);
                    logger.LogInformation("Spliced {Count} modded bundle(s) into {Path}", mods.Count, install);
                }
            }
        }

        private byte[] Build(string source)
        {
            var catalog = AddressableCatalog.Read(File.ReadAllBytes(source));
            var bundleProvider = catalog.IndexOfProvider(BundleProvider);
            var assetProvider = catalog.IndexOfProvider(AssetProvider);
            var bundleType = catalog.IndexOfResourceType(BundleResource);

            // the request options carry a different field set per Addressables version, so a shipped one is cloned rather than composed from what this version happens to want. newer clients write the class name namespace-qualified where older catalogs kept it bare, hence the suffix match.
            var template = catalog.Extras.First(e => e.ClassName != null && e.ClassName.EndsWith("AssetBundleRequestOptions"));

            var added = 0;
            foreach (var mod in characters.List())
            {
                if (string.IsNullOrWhiteSpace(mod.Bundle) || mod.Addressables == null || mod.Addressables.Count == 0)
                    continue;

                var path = Path.Combine(CustomCharacterService.ModsDir, mod.Id.ToString(), mod.Bundle);
                if (!File.Exists(path))
                {
                    logger.LogWarning("Custom character {Id} names bundle {Bundle}, which was never staged - its addressables are being left out", mod.Id, mod.Bundle);
                    continue;
                }

                var options = JObject.Parse(template.Json);
                options["m_BundleName"] = Path.GetFileNameWithoutExtension(mod.Bundle);
                options["m_BundleSize"] = new FileInfo(path).Length;
                options["m_Hash"] = "";
                options["m_Crc"] = 0;

                // retail entries address their bundles as {PlatformUtils.AddressableLoadPath}\name, which the client expands to the GameData folder it loads every other bundle from; SyncClient copies the mod bundle there so the same expansion finds it
                var bundle = new CatalogEntry
                {
                    InternalId = catalog.AddInternalId($@"{{PlatformUtils.AddressableLoadPath}}\{mod.Bundle}"),
                    ProviderIndex = bundleProvider,
                    ExtraIndex = catalog.AddExtra(CatalogKey.Json7(template.AssemblyName, template.ClassName, options.ToString(Newtonsoft.Json.Formatting.None))),
                    ResourceType = bundleType
                };
                var bundleBucket = catalog.AddBucket(CatalogKey.Ascii(mod.Bundle), catalog.AddEntry(bundle));
                bundle.PrimaryKey = catalog.Keys.Count - 1;

                using var hasher = XXHash32.Create();
                hasher.ComputeHash(Encoding.UTF8.GetBytes(mod.Bundle));
                var dependencyHash = unchecked((int)hasher.HashUInt32);

                var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in mod.Addressables)
                {
                    var className = string.IsNullOrWhiteSpace(pair.Value) ? TypeOf(pair.Key) : pair.Value;
                    var resourceType = catalog.IndexOfResourceType(className);
                    if (resourceType < 0)
                    {
                        logger.LogWarning("Custom character {Id} wants {Address} as {Type}, which nothing in the shipped catalog uses", mod.Id, pair.Key, className);
                        continue;
                    }

                    var asset = new CatalogEntry
                    {
                        InternalId = catalog.AddInternalId(pair.Key),
                        ProviderIndex = assetProvider,
                        DependencyBucket = bundleBucket,
                        DependencyHash = dependencyHash,
                        ResourceType = resourceType
                    };
                    var index = catalog.AddEntry(asset);

                    var lookup = LookupKey(pair.Key);
                    catalog.AddBucket(CatalogKey.Ascii(lookup), index);
                    asset.PrimaryKey = catalog.Keys.Count - 1;
                    // the verbatim path is minted alongside so a hand-written Addressables.Load of the real asset path resolves too
                    if (lookup != pair.Key)
                        catalog.AddBucket(CatalogKey.Ascii(pair.Key), index);

                    indices[pair.Key] = index;
                    added++;
                }

                // an alias rides the target's entry as one more key, keeping the internal id the bundle provider actually loads by - a guid minted as its own entry would put the guid where the asset path belongs and load nothing
                if (mod.Aliases != null)
                    foreach (var alias in mod.Aliases)
                    {
                        if (!indices.TryGetValue(alias.Value, out var target))
                        {
                            logger.LogWarning("Custom character {Id} aliases {Alias} to {Address}, which is not among its addressables", mod.Id, alias.Key, alias.Value);
                            continue;
                        }
                        catalog.AddBucket(CatalogKey.Ascii(alias.Key.ToLowerInvariant()), target);
                        added++;
                    }
            }

            logger.LogInformation("Rewrote {Source} with {Added} modded addressable(s), {Total} entries total", Path.GetFileName(source), added, catalog.Entries.Count);
            return catalog.Write();
        }

        // Everything the shipped catalog keeps under AddressableAsset is looked up by the path below that folder with the extension dropped - CostumeExcel.TextureDir is "UIs/01_Common/01_Character/Student_Portrait_Aru" for an asset that lives at Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/Student_Portrait_Aru.png - while everything outside it, the spine rigs included, is keyed by its whole path. Both are lowercased.
        internal static string LookupKey(string address)
        {
            const string prefix = "Assets/_MX/AddressableAsset/";
            if (!address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return address.ToLowerInvariant();

            var trimmed = address.Substring(prefix.Length);
            var dot = trimmed.LastIndexOf('.');
            if (dot > trimmed.LastIndexOf('/'))
                trimmed = trimmed.Substring(0, dot);
            return trimmed.ToLowerInvariant();
        }

        private static string TypeOf(string address)
        {
            var name = address.ToLowerInvariant();
            if (name.EndsWith("_atlas.asset")) return "Spine.Unity.SpineAtlasAsset";
            if (name.EndsWith("_skeletondata.asset")) return "Spine.Unity.SkeletonDataAsset";
            if (name.EndsWith(".mat")) return "UnityEngine.Material";
            if (name.EndsWith(".prefab")) return "UnityEngine.GameObject";
            if (name.EndsWith(".ogg") || name.EndsWith(".wav")) return "UnityEngine.AudioClip";
            if (name.EndsWith(".png") || name.EndsWith(".jpg")) return "UnityEngine.Texture2D";
            return "UnityEngine.TextAsset";
        }
    }
}
