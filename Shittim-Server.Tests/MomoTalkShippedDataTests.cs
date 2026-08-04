using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

// The chaining tests next door run on a hand-built fixture. These run the same two functions over the real
// AcademyMessangerExcel, so the assumptions they encode - the opening group is the lowest id, its FavorRankUp
// gate is on the first row, follow-up conversations hang off NextGroupId behind a rank gate - stay checked
// against the shipped table rather than against my reading of it.
// Skipped when no ExcelDB.db is available (CI, or a checkout without the client data).
public class MomoTalkShippedDataTests
{
    [Fact]
    public void EveryStudentsConversationOpensAtTheLowestGroupBehindItsRankGate()
    {
        var messengers = ShippedMessengers();
        if (messengers == null) return;

        var students = messengers.Where(x => x.CharacterId > 0).GroupBy(x => x.CharacterId).ToList();
        Assert.NotEmpty(students);

        foreach (var student in students)
        {
            var groups = student.GroupBy(x => x.MessageGroupId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).ToList());
            var lowest = groups.Keys.Min();

            // Nothing points at the opening group, so it is the one to seed an outline with.
            var incoming = student.Where(x => x.NextGroupId > 0).Select(x => x.NextGroupId).ToHashSet();
            Assert.Equal([lowest], groups.Keys.Where(k => !incoming.Contains(k)).OrderBy(k => k).ToList());

            // Its gate decides whether the student has a conversation at all yet.
            var gate = groups[lowest][0];
            Assert.Equal(AcademyMessageConditions.FavorRankUp, gate.MessageCondition);
            Assert.Equal(0, MomoTalkService.OpeningGroup(messengers, student.Key, gate.ConditionValue - 1));
            Assert.Equal(lowest, MomoTalkService.OpeningGroup(messengers, student.Key, gate.ConditionValue));
        }
    }

    [Fact]
    public void ARankGateIsAlwaysTheFirstRowOfTheGroupItGates()
    {
        // RankUnlockedGroup and the handler's own gate both read the condition off the group's opening row.
        var messengers = ShippedMessengers();
        if (messengers == null) return;

        foreach (var group in messengers.GroupBy(x => x.MessageGroupId))
        {
            var rows = group.OrderBy(x => x.Id).ToList();
            if (rows.Any(x => x.MessageCondition == AcademyMessageConditions.FavorRankUp))
                Assert.Equal(AcademyMessageConditions.FavorRankUp, rows[0].MessageCondition);
        }
    }

    [Fact]
    public void AStudentsConversationsAreReachableOneRankGateAtATime()
    {
        var messengers = ShippedMessengers();
        if (messengers == null) return;

        var student = messengers.Where(x => x.CharacterId > 0).GroupBy(x => x.CharacterId).OrderBy(x => x.Key).First();

        var group = MomoTalkService.OpeningGroup(messengers, student.Key, 99);
        Assert.NotEqual(0, group);

        var gatesCrossed = 0;
        for (var step = 0; step < 500; step++)
        {
            var next = messengers
                .Where(x => x.MessageGroupId == group && x.NextGroupId > 0 && x.NextGroupId != group)
                .Select(x => x.NextGroupId)
                .FirstOrDefault();
            if (next == 0) break;

            // Every gate on the way is one the old code stopped at for good; RankUnlockedGroup has to find each.
            var opening = messengers.Where(x => x.MessageGroupId == next).OrderBy(x => x.Id).First();
            if (opening.MessageCondition == AcademyMessageConditions.FavorRankUp)
            {
                Assert.Equal(next, MomoTalkService.RankUnlockedGroup(messengers, group, 99));
                Assert.Equal(0, MomoTalkService.RankUnlockedGroup(messengers, group, opening.ConditionValue - 1));
                gatesCrossed++;
            }
            else
            {
                // An ordinary continuation is the client's job, never pushed from here.
                Assert.Equal(0, MomoTalkService.RankUnlockedGroup(messengers, group, 99));
            }

            group = next;
        }

        Assert.True(gatesCrossed > 0, "expected at least one rank-gated conversation on the sample student");
    }

    private static List<AcademyMessangerExcelT>? ShippedMessengers()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        var dumped = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.FirstOrDefault(Directory.Exists);

        if (dumped == null)
            return null;

        ExcelTableService.DumpedDir = dumped;

        // Empty means the table degraded - no ExcelDB.db, or a SQLCipher key that has rotated.
        var messengers = new ExcelTableService().GetTable<AcademyMessangerExcelT>();
        return messengers.Count > 0 ? messengers : null;
    }
}
