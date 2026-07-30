using Schale.Crypto;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

public class EventContentSeasonLayoutTests
{
    private static EventContentSeasonExcelT Row(string label)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "EventContentSeasonRows.txt");
        var line = File.ReadLines(path)
            .FirstOrDefault(x => x.StartsWith(label + " ", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No row '{label}' in {path}");

        // ExcelDB rows are stored as plaintext FlatBuffers; only the .bytes files are XOR-obfuscated. ExcelTableService flips this the same way before reading either source.
        TableEncryptionService.UseEncryption = false;

        var bytes = Convert.FromBase64String(line[(label.Length + 1)..]);
        return EventContentSeasonExcel
            .GetRootAsEventContentSeasonExcel(new Google.FlatBuffers.ByteBuffer(bytes))
            .UnPack();
    }

    [Fact]
    public void RerunEventsAreDistinguishableFromNormalOnes()
    {
        // The two events official's Mission_List calls actually asked about. 10842 is the rerun of 842, and MissionHandler keys ClearedOrignalMissionIds off exactly these two members, so a wrong decode silently omits a key official sends.
        var rerun = Row("10842");
        Assert.Equal(10842, rerun.EventContentId);
        Assert.Equal(842, rerun.OriginalEventContentId);
        Assert.True(rerun.IsReturn);

        var normal = Row("856");
        Assert.Equal(856, normal.EventContentId);
        Assert.Equal(856, normal.OriginalEventContentId);
        Assert.False(normal.IsReturn);
    }

    [Fact]
    public void FieldsBelowTheOnceMissingSlotStillLandOnTheirOwnValues()
    {
        // Slot 15 was the missing field, so everything from here down sat one slot off. The times have to read as "yyyy-MM-dd HH:mm:ss" in ascending order and the last two as asset paths.
        var row = Row("856");

        Assert.Equal("2026-07-21 11:00:00", row.EventContentOpenTime);
        Assert.Equal("2026-08-03 04:00:00", row.EventContentPhaseCloseTime);
        Assert.Equal("2026-08-04 10:59:59", row.EventContentCloseTime);
        Assert.Equal("2026-08-11 10:59:59", row.ExtensionTime);

        Assert.Equal("UIs/01_Common/27_EventContent/Enter_Parcel/Event_856", row.MainBannerImagePath);
        Assert.Equal("UIs/01_Common/27_EventContent/Main_BgImage/Event_Main_Stage_Bg_856", row.MainBgImagePath);
    }

    [Fact]
    public void NoStringFieldDecodesOutOfANumericSlot()
    {
        // a shifted layout reads a string offset out of a long, giving either a wild offset or bytes that are not text. ShiftMainBgImagePath is the one that throws first.
        foreach (var label in new[] { "856", "10842", "widest_70000" })
        {
            var row = Row(label);

            foreach (var property in typeof(EventContentSeasonExcelT).GetProperties()
                         .Where(p => p.PropertyType == typeof(string)))
            {
                var value = (string?)property.GetValue(row);
                Assert.False(
                    value?.Any(c => char.IsControl(c)) ?? false,
                    $"{label}.{property.Name} decoded to non-text: {value}");
            }
        }
    }

    [Fact]
    public void WidestRowReachesItsLastField()
    {
        // 38 slots is the widest vtable in the table and this is the last of them. It reads as 0 under any layout that is short by a field, so it pins the total field count as well.
        var row = Row("widest_70000");

        Assert.Equal(70000, row.EventContentId);
        Assert.Equal(1, row.ScenarioContentCollectionGroupId);
        Assert.Equal(EventContentType.SpecialMiniEvent, row.EventContentType);
    }
}
