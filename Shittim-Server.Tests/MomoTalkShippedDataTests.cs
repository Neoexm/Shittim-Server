using BlueArchiveAPI.Services;
using Schale.FlatData;
using Xunit;

namespace Shittim_Server.Tests;

// The chaining tests next door run on a hand-built fixture. These run the same two functions over the real
// AcademyMessangerExcel, so the assumptions they encode - the opening group is the lowest id, its FavorRankUp
// gate is on the first row, follow-up conversations hang off NextGroupId behind a rank gate - stay checked
// against the shipped table rather than against my reading of it.
// Skipped when no ExcelDB.db is available (CI, or a checkout without the client data). The skip is decided by
// the attribute so the runner reports these as skipped rather than green-while-asserting-nothing; a present but
// unreadable database (rotated SQLCipher key) fails instead of passing silently.
public class MomoTalkShippedDataTests
{
    private sealed class ShippedDataFactAttribute : FactAttribute
    {
        public ShippedDataFactAttribute()
        {
            var dumped = DumpedDir();
            if (dumped == null || !File.Exists(Path.Combine(dumped, "ExcelDB.db")))
                Skip = "Shipped ExcelDB.db not available in this checkout";
        }
    }

    [ShippedDataFact]
    public void EveryStudentsConversationOpensAtTheLowestGroupBehindItsRankGate()
    {
        var messengers = ShippedMessengers();

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

    [ShippedDataFact]
    public void ARankGateIsAlwaysTheFirstRowOfTheGroupItGates()
    {
        // RankUnlockedGroup and the handler's own gate both read the condition off the group's opening row.
        var messengers = ShippedMessengers();

        foreach (var group in messengers.GroupBy(x => x.MessageGroupId))
        {
            var rows = group.OrderBy(x => x.Id).ToList();
            if (rows.Any(x => x.MessageCondition == AcademyMessageConditions.FavorRankUp))
                Assert.Equal(AcademyMessageConditions.FavorRankUp, rows[0].MessageCondition);
        }
    }

    [ShippedDataFact]
    public void AStudentsConversationsAreReachableOneRankGateAtATime()
    {
        var messengers = ShippedMessengers();

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

    private static string? DumpedDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        if (dir == null)
            return null;

        return new[]
        {
            Path.Combine(dir, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(dir, "Shittim-Server", "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.FirstOrDefault(Directory.Exists);
    }

    private static List<AcademyMessangerExcelT> ShippedMessengers()
    {
        ExcelTableService.DumpedDir = DumpedDir()!;

        // Empty means the table degraded - the SQLCipher key rotated or the dump is truncated. The attribute
        // already skipped when the file is absent, so degradation here is a failure, not a skip.
        var messengers = new ExcelTableService().GetTable<AcademyMessangerExcelT>();
        Assert.NotEmpty(messengers);
        return messengers;
    }
}
