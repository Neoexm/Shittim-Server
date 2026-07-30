using Schale.Crypto;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

public class GoodsExcelLayoutTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "TestData", "GoodsRows.txt");

    private static GoodsExcelT Row(string label)
    {
        var line = File.ReadLines(FixturePath)
            .FirstOrDefault(x => x.StartsWith(label + " ", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No row '{label}' in {FixturePath}");
        // ExcelDB rows are stored as plaintext FlatBuffers; only the .bytes files are XOR-obfuscated.
        TableEncryptionService.UseEncryption = false;
        var bytes = Convert.FromBase64String(line[(label.Length + 1)..]);
        return GoodsExcel
            .GetRootAsGoodsExcel(new Google.FlatBuffers.ByteBuffer(bytes))
            .UnPack();
    }

    /// <summary>
    /// The undecoded row blobs, for <see cref="ExcelLayoutDriftTests"/> - it measures the vtable the
    /// writer emitted rather than unpacking through the model, so it needs the bytes and not a GoodsExcelT.
    /// </summary>
    internal static IEnumerable<byte[]> AllRowBytes() => File.ReadLines(FixturePath)
        .Where(line => !line.StartsWith('#') && line.Contains(' '))
        .Select(line => Convert.FromBase64String(line[(line.IndexOf(' ') + 1)..]));

    [Fact]
    public void ApShopGrantsAp()
    {
        // Shop 21's first-tier goods: 30 gems -> 120 AP, matching the captured official purchase.
        var goods = Row("ap_200");

        Assert.EndsWith("Currency_Icon_AP", goods.IconPath);
        Assert.Equal([ParcelType.Currency], goods.ConsumeParcelType);
        Assert.Equal([(long)CurrencyTypes.Gem], goods.ConsumeParcelId);
        Assert.Equal([30L], goods.ConsumeParcelAmount);

        Assert.Equal([ParcelType.Currency], goods.ParcelType);
        Assert.Equal([(long)CurrencyTypes.ActionPoint], goods.ParcelId);
        Assert.Equal([120L], goods.ParcelAmount);
    }

    [Fact]
    public void CashGoodsKeepProductIds()
    {
        // One of the 56 cash rows that populate the six-long product-id block. A 21-slot layout
        // decodes this row's reward triple out of the neighbouring vectors, so it catches that.
        var goods = Row("cash_90030397");

        Assert.Equal(101L, goods.ProductIdAOS);
        Assert.Equal(0L, goods.ProductIdExtra);
        Assert.Equal([20L], goods.ParcelId);
        Assert.Equal([1L], goods.ParcelAmount);
    }

    [Fact]
    public void MultiParcelStaysAligned()
    {
        var goods = Row("multi_1");

        Assert.Equal(2, goods.ParcelType!.Count);
        Assert.Equal([10L, 13L], goods.ParcelId);
        Assert.Equal([9L, 1L], goods.ParcelAmount);
    }
}
