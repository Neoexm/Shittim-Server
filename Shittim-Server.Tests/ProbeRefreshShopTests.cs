using System.Text;
using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

public class ProbeRefreshShopTests
{
    // ShopRefreshExcel only carries a GoodsId; the cost and the reward both come from GoodsExcel. A row whose GoodsId does not resolve builds a shop slot that sells nothing, which is what a mismatched excel dump looks like from the client side.
    [Fact]
    public void EveryRefreshRowResolvesToAGoodsRow()
    {
        var excels = Excels();
        var refresh = excels.GetTable<ShopRefreshExcelT>();
        var goodsIds = excels.GetTable<GoodsExcelT>().Select(x => x.Id).ToHashSet();

        var unresolved = refresh.Where(x => !goodsIds.Contains(x.GoodsId)).ToList();

        var sb = new StringBuilder();
        foreach (var r in unresolved.Take(20))
            sb.AppendLine($"  refresh {r.Id} cat={r.CategoryType} group={r.RefreshGroup} goods={r.GoodsId}");

        Assert.True(unresolved.Count == 0, $"{unresolved.Count} of {refresh.Count} ShopRefreshExcel rows have an unresolved GoodsId\n{sb}");
    }

    [Fact]
    public void RefreshRowsCoverTheFourRefreshableCategories()
    {
        var refresh = Excels().GetTable<ShopRefreshExcelT>();
        var categories = refresh.Select(x => x.CategoryType).Distinct().OrderBy(x => (int)x);

        Assert.Equal(
            [ShopCategoryType.General, ShopCategoryType.Arena, ShopCategoryType.GemDaily, ShopCategoryType.GemWeekly],
            categories);
    }

    private static ExcelTableService Excels()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        ExcelTableService.DumpedDir = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.First(Directory.Exists);

        return new ExcelTableService();
    }
}
