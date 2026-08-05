using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Newtonsoft.Json;
using Schale.FlatData;
using Xunit;
using Xunit.Abstractions;

namespace Shittim_Server.Tests;

// The compiled FlatData models chase a moving target: a client update that inserts a field mid-table shifts every later slot, and the generated readers then decode neighbours' data with no error anywhere.
// ClientExcelSchema reads the current slot order out of the installed client's global-metadata.dat and RealignedRowReader re-reads drifted ExcelDB tables at the right slots.
public class ClientExcelSchemaTests
{
    private readonly ITestOutputHelper output;
    public ClientExcelSchemaTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void AnInsertedFieldShiftsEveryLaterSlot()
    {
        var ours = new[] { "Id", "Name", "Amount", "Tags" };
        var client = new List<string> { "Id", "Inserted", "Name", "Amount", "TagsLength" };

        Assert.Equal(new[] { 0, 2, 3, 4 }, ClientExcelSchema.Align(ours, client));
    }

    [Fact]
    public void ARenamedFieldKeepsItsSlot()
    {
        var ours = new[] { "Id", "AccountType", "Title" };
        var client = new List<string> { "Id", "TargetGroup", "Title" };

        Assert.Equal(new[] { 0, 1, 2 }, ClientExcelSchema.Align(ours, client));
    }

    [Fact]
    public void ARemovedFieldDropsWithoutShiftingItsSurvivors()
    {
        var ours = new[] { "Id", "Gone", "AlsoGone", "Name" };
        var client = new List<string> { "Id", "Name" };

        Assert.Equal(new[] { 0, -1, -1, 1 }, ClientExcelSchema.Align(ours, client));
    }

    [Fact]
    public void AnUnknownPlaceholderPairsWithTheClientsNameAtTheSamePosition()
    {
        // RaidStageExcel: the model has UnknownSlot14 where the client says RaidBossGroupType
        var ours = new[] { "Id", "UnknownSlot1", "Score" };
        var client = new List<string> { "Id", "RaidBossGroupType", "Score" };

        Assert.Equal(new[] { 0, 1, 2 }, ClientExcelSchema.Align(ours, client));
    }

    [Fact]
    public void RealignedReaderDecodesAShiftedRow()
    {
        // a row written with a field our model does not have at slot 1, everything after shifted by one
        var builder = new FlatBufferBuilder(64);
        var name = builder.CreateString("Serika");
        var tags = new long[] { 7, 8 };
        builder.StartVector(8, tags.Length, 8);
        for (var i = tags.Length - 1; i >= 0; i--) builder.AddLong(tags[i]);
        var tagsVector = builder.EndVector();

        builder.StartTable(5);
        builder.AddLong(0, 4021, 0);
        builder.AddLong(1, 999, 0);
        builder.AddOffset(2, name.Value, 0);
        builder.AddInt(3, 55, 0);
        builder.AddOffset(4, tagsVector.Value, 0);
        var root = builder.EndTable();
        builder.Finish(root);

        var row = (TestRow)RealignedRowReader.Read(builder.SizedByteArray(), typeof(TestRow), [0, 2, 3, 4]);

        Assert.Equal(4021, row.Id);
        Assert.Equal("Serika", row.Name);
        Assert.Equal(55, row.Amount);
        Assert.Equal(new List<long> { 7, 8 }, row.Tags);
    }

    private class TestRow
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Amount { get; set; }
        public List<long> Tags { get; set; }
    }

    // The client's order is a name match against a build that may be ahead of the downloaded resources, so it only gets to win where the rows agree with it.
    [Fact]
    public void RowsWrittenInTheCompiledLayoutScoreAgainstTheClientsOrder()
    {
        var (compiled, candidate) = RealignedRowReader.ScoreMaps(AlignedRow(), typeof(TestRow).GetProperties(), [0, 2, 3, 4]);

        Assert.Equal(0, compiled);
        Assert.True(candidate > compiled, $"a row in our own layout scored {candidate} against the client's order and {compiled} against ours");
    }

    [Fact]
    public void RowsWrittenInTheClientsLayoutScoreAgainstTheCompiledOrder()
    {
        var (compiled, candidate) = RealignedRowReader.ScoreMaps(ShiftedRow(), typeof(TestRow).GetProperties(), [0, 2, 3, 4]);

        Assert.Equal(0, candidate);
        Assert.True(compiled > candidate, $"a shifted row scored {compiled} against our layout and {candidate} against the client's");
    }

    private static byte[] AlignedRow()
    {
        var builder = new FlatBufferBuilder(64);
        var name = builder.CreateString("Serika");
        var tagsVector = TagsVector(builder);

        builder.StartTable(4);
        builder.AddLong(0, 4021, 0);
        builder.AddOffset(1, name.Value, 0);
        builder.AddInt(2, 55, 0);
        builder.AddOffset(3, tagsVector.Value, 0);
        builder.Finish(builder.EndTable());
        return builder.SizedByteArray();
    }

    private static byte[] ShiftedRow()
    {
        var builder = new FlatBufferBuilder(64);
        var name = builder.CreateString("Serika");
        var tagsVector = TagsVector(builder);

        builder.StartTable(5);
        builder.AddLong(0, 4021, 0);
        builder.AddLong(1, 999, 0);
        builder.AddOffset(2, name.Value, 0);
        builder.AddInt(3, 55, 0);
        builder.AddOffset(4, tagsVector.Value, 0);
        builder.Finish(builder.EndTable());
        return builder.SizedByteArray();
    }

    private static VectorOffset TagsVector(FlatBufferBuilder builder)
    {
        var tags = new long[] { 7, 8 };
        builder.StartVector(8, tags.Length, 8);
        for (var i = tags.Length - 1; i >= 0; i--) builder.AddLong(tags[i]);
        return builder.EndVector();
    }

    [Fact]
    public void RealignedReaderMatchesTheGeneratedReaderOnAnAlignedTable()
    {
        if (!ClientExcelSchema.Available)
        {
            output.WriteLine("No installed client with a readable global-metadata.dat; nothing to compare against.");
            return;
        }

        var dumped = LocateDumped();
        if (dumped == null || !File.Exists(Path.Combine(dumped, "ExcelDB.db")))
        {
            output.WriteLine("No Resources/Dumped/ExcelDB.db; run the server once to download resources.");
            return;
        }

        ExcelTableService.DumpedDir = dumped;
        var generated = new ExcelTableService().GetTable<CharacterExcelT>();
        Assert.NotEmpty(generated);

        var identity = Enumerable.Range(0, typeof(CharacterExcelT).GetProperties().Length).ToArray();
        var realigned = new List<CharacterExcelT>();
        using (var connection = ExcelTableService.OpenExcelDbConnection(Path.Combine(dumped, "ExcelDB.db")))
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Bytes FROM [CharacterDBSchema]";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                realigned.Add((CharacterExcelT)RealignedRowReader.Read((byte[])reader[0], typeof(CharacterExcelT), identity));
        }

        Assert.Equal(
            JsonConvert.SerializeObject(generated),
            JsonConvert.SerializeObject(realigned));
    }

    [Fact]
    public void SlotMapsAgainstTheInstalledClientAreConsistent()
    {
        if (!ClientExcelSchema.Available)
        {
            output.WriteLine("No installed client with a readable global-metadata.dat; nothing to compare against.");
            return;
        }

        // every excel model the server can load: a slot map must either be unnecessary or strictly increasing over its mapped fields - a crossing map would mean the alignment paired fields out of order
        var mapped = 0;
        foreach (var model in typeof(CharacterExcelT).Assembly.GetTypes().Where(t => t.IsClass && t.Name.EndsWith("ExcelT")))
        {
            var map = ClientExcelSchema.SlotMapFor(model, model.Name[..^1]);
            if (map == null)
                continue;

            mapped++;
            var last = -1;
            foreach (var slot in map.Where(s => s >= 0))
            {
                Assert.True(slot > last, $"{model.Name}: slot map goes backwards at slot {slot}");
                last = slot;
            }
        }
        output.WriteLine($"{mapped} model(s) currently need realignment against the installed client");
    }

    private static string LocateDumped()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) return null;

        var server = Path.Combine(dir, "Shittim-Server");
        return new[]
        {
            Path.Combine(server, "Resources", "Dumped"),
            Path.Combine(server, "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(server, "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.FirstOrDefault(Directory.Exists);
    }
}
