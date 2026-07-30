using Schale.Data.GameModel;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Xunit;

namespace Shittim_Server.Tests;

public class MailListWindowTests
{
    // The three official rows: 70527403@22:03:12, 70527400@22:03:00, 70527395@22:02:53.
    private static List<MailDBServer> OfficialBox() =>
    [
        new() { ServerId = 70527395, SendDate = new DateTime(2026, 7, 28, 22, 2, 53) },
        new() { ServerId = 70527403, SendDate = new DateTime(2026, 7, 28, 22, 3, 12) },
        new() { ServerId = 70527400, SendDate = new DateTime(2026, 7, 28, 22, 3, 0) },
    ];

    [Fact]
    public void FirstPage_Sentinel_ReturnsAllNewestFirst()
    {
        var window = MailHandler.ApplyListWindow(
            OfficialBox(), DateTime.Parse("9999-12-31T23:59:59"), isDescending: true);

        Assert.Equal(new long[] { 70527403, 70527400, 70527395 }, window.Select(m => m.ServerId));
    }

    [Fact]
    public void Pivot_BoundsInclusively()
    {
        // Official's page 2 passed the last row's own SendDate and got that row back.
        var window = MailHandler.ApplyListWindow(
            OfficialBox(), new DateTime(2026, 7, 28, 22, 2, 53), isDescending: true);

        Assert.Equal(new long[] { 70527395 }, window.Select(m => m.ServerId));
    }

    [Fact]
    public void Pivot_ComparesAtSeconds()
    {
        // Rows are stored with sub-second precision but SendDate crosses the wire truncated to
        // seconds, and the client echoes the truncated value back as the pivot. A strict
        // comparison made the pivot row exclude itself and page 2 come back empty (live repro).
        var box = new List<MailDBServer>
        {
            new() { ServerId = 1, SendDate = new DateTime(2026, 7, 27, 14, 11, 19).AddTicks(5378123) },
        };

        var window = MailHandler.ApplyListWindow(
            box, new DateTime(2026, 7, 27, 14, 11, 19), isDescending: true);

        Assert.Single(window);
    }

    [Fact]
    public void Pivot_Unset_BoundsNothing()
    {
        var window = MailHandler.ApplyListWindow(OfficialBox(), default, isDescending: true);

        Assert.Equal(3, window.Count);
    }

    [Fact]
    public void Ascending_OldestFirst_BoundsFromBelow()
    {
        var window = MailHandler.ApplyListWindow(
            OfficialBox(), new DateTime(2026, 7, 28, 22, 3, 0), isDescending: false);

        Assert.Equal(new long[] { 70527400, 70527403 }, window.Select(m => m.ServerId));
    }
}
