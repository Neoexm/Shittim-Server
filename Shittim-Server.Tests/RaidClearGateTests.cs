using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// A raid row exists from the moment its battle is created and EndBossBattle writes Clear even for a give-up,
// so the reward and sweep paths gate on the bosses being dead rather than on RaidState.
public class RaidClearGateTests
{
    private static RaidDBServer Raid(params long[] bossHp) => new()
    {
        RaidBossDBs = bossHp.Select((hp, i) => new RaidBossDBServer { BossIndex = i, BossCurrentHP = hp }).ToList()
    };

    [Fact]
    public void AFreshRaidIsNotCleared()
    {
        Assert.False(RaidService.IsCleared(Raid(10_000_000)));
    }

    [Fact]
    public void ARaidWithOneBossStandingIsNotCleared()
    {
        Assert.False(RaidService.IsCleared(Raid(0, 0, 4200)));
    }

    [Fact]
    public void EveryBossDeadIsCleared()
    {
        Assert.True(RaidService.IsCleared(Raid(0, 0, 0)));
    }

    [Fact]
    public void ARaidWithNoBossRowsIsNotCleared()
    {
        // An empty boss list would otherwise pass All() vacuously and make every raid rewardable.
        Assert.False(RaidService.IsCleared(Raid()));
    }
}
