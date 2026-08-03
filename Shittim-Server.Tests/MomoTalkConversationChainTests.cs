using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

public class MomoTalkConversationChainTests
{
    // Shaped after the live AcademyMessangerExcel rows for character 10004: groups are numbered in reading order and
    // chained end to end by NextGroupId, and a new conversation is marked by a FavorRankUp condition on the first
    // row of the group it starts at (100040010 at rank 2, 100040090 at rank 3).
    private static readonly List<AcademyMessangerExcelT> Messengers =
    [
        Row(id: 1, group: 100040010, next: 100040011, condition: AcademyMessageConditions.FavorRankUp, conditionValue: 2),
        Row(id: 1, group: 100040011, next: 100040020),
        Row(id: 1, group: 100040020, next: 100040090),
        Row(id: 1, group: 100040090, next: 100040100, condition: AcademyMessageConditions.FavorRankUp, conditionValue: 3),
        Row(id: 2, group: 100040090, next: 100040100),
        Row(id: 1, group: 100040100, next: 0),
    ];

    [Fact]
    public void AStudentOpensOnTheirLowestGroupOnceItsRankIsReached()
    {
        Assert.Equal(0, MomoTalkService.OpeningGroup(Messengers, characterId: 10004, favorRank: 1));
        Assert.Equal(100040010, MomoTalkService.OpeningGroup(Messengers, characterId: 10004, favorRank: 2));
    }

    [Fact]
    public void AnotherStudentGetsNothing()
    {
        Assert.Equal(0, MomoTalkService.OpeningGroup(Messengers, characterId: 20000, favorRank: 99));
    }

    [Fact]
    public void AConversationStoppedAtARankGateResumesOnceTheRankIsThere()
    {
        Assert.Equal(0, MomoTalkService.RankUnlockedGroup(Messengers, currentGroupId: 100040020, favorRank: 2));
        Assert.Equal(100040090, MomoTalkService.RankUnlockedGroup(Messengers, currentGroupId: 100040020, favorRank: 3));
    }

    [Fact]
    public void AnOrdinaryContinuationIsLeftToTheClient()
    {
        // 100040011 follows 100040010 with no gate, so the player reads their way to it through MomoTalk_Read;
        // pushing it here would spill messages they have not reached.
        Assert.Equal(0, MomoTalkService.RankUnlockedGroup(Messengers, currentGroupId: 100040010, favorRank: 99));
    }

    [Fact]
    public void TheLastGroupHasNothingAfterIt()
    {
        Assert.Equal(0, MomoTalkService.RankUnlockedGroup(Messengers, currentGroupId: 100040100, favorRank: 99));
    }

    private static AcademyMessangerExcelT Row(
        long id,
        long group,
        long next,
        AcademyMessageConditions condition = AcademyMessageConditions.None,
        long conditionValue = 0) => new()
        {
            Id = id,
            MessageGroupId = group,
            CharacterId = 10004,
            NextGroupId = next,
            MessageCondition = condition,
            ConditionValue = conditionValue
        };
}
