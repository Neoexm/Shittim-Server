using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;
using Xunit.Abstractions;

namespace Shittim_Server.Tests;

public class RecoveredExcelLayoutTests
{
    private readonly ITestOutputHelper output;

    public RecoveredExcelLayoutTests(ITestOutputHelper output) => this.output = output;

    // ShopCashExcel's 25 data slots were being read as 20 consecutive ones, so IconPath decoded off a 32-bit hash, UnPack threw, and all 1345 rows got dropped - every cash-shop lookup returned null.
    // Id and CashProductId are the only two fields any handler reads, and CashProductId is a foreign key into ProductExcel, so a wrong slot would have to land on values that are all valid product ids. Slot 1 is 4 bytes among 4-byte neighbours; nothing structural tells them apart.
    [Fact]
    public void ShopCashCashProductIdJoinsToProductExcel()
    {
        if (Service is null)
        {
            SkipNote();
            return;
        }

        var shopCash = Service.GetTable<ShopCashExcelT>(false, true);
        var productIds = Service.GetTable<ProductExcelT>(false, true).Select(p => p.Id).ToHashSet();

        Assert.NotEmpty(shopCash);
        Assert.NotEmpty(productIds);

        var orphans = shopCash.Where(s => !productIds.Contains(s.CashProductId)).ToList();

        output.WriteLine($"{shopCash.Count} ShopCashExcel row(s), " +
                         $"{shopCash.Count - orphans.Count} resolve into ProductExcel's {productIds.Count} id(s)");

        Assert.True(orphans.Count == 0,
            $"{orphans.Count} of {shopCash.Count} ShopCashExcel rows carry a CashProductId that is not a " +
            "ProductExcel id. Either the recovered slot for CashProductId is wrong - it is a 4-byte field " +
            "among 4-byte neighbours, so the structural checks cannot tell them apart - or a client build " +
            "moved it. First few: " +
            string.Join(", ", orphans.Take(5).Select(s => $"Id {s.Id} -> CashProductId {s.CashProductId}")));
    }

    // GroundExcel writes 59 slots and the model declared 58 consecutively, so from data slot 54 on every field read its neighbour; a string among them threw and took 227 of 4424 rows with it.
    // The float trio at slots 50-52 pinned the omission to slot 54 - the rows hold the exact bit pattern of 0.85f there, and one slot over an int reinterpreted as a float gives a denormal around 1.19e-38, which no UI scale ever is.
    // The four shifted fields are two parallel id/level pairs, so a one-slot shift points the string vector at the int vector beside it and the lengths stop matching even where nothing throws.
    [Fact]
    public void GroundExcelDecodesPlausibleBattleMapValues()
    {
        if (Service is null)
        {
            SkipNote();
            return;
        }

        var ground = Service.GetTable<GroundExcelT>(false, true);
        Assert.NotEmpty(ground);

        Assert.All(ground, g => Assert.True(g.Id > 0, $"GroundExcel row has a non-positive Id: {g.Id}"));

        foreach (var g in ground)
        {
            foreach (var (name, scale) in new[]
                     {
                         (nameof(g.UIHpScale), g.UIHpScale),
                         (nameof(g.UIEmojiScale), g.UIEmojiScale),
                         (nameof(g.UISkillMainLogScale), g.UISkillMainLogScale),
                     })
            {
                // Zero is the FlatBuffers default, i.e. the row left the field out.
                Assert.True(scale == 0f || (scale > 0.001f && scale < 1000f),
                    $"GroundExcel row {g.Id} decoded {name} as {scale:E}, which is not a UI scale - a " +
                    "denormal here is the signature of reading one slot away from where the data writes it.");
            }

            Assert.Equal(g.AllyPassiveSkillId?.Count ?? 0, g.AllyPassiveSkillLevel?.Count ?? 0);
            Assert.Equal(g.EnemyPassiveSkillId?.Count ?? 0, g.EnemyPassiveSkillLevel?.Count ?? 0);
        }

        output.WriteLine($"{ground.Count} GroundExcel row(s) loaded; " +
                         $"{ground.Count(g => g.AllyPassiveSkillId is { Count: > 0 })} carry ally passive skills, " +
                         $"{ground.Count(g => g.EnemyPassiveSkillId is { Count: > 0 })} carry enemy passive skills");
    }

    // Stays null without the dumps; both tests skip then.
    private static readonly ExcelTableService? Service = BuildService();

    private static ExcelTableService? BuildService()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "Shittim-Server", "Shittim-Server.csproj")))
                continue;

            var server = Path.Combine(dir.FullName, "Shittim-Server");

            // Under a test run AppContext.BaseDirectory is the test project's output, not the server's, so GetTable has to be pointed at the server's copy.
            var dumped = new[]
            {
                Path.Combine(server, "Resources", "Dumped"),
                Path.Combine(server, "bin", "Debug", "net10.0", "Resources", "Dumped"),
                Path.Combine(server, "bin", "Release", "net10.0", "Resources", "Dumped"),
            }.FirstOrDefault(Directory.Exists);

            if (dumped is null)
                return null;

            ExcelTableService.DumpedDir = dumped;
            return new ExcelTableService();
        }

        return null;
    }

    private void SkipNote() => output.WriteLine(
        "No Resources/Dumped found, so nothing was compared. The excel dumps live in the server's build " +
        "output rather than the repo; run the server once to download them, then re-run this test.");
}
