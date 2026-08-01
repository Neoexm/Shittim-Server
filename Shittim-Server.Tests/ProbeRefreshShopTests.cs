using System.Text;
using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

public class ProbeRefreshShopTests
{
    [Fact]
    public void Dump()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        ExcelTableService.DumpedDir = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
        }.First(Directory.Exists);

        var excels = new ExcelTableService();
        var refresh = excels.GetTable<ShopRefreshExcelT>();
        var shops = excels.GetTable<ShopExcelT>();
        var goods = excels.GetTable<GoodsExcelT>();
        var goodsIds = goods.Select(x => x.Id).ToHashSet();

        var sb = new StringBuilder();
        sb.AppendLine($"ShopRefreshExcel rows: {refresh.Count}, unresolved GoodsId: {refresh.Count(x => !goodsIds.Contains(x.GoodsId))}");
        foreach (var g in refresh.GroupBy(x => x.CategoryType))
            sb.AppendLine($"refresh category {g.Key}: {g.Count()} rows, id range {g.Min(x => x.Id)}..{g.Max(x => x.Id)}");

        var shopIds = shops.Select(x => x.Id).ToHashSet();
        var refreshIds = refresh.Select(x => x.Id).ToHashSet();
        sb.AppendLine($"refresh ids also in ShopExcel: {refreshIds.Count(shopIds.Contains)}");

        foreach (var g in shops.GroupBy(x => x.CategoryType).OrderBy(x => (int)x.Key))
            sb.AppendLine($"shop category {g.Key}: {g.Count()} rows");

        foreach (var r in refresh.Take(4))
        {
            var g = goods.FirstOrDefault(x => x.Id == r.GoodsId);
            sb.AppendLine($"refresh {r.Id} cat={r.CategoryType} group={r.RefreshGroup} prob={r.Prob} goods={r.GoodsId} consume=[{string.Join(",", (g?.ConsumeParcelType ?? new()).Zip(g?.ConsumeParcelId ?? new(), (t, i) => $"{t}:{i}").Zip(g?.ConsumeParcelAmount ?? new(), (p, a) => $"{p}x{a}"))}] reward=[{string.Join(",", (g?.ParcelType ?? new()).Zip(g?.ParcelId ?? new(), (t, i) => $"{t}:{i}").Zip(g?.ParcelAmount ?? new(), (p, a) => $"{p}x{a}"))}]");
        }

        Assert.Fail(sb.ToString());
    }
}
