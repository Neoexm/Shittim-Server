using Schale.Data.GameModel;
using Schale.FlatData;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class ResetableContentWindowTests
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(60);

    [Fact]
    public void ACounterAccumulatesWithinTheWindow()
    {
        var account = new AccountDBServer();

        ResetableContentService.Bump(account, ResetContentType.StarategyMapHeal, 0, Window);
        ResetableContentService.Bump(account, ResetContentType.StarategyMapHeal, 0, Window);

        Assert.Equal(2, ResetableContentService.LiveValue(account, ResetContentType.StarategyMapHeal, 0, Window));
    }

    [Fact]
    public void ACounterResetsOnceTheWindowPasses()
    {
        var account = new AccountDBServer();
        ResetableContentService.Bump(account, ResetContentType.HardStagePlay, 1111103, Window);

        account.GameSettings.ResetableContents[0].LastUpdateTime =
            account.GameSettings.ServerDateTime() - Window - TimeSpan.FromMinutes(1);

        Assert.Equal(0, ResetableContentService.LiveValue(account, ResetContentType.HardStagePlay, 1111103, Window));
        Assert.Empty(ResetableContentService.LiveValues(account, Window));
    }

    [Fact]
    public void CountersAreKeyedByTypeAndContent()
    {
        var account = new AccountDBServer();

        ResetableContentService.Bump(account, ResetContentType.HardStagePlay, 1111103, Window);
        ResetableContentService.Bump(account, ResetContentType.HardStagePlay, 1222203, Window, delta: 3);

        Assert.Equal(1, ResetableContentService.LiveValue(account, ResetContentType.HardStagePlay, 1111103, Window));
        Assert.Equal(3, ResetableContentService.LiveValue(account, ResetContentType.HardStagePlay, 1222203, Window));
        Assert.Equal(0, ResetableContentService.LiveValue(account, ResetContentType.StarategyMapHeal, 1111103, Window));
    }
}
