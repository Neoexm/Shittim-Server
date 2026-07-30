using Schale.Data.GameModel;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Xunit;

namespace Shittim_Server.Tests;

/// <summary>
/// Mission_List against the 2026-07-28 capture pair. The mission screen renders from the
/// login-cached response and never asks again, so a single unresolvable id in MissionHistoryUniqueIds
/// or ProgressDBs blanks it with nothing on the wire to explain why. The two ids that get in there
/// are CampaignStageHistory.StoryUniqueId (0 on every row we write) and battle-pass rows, which share
/// the MissionProgresses table - BattlePassMissionExcel ids 2000001+.
/// </summary>
public class MissionListWireTests
{
    private static readonly HashSet<long> MissionIds = [1000, 1200, 1513, 82000, 110000];
    private static readonly HashSet<long> GuideOrEventIds = [1000210, 1000220, 856001];

    [Fact]
    public void AccountWideHistoryIsNonMissionClaimsThenAllClaims()
    {
        // Official's account-wide list: 953 entries = 875 distinct claims + a second copy of the
        // 78 guide claims; the guide block leads. Replicate that shape.
        var claims = new List<long> { 1513, 1000210, 82000, 1000220 };

        var ids = MissionHandler.BuildMissionHistoryIds(claims, MissionIds, GuideOrEventIds, eventScoped: false);

        Assert.Equal(new List<long> { 1000210, 1000220, 1513, 1000210, 82000, 1000220 }, ids);
    }

    [Fact]
    public void EventScopedHistoryIsOnlyNonMissionClaims()
    {
        // Official's event-scoped list is exactly the claims that do not resolve in MissionExcel
        // (their 78 guide claims; a claimed event mission would land here the same way).
        var claims = new List<long> { 1513, 1000210, 856001, 82000 };

        var ids = MissionHandler.BuildMissionHistoryIds(claims, MissionIds, GuideOrEventIds, eventScoped: true);

        Assert.Equal(new List<long> { 1000210, 856001 }, ids);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnresolvableClaimIdsNeverReachTheWire(bool eventScoped)
    {
        // unlockall writes CampaignStageHistories with StoryUniqueId=0, so projecting that column
        // straight into the history list puts a literal 0 on the wire that the client cannot
        // resolve. any id no excel knows gets dropped rather than forwarded.
        var claims = new List<long> { 0, 1513, 999999999 };

        var ids = MissionHandler.BuildMissionHistoryIds(claims, MissionIds, GuideOrEventIds, eventScoped);

        Assert.DoesNotContain(0, ids);
        Assert.DoesNotContain(999999999, ids);
    }

    [Fact]
    public void EmptyExcelTablesDegradeOpenForTheAccountWideList()
    {
        // A dump/schema failure empties the excel tables; blanking the whole history would turn a
        // data problem into "everything unclaimed". Pass claims through untouched instead.
        var claims = new List<long> { 1513, 82000 };

        var ids = MissionHandler.BuildMissionHistoryIds(claims, [], [], eventScoped: false);

        Assert.Equal(claims, ids);
        Assert.Empty(MissionHandler.BuildMissionHistoryIds(claims, [], [], eventScoped: true));
    }

    [Fact]
    public void ProgressFilterDropsBattlePassAndEventRows()
    {
        // MissionProgresses stores mission, guide, battle-pass and event rows side by side; only
        // the first two may appear in mission-screen payloads (Mission_List / Mission_Sync /
        // Account_Auth). Official's account-wide ProgressDBs carries no 2000001+ or 856xxx ids.
        var progresses = new List<MissionProgressDBServer>
        {
            new() { MissionUniqueId = 1513 },     // mission
            new() { MissionUniqueId = 1000210 },  // guide
            new() { MissionUniqueId = 2000001 },  // battle-pass
            new() { MissionUniqueId = 856001 },   // event content
        };
        var validIds = new HashSet<long> { 1513, 1000210 };

        var filtered = MissionHandler.FilterMissionScreenProgresses(progresses, validIds);

        Assert.Equal(new long[] { 1513, 1000210 }, filtered.Select(p => p.MissionUniqueId));
    }

    [Fact]
    public void ProgressFilterDegradesOpenWhenExcelIsEmpty()
    {
        var progresses = new List<MissionProgressDBServer> { new() { MissionUniqueId = 1513 } };

        var filtered = MissionHandler.FilterMissionScreenProgresses(progresses, []);

        Assert.Same(progresses, filtered);
    }
}
