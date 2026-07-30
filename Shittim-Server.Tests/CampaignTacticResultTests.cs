using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.BattlesEntities;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class CampaignTacticResultTests
{
    private static HeroSummary Hero(
        long serverId, int entityId, long hpRateAfter, int deadFrame = -1, long hpRateBefore = 10000) => new()
    {
        ServerId = serverId,
        BattleEntityId = new EntityId { uniqueId = entityId },
        HPRateBefore = hpRateBefore,
        HPRateAfter = hpRateAfter,
        DeadFrame = deadFrame,
    };

    // A Special who was never called in - how real clients report an unused support slot, taken from the official 6008 request rather than its response.
    private static HeroSummary OffFieldSpecial(long serverId, int entityId) =>
        Hero(serverId, entityId, hpRateAfter: 0, deadFrame: -1, hpRateBefore: 0);

    private static HeroSummaryCollection Collection(params HeroSummary[] heroes)
    {
        var collection = new HeroSummaryCollection();
        collection.Add(heroes);
        return collection;
    }

    // Official 6008 #2: four strikers and two supporters, all alive, none dying.
    private static BattleSummary WonBattle(int endFrame = 1872) => new()
    {
        StageId = 1161101,
        IsAbort = false,
        EndType = BattleEndType.Clear,
        EndFrame = endFrame,
        Group01Summary = new GroupSummary
        {
            TeamId = 1,
            Heroes = Collection(
                Hero(198100259, 16777217, 10000),
                Hero(243235919, 16777218, 9655),
                Hero(196716573, 16777219, 9788),
                Hero(196717556, 16777220, 7757)),
            Supporters = Collection(
                OffFieldSpecial(249736115, 16777250),
                OffFieldSpecial(191445197, 16777251)),
        },
        Group02Summary = new GroupSummary
        {
            TeamId = 2,
            Heroes = Collection(Hero(0, 16777221, 0, deadFrame: 212)),
            // The enemy group has no supporters, so HpInfos cannot be read from here.
            Supporters = Collection(),
        },
    };

    [Fact]
    public void WonBattleRanksAboveZeroSoTheSkipPathReadsItAsAWin()
    {
        Assert.True(ConcentrateCampaignManager.CalcTacticRank(WonBattle()) > 0);
    }

    [Fact]
    public void CleanQuickWinScoresThreeLikeOfficial()
    {
        // All four official results were wins inside the par time with nobody downed, and every one of them came back as 3.
        Assert.Equal(3, ConcentrateCampaignManager.CalcTacticRank(WonBattle()));
    }

    [Fact]
    public void WinOverParTimeLosesARank()
    {
        // 120s par. Frames resolve to whole seconds, so 3600 (exactly 120s) still makes par and it takes another full second to drop a rank.
        Assert.Equal(3, ConcentrateCampaignManager.CalcTacticRank(WonBattle(endFrame: 3600)));
        Assert.Equal(2, ConcentrateCampaignManager.CalcTacticRank(WonBattle(endFrame: 3630)));
    }

    [Fact]
    public void WinWithACasualtyLosesARank()
    {
        var summary = WonBattle();
        summary.Group01Summary!.Heroes![3].DeadFrame = 900;
        summary.Group01Summary.Heroes[3].HPRateAfter = 0;

        Assert.Equal(2, ConcentrateCampaignManager.CalcTacticRank(summary));
    }

    [Fact]
    public void DefeatWithCasualtiesRanksZero()
    {
        var summary = WonBattle();
        summary.EndType = BattleEndType.AllNearlyDead;
        foreach (var hero in summary.Group01Summary!.Heroes!)
        {
            hero.DeadFrame = 900;
            hero.HPRateAfter = 0;
        }

        Assert.Equal(0, ConcentrateCampaignManager.CalcTacticRank(summary));
    }

    [Fact]
    public void AbortRanksZero()
    {
        var summary = WonBattle();
        summary.IsAbort = true;
        summary.Group01Summary!.Heroes![0].DeadFrame = 900;

        Assert.Equal(0, ConcentrateCampaignManager.CalcTacticRank(summary));
    }

    [Fact]
    public void HpInfosKeepsEverySurvivorIncludingTheEchelonsOwnSupporters()
    {
        var group01 = WonBattle().Group01Summary!;

        var (hpInfos, _) = ConcentrateCampaignManager.ChangeHpInfos(group01.Heroes, group01.Supporters);

        // Official's HpInfos is a stable six-member map - four strikers plus two Specials.
        Assert.Equal(6, hpInfos.Count);
        Assert.Equal(9655, hpInfos[243235919]);
    }

    [Fact]
    public void DyingInfosIsEmptyAfterAWinWithNoCasualties()
    {
        var group01 = WonBattle().Group01Summary!;

        var (_, dyingInfos) = ConcentrateCampaignManager.ChangeHpInfos(group01.Heroes, group01.Supporters);

        Assert.Empty(dyingInfos);
    }

    [Fact]
    public void SpecialsWhoNeverTookTheFieldAreNotCasualties()
    {
        // both Specials come back at 0/0 with DeadFrame -1. reading that as death files the whole support slot under DyingInfos; official answers this request with DyingInfos {} and both of them in HpInfos at 10000.
        var group01 = WonBattle().Group01Summary!;

        var (hpInfos, dyingInfos) = ConcentrateCampaignManager.ChangeHpInfos(group01.Heroes, group01.Supporters);

        Assert.Empty(dyingInfos);
        Assert.Equal(10000, hpInfos[249736115]);
        Assert.Equal(10000, hpInfos[191445197]);
    }

    [Fact]
    public void AnUntouchedStudentKeepsWhatTheLedgerAlreadyHeld()
    {
        // HpInfos spans the mission, not the battle. Official's echelon 1 still reads 9647 for one student three tactic results after the only battle it fought.
        var group01 = WonBattle().Group01Summary!;
        var existing = new Dictionary<long, long> { [249736115] = 9647 };

        var (hpInfos, _) = ConcentrateCampaignManager.ChangeHpInfos(
            group01.Heroes, group01.Supporters, existing);

        Assert.Equal(9647, hpInfos[249736115]);
    }

    [Fact]
    public void DyingInfosCarriesOnlyTheStudentsWhoWereActuallyDowned()
    {
        var group01 = WonBattle().Group01Summary!;
        group01.Heroes![3].HPRateAfter = 0;
        group01.Heroes[3].DeadFrame = 900;

        var (hpInfos, dyingInfos) = ConcentrateCampaignManager.ChangeHpInfos(group01.Heroes, group01.Supporters);

        Assert.Equal(new long[] { 196717556 }, dyingInfos.Keys);
        // A downed student belongs to exactly one of the two maps.
        Assert.DoesNotContain(196717556, hpInfos.Keys);
        Assert.Equal(5, hpInfos.Count);
    }

    [Fact]
    public void AStudentDownedEarlierDoesNotStandBackUpInTheNextBattle()
    {
        // A downed student still rides along in later summaries as an untouched slot. Taking that at face value would quietly revive them at full HP.
        var group01 = WonBattle().Group01Summary!;
        var alreadyDown = new Dictionary<long, long> { [249736115] = 0 };

        var (hpInfos, dyingInfos) = ConcentrateCampaignManager.ChangeHpInfos(
            group01.Heroes, group01.Supporters, existingDyingInfos: alreadyDown);

        Assert.Contains(249736115, dyingInfos.Keys);
        Assert.DoesNotContain(249736115, hpInfos.Keys);
    }
}
