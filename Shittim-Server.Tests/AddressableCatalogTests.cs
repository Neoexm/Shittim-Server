using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The catalog writer is only useful if it is byte-exact on input it did not produce, so the synthetic case covers every object type and the installed client's own 64 MB catalog covers the shapes Nexon actually ships.
public class AddressableCatalogTests
{
    [Fact]
    public void EveryKeyObjectTypeSurvivesARoundTrip()
    {
        var c = SyntheticCatalog();
        var written = c.Write();
        var again = AddressableCatalog.Read(written);

        Assert.Equal(written, again.Write());
        Assert.Equal(c.Keys.Count, again.Keys.Count);
        Assert.Equal("nx0001.bundle", again.Keys[0].Text);
        Assert.Equal(CatalogObjectType.Int32, again.Keys[2].Type);
        Assert.Equal("Sprites/Colored/round", again.Keys[5].Text);
        Assert.Equal("AssetBundleRequestOptions", again.Extras[0].ClassName);
    }

    [Fact]
    public void ExtraDataStaysAttachedToItsEntryAcrossARewrite()
    {
        var c = SyntheticCatalog();
        var again = AddressableCatalog.Read(c.Write());

        Assert.Equal(0, again.Entries[0].ExtraIndex);
        Assert.Equal(-1, again.Entries[1].ExtraIndex);
        Assert.Contains("m_BundleSize", again.Extras[again.Entries[0].ExtraIndex].Json);
    }

    [Fact]
    public void ABucketStillPointsAtItsKeyWhenAnEarlierKeyChangesLength()
    {
        var c = SyntheticCatalog();
        c.Keys[0] = CatalogKey.Ascii("a-much-longer-address-than-the-one-it-replaces/and/then/some.png");

        var again = AddressableCatalog.Read(c.Write());

        Assert.Equal(c.Buckets.Count, again.Buckets.Count);
        for (var i = 0; i < c.Buckets.Count; i++)
            Assert.Equal(c.Buckets[i].KeyIndex, again.Buckets[i].KeyIndex);
    }

    [Fact]
    public void TheInstalledCatalogRewritesByteForByte()
    {
        var path = InstalledCatalog();
        if (path == null)
            return;

        var original = File.ReadAllBytes(path);
        var catalog = AddressableCatalog.Read(original);

        Assert.Equal("AddressablesMainContentCatalog", catalog.LocatorId);
        Assert.Equal(catalog.Keys.Count, catalog.Buckets.Count);
        Assert.Contains("UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider", catalog.ProviderIds);
        Assert.Equal(original, catalog.Write());
    }

    [Fact]
    public void AModBundleSplicedIntoTheInstalledCatalogResolvesWithoutMovingAnythingElse()
    {
        var path = InstalledCatalog();
        if (path == null)
            return;

        var catalog = AddressableCatalog.Read(File.ReadAllBytes(path));
        var shippedEntries = catalog.Entries.Count;
        var witness = catalog.Buckets[0];
        var witnessKey = catalog.Keys[witness.KeyIndex].Text;
        var witnessId = catalog.InternalIds[catalog.Entries[witness.Entries[0]].InternalId];

        var bundle = new CatalogEntry
        {
            InternalId = catalog.AddInternalId("http://127.0.0.1:5000/addressables/mods/10001/nx0001_assets_all.bundle"),
            ProviderIndex = catalog.IndexOfProvider("UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider"),
            ExtraIndex = catalog.AddExtra(CatalogKey.Json7("Unity.ResourceManager", "AssetBundleRequestOptions", "{\"m_BundleName\":\"nx0001_assets_all\",\"m_BundleSize\":4695}")),
            ResourceType = catalog.IndexOfResourceType("UnityEngine.ResourceManagement.ResourceProviders.IAssetBundleResource")
        };
        var bundleBucket = catalog.AddBucket(CatalogKey.Ascii("nx0001_assets_all.bundle"), catalog.AddEntry(bundle));
        bundle.PrimaryKey = catalog.Keys.Count - 1;

        var asset = new CatalogEntry
        {
            InternalId = catalog.AddInternalId("Assets/_MX/SpineCharacters/nx0001_spr/nx0001_spr.skel.bytes"),
            ProviderIndex = catalog.IndexOfProvider("UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider"),
            DependencyBucket = bundleBucket,
            ResourceType = catalog.IndexOfResourceType("UnityEngine.TextAsset")
        };
        catalog.AddBucket(CatalogKey.Ascii("assets/_mx/spinecharacters/nx0001_spr/nx0001_spr.skel.bytes"), catalog.AddEntry(asset));
        asset.PrimaryKey = catalog.Keys.Count - 1;

        var again = AddressableCatalog.Read(catalog.Write());

        Assert.Equal(shippedEntries + 2, again.Entries.Count);
        Assert.Equal(witnessKey, again.Keys[again.Buckets[0].KeyIndex].Text);
        Assert.Equal(witnessId, again.InternalIds[again.Entries[again.Buckets[0].Entries[0]].InternalId]);

        var spliced = again.Buckets[again.Buckets.Count - 1];
        var loaded = again.Entries[spliced.Entries[0]];
        Assert.Equal("assets/_mx/spinecharacters/nx0001_spr/nx0001_spr.skel.bytes", again.Keys[spliced.KeyIndex].Text);
        Assert.Equal("Assets/_MX/SpineCharacters/nx0001_spr/nx0001_spr.skel.bytes", again.InternalIds[loaded.InternalId]);
        Assert.Equal("assets/_mx/spinecharacters/nx0001_spr/nx0001_spr.skel.bytes", again.Keys[loaded.PrimaryKey].Text);

        var dependency = again.Buckets[loaded.DependencyBucket];
        var dependencyEntry = again.Entries[dependency.Entries[0]];
        Assert.Equal("http://127.0.0.1:5000/addressables/mods/10001/nx0001_assets_all.bundle", again.InternalIds[dependencyEntry.InternalId]);
        Assert.Contains("nx0001_assets_all", again.Extras[dependencyEntry.ExtraIndex].Json);
    }

    [Fact]
    public void TheInstalledCatalogStillCarriesARequestOptionsTemplate()
    {
        var path = InstalledCatalog();
        if (path == null)
            return;

        // the splice clones a shipped AssetBundleRequestOptions extra; the 2026-08 client update renamed it from the bare class name to the namespace-qualified one, which is the drift this pins
        var catalog = AddressableCatalog.Read(File.ReadAllBytes(path));
        Assert.Contains(catalog.Extras, e => e.ClassName != null && e.ClassName.EndsWith("AssetBundleRequestOptions"));
    }

    [Fact]
    public void AGuidAliasSharesItsTargetsEntryAndInternalId()
    {
        var c = SyntheticCatalog();
        c.AddBucket(CatalogKey.Ascii("cfb8653752a2d00489f9b3580f78f1c1"), c.Buckets[1].Entries[0]);

        var again = AddressableCatalog.Read(c.Write());

        var alias = again.Buckets[again.Buckets.Count - 1];
        Assert.Equal("cfb8653752a2d00489f9b3580f78f1c1", again.Keys[alias.KeyIndex].Text);
        Assert.Equal(again.Buckets[1].Entries[0], alias.Entries[0]);
        Assert.Equal("Assets/_MX/SpineCharacters/nx0001_spr/nx0001_spr.png", again.InternalIds[again.Entries[alias.Entries[0]].InternalId]);
    }

    [Fact]
    public void AnAddressUnderAddressableAssetIsKeyedTheWayCostumeExcelNamesIt()
    {
        Assert.Equal("uis/01_common/01_character/student_portrait_nx0001", ModCatalogService.LookupKey("Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/Student_Portrait_Nx0001.png"));
        Assert.Equal("assets/_mx/spinecharacters/nx0001_spr/nx0001_spr.skel.bytes", ModCatalogService.LookupKey("Assets/_MX/SpineCharacters/nx0001_spr/nx0001_spr.skel.bytes"));
    }

    private static string? InstalledCatalog()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "NEXON Games", "Blue Archive", "catalog_Remote.bytes");
        return File.Exists(path) ? path : null;
    }

    private static AddressableCatalog SyntheticCatalog()
    {
        var c = new AddressableCatalog
        {
            LocatorId = "AddressablesMainContentCatalog",
            InstanceProviderData = "{\"m_Id\":\"InstanceProvider\"}",
            SceneProviderData = "{\"m_Id\":\"SceneProvider\"}"
        };
        c.ResourceProviderData.Add("{\"m_Id\":\"AssetBundleProvider\"}");
        c.ProviderIds.Add("UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider");
        c.ProviderIds.Add("UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider");
        c.ResourceTypes.Add("{\"m_AssemblyName\":\"Unity.ResourceManager\",\"m_ClassName\":\"UnityEngine.ResourceManagement.ResourceProviders.IAssetBundleResource\"}");
        c.ResourceTypes.Add("{\"m_AssemblyName\":\"UnityEngine.CoreModule\",\"m_ClassName\":\"UnityEngine.Texture2D\"}");

        var bundleId = c.AddInternalId("http://127.0.0.1:5000/mods/bundles/nx0001.bundle");
        var assetId = c.AddInternalId("Assets/_MX/SpineCharacters/nx0001_spr/nx0001_spr.png");
        var extra = c.AddExtra(CatalogKey.Json7("Unity.ResourceManager", "AssetBundleRequestOptions",
            "{\"m_Hash\":\"\",\"m_Crc\":0,\"m_BundleName\":\"nx0001\",\"m_BundleSize\":4695}"));

        var bundleEntry = c.AddEntry(new CatalogEntry { InternalId = bundleId, ProviderIndex = 0, ExtraIndex = extra, PrimaryKey = 0, ResourceType = 0 });
        var bundleBucket = c.AddBucket(CatalogKey.Ascii("nx0001.bundle"), bundleEntry);
        var assetEntry = c.AddEntry(new CatalogEntry { InternalId = assetId, ProviderIndex = 1, DependencyBucket = bundleBucket, DependencyHash = 12345, PrimaryKey = 1, ResourceType = 1 });

        c.AddBucket(CatalogKey.Ascii("assets/_mx/spinecharacters/nx0001_spr/nx0001_spr.png"), assetEntry);
        c.AddBucket(CatalogKey.Int(unchecked((int)0xdeadbeef)), assetEntry);
        c.AddBucket(new CatalogKey { Type = CatalogObjectType.UnicodeString, Text = "ブルーアーカイブ" }, assetEntry);
        c.AddBucket(new CatalogKey { Type = CatalogObjectType.Hash128, Text = "01f86f47031b8453acb27bc03daf90bf" }, assetEntry);
        c.AddBucket(new CatalogKey { Type = CatalogObjectType.Type, Text = "Sprites/Colored/round" }, assetEntry);
        c.AddBucket(new CatalogKey { Type = CatalogObjectType.UInt16, Number = 4097 }, assetEntry);
        c.AddBucket(new CatalogKey { Type = CatalogObjectType.UInt32, Number = 3908708176 }, assetEntry);

        return c;
    }
}
