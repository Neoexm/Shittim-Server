using System.Buffers.Binary;
using System.Reflection;
using System.Text.RegularExpressions;
using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Schale.Crypto;
using Schale.FlatData;
using Xunit;
using Xunit.Abstractions;

namespace Shittim_Server.Tests;

/// <summary>
/// Guards the generated FlatBuffer models in Schale.FlatData against the excel data the client ships.
/// They are reverse-engineered per client build rather than compiled from an authoritative .fbs, and a
/// reader stops at the last field it knows about without complaining, so drift is silent.
///
/// Trailing fields past the model's last slot are harmless - a vtable is an explicit index-to-offset map,
/// so earlier slots still decode. A field missing from the middle is not: every later slot shifts by one
/// and reads its neighbour, which is how GoodsExcel's absent sixth product-id long turned 120 ActionPoint
/// into "Currency 0x1_00000002 x5". A slot count cannot tell those apart, so the real check is
/// <see cref="NoShippedRowFailsVerification"/>, running each table's generated <c>Verify</c> over every
/// shipped row; <see cref="ModelWidthsHaveNotDriftedFurther"/> ratchets the counts on top.
///
/// The dumps are not in the repository (~300 MB, fetched into the build output), so those sweeps skip
/// when absent and <see cref="GoodsExcelModelMatchesItsShippedRows"/> carries the hermetic coverage.
/// </summary>
public class ExcelLayoutDriftTests
{
    /// <summary>
    /// Tables whose models are known to stop short of the shipped data, all of them confirmed benign by
    /// <see cref="NoShippedRowFailsVerification"/>: the extra slots are appended past the model's last
    /// field, so nothing the model does read is misaligned. Listing them turns an unactionable wall of 32
    /// findings into a ratchet - a table drifting for the first time fails, these do not.
    ///
    /// Regenerating any of these from the current client's schema is what removes it from the list; it
    /// would let the server read content it currently ignores, which is a feature, not a defect.
    ///
    /// Do not add a table here to silence a verification failure. Verification failing means the model
    /// genuinely misreads the data, which no baseline makes acceptable.
    /// </summary>
    private static readonly string[] KnownNarrowModels =
    [
        "ArenaMapExcel", "ArenaSeasonExcel", "BattlePassFlavorTextExcel", "CafeInfoExcel",
        "CharacterDialogEmojiExcel", "CharacterDialogEventExcel", "CharacterStatExcel",
        "ConstCombatExcel", "ConstCommonExcel", "ConstKeyMappingExcel", "ContentsShortcutExcel",
        "CurrencyExcel", "EquipmentExcel", "EventContentDiceRaceExcel", "EventContentScenarioExcel",
        "GroundExcel", "GuideMissionSeasonExcel", "ItemExcel", "KeyMappingExcel",
        "LocalizeCharProfileChangeExcel", "LocalizeCharProfileExcel", "MinigameCardExcel",
        "MinigameCCGOpenDialogExcel", "MiniGamePlayGuideExcel", "ObstacleStatExcel",
        "ProductSelectExcel", "ShiftingCraftRecipeExcel", "ShopCashExcel", "ShopFreeRecruitExcel",
        "SystemMailExcel", "TutorialFailureImageExcel", "WebEventSeasonExcel",
    ];

    /// <summary>
    /// Tables whose models misread the shipped data, with the row counts at measurement time so the list
    /// ratchets three ways: a new table breaking fails, one of these verifying fewer rows fails, and one
    /// that starts reading every row has to be removed. Unlike <see cref="KnownNarrowModels"/> these are
    /// misaligned rather than short - a slot index has moved, so later fields decode out of their neighbour.
    ///
    /// Measurement recovers where a field sits but not its name or contents. The reward tables were fixable
    /// because the omitted column had a recognisable signature - a <c>*ParcelUniqueName</c> string vector
    /// between the id and amount vectors - and none of these do, so naming their fields needs the client's
    /// global-metadata. A guess yields a model that verifies while still decoding the wrong column.
    ///
    /// Leaving them is safe because <see cref="TablesTheServerReadsLoadEveryShippedRow"/> holds
    /// unconditionally: of these only CharacterStatExcel is read by a handler, and every field the server
    /// touches sits at slots 0-10, ahead of the first shifted slot.
    /// </summary>
    private static readonly (string Name, int FailedRows, int Rows)[] KnownBrokenModels =
    [
        ("ArenaMapExcel", 60, 60),
        ("CharacterDialogEmojiExcel", 77, 414),
        ("CharacterStatExcel", 1144, 7279),
        ("ConstKeyMappingExcel", 1, 1),
        ("ContentSpoilerPopupExcel", 1, 1),
        ("ContentsShortcutExcel", 12, 12),
        ("EventContentScenarioExcel", 970, 970),
        ("EventContentSpoilerPopupExcel", 15, 15),
        ("GuideMissionSeasonExcel", 19, 19),
        ("KeyMappingExcel", 236, 246),
        ("KeyMappingPopupExcel", 15, 343),
        ("LocalizeCharProfileExcel", 1, 266),
        ("MinigameCardExcel", 2, 2),
        ("MiniGamePlayGuideExcel", 121, 146),
        ("ProductSelectExcel", 10, 10),
        ("ShiftingCraftRecipeExcel", 1, 91),
        ("ShopFreeRecruitExcel", 9, 9),
        ("SystemMailExcel", 30, 30),
        ("WebEventSeasonExcel", 29, 29),
    ];

    private readonly ITestOutputHelper output;

    public ExcelLayoutDriftTests(ITestOutputHelper output) => this.output = output;

    // the checks

    /// <summary>
    /// The check that speaks to behaviour rather than layout: every table a handler reads has to come back
    /// from <see cref="ExcelTableService"/> with all of its rows.
    ///
    /// The service catches per-row unpack failures and skips the offending row, which is the right call for
    /// a content patch adding a table the server does not care about, but it means a misaligned model
    /// degrades silently - a string field whose slot has shifted reads a uoffset that lands outside the
    /// buffer, UnPack throws, and the row vanishes. RaidStageExcel was doing exactly that for all 157 of
    /// its rows, so <c>FirstOrDefault</c> in the raid battle path returned null on every submission.
    ///
    /// No baseline here: a table the server reads and cannot fully load is a live defect.
    /// </summary>
    [Fact]
    public void TablesTheServerReadsLoadEveryShippedRow()
    {
        if (Report is null || TablesTheServerReads is null)
        {
            SkipNote();
            return;
        }

        // GetTable resolves Resources against AppContext.BaseDirectory, which under a test run is this
        // project's output rather than the server's. Point it at the data the audit just measured.
        ExcelTableService.DumpedDir = DumpedDir!;
        var service = new ExcelTableService();

        var getTable = typeof(ExcelTableService).GetMethod(nameof(ExcelTableService.GetTable))!;
        var shortfalls = new List<string>();

        foreach (var table in Report.Where(t => TablesTheServerReads.Contains(t.Name)).OrderBy(t => t.Name))
        {
            var model = typeof(GoodsExcelT).Assembly.GetType($"Schale.FlatData.{table.Name}T")!;
            var loaded = (System.Collections.ICollection)getTable
                .MakeGenericMethod(model)
                .Invoke(service, [false, table.Source == "ExcelDB"])!;

            if (loaded.Count != table.Rows)
                shortfalls.Add($"{table.Name} ({table.Source}): loaded {loaded.Count} of {table.Rows} shipped row(s)");
        }

        Assert.True(shortfalls.Count == 0,
            "Handlers read these tables, and ExcelTableService cannot unpack every shipped row - the rows " +
            "it drops are invisible to every lookup, so a miss reads as 'no such content' rather than an " +
            "error. This means a field's slot index has moved in the generated model:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", shortfalls));
    }

    [Fact]
    public void GoodsExcelModelMatchesItsShippedRows()
    {
        // The hermetic anchor, and the self-check on everything below: it is the one table whose correct
        // width is known independently (recovered by hand from a capture diff), so if the measurement or
        // the verification were wrong, this is where it shows.
        var widest = 0;
        var verify = VerifyActionFor(typeof(GoodsExcel))!;

        foreach (var row in GoodsExcelLayoutTests.AllRowBytes())
        {
            widest = Math.Max(widest, FlatBufferLayout.SlotCount(row, FlatBufferLayout.Root(row)));
            Assert.True(Verifies(row, verify), "A shipped GoodsExcel row does not verify against GoodsExcelT.");
        }

        Assert.Equal(22, ModelSlots(typeof(GoodsExcelT)));
        Assert.Equal(22, widest);
    }

    [Fact]
    public void NoShippedRowFailsVerification()
    {
        if (Report is null)
        {
            SkipNote();
            return;
        }

        var broken = Report.Where(t => t.FailedRows > 0).OrderBy(t => t.Name).ToList();
        var baseline = KnownBrokenModels.ToDictionary(t => t.Name, StringComparer.Ordinal);

        var appeared = broken
            .Where(t => !baseline.ContainsKey(t.Name))
            .Select(t => $"{t.Name} ({t.Source}): {t.FailedRows} of {t.Rows} row(s) fail {t.Name}Verify")
            .ToList();

        Assert.True(appeared.Count == 0,
            "Generated models cannot read the data they are pointed at. A verification failure means a " +
            "field's slot index has moved, so every field after it decodes out of its neighbour - the " +
            "GoodsExcel failure mode, which produces wrong values on the wire rather than an error. " +
            "Regenerate these from the current client's schema:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", appeared));

        // count passing rows, not failing ones - a content patch adds rows, so a raw failure count
        // trips on every client update. rows that verified at baseline and no longer do is the
        // signal, and it survives the table growing.
        var worse = broken
            .Where(t => baseline.TryGetValue(t.Name, out var was)
                        && t.Rows - t.FailedRows < was.Rows - was.FailedRows)
            .Select(t => $"{t.Name}: {t.Rows - t.FailedRows} of {t.Rows} row(s) verify, was " +
                         $"{baseline[t.Name].Rows - baseline[t.Name].FailedRows} of {baseline[t.Name].Rows}")
            .ToList();

        Assert.True(worse.Count == 0,
            "These models were already misaligned, and fewer rows verify than the baseline records - the " +
            "drift has grown rather than held still:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", worse));

        var fixedUp = baseline.Keys.Except(broken.Select(t => t.Name)).Order().ToList();

        Assert.True(fixedUp.Count == 0,
            "These models now read every shipped row and should come out of KnownBrokenModels, so the list " +
            "keeps meaning something: " + string.Join(", ", fixedUp));
    }

    [Fact]
    public void ModelWidthsHaveNotDriftedFurther()
    {
        if (Report is null)
        {
            SkipNote();
            return;
        }

        var narrow = Report.Where(t => t.DataSlots > t.ModelSlots).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        var appeared = narrow.Except(KnownNarrowModels).Order().ToList();
        var resolved = KnownNarrowModels.Except(narrow).Order().ToList();

        Assert.True(appeared.Count == 0,
            "These models no longer cover every field in the shipped data, which usually means a client " +
            "build added fields. Verification still passing means nothing is being misread today, so this " +
            "is a regeneration prompt rather than a live defect - regenerate them, or add them to " +
            "KnownNarrowModels if the new fields are genuinely not wanted:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", appeared.Select(Describe)));

        Assert.True(resolved.Count == 0,
            "These models now cover the shipped data and should come out of KnownNarrowModels - a stale " +
            "entry hides real drift: " + string.Join(", ", resolved));
    }

    private static string Describe(string name)
    {
        var table = Report!.First(t => t.Name == name);
        return $"{name} ({table.Source}): data has {table.DataSlots} slots, {name}T model has {table.ModelSlots}";
    }

    private void SkipNote() => output.WriteLine(
        "No Resources/Dumped found, so nothing was compared. The excel dumps live in the server's build " +
        "output rather than the repo; run the server once to download them, then re-run this test.");

    // the audit

    private readonly record struct TableAudit(
        string Name, string Source, int ModelSlots, int DataSlots, int Rows, int FailedRows);

    // One pass over ~300 MB of dumps, shared by every test in the class. Null when the dumps are absent.
    private static readonly string? DumpedDir = LocateDumpedExcelData();
    private static readonly List<TableAudit>? Report = BuildReport();

    /// <summary>
    /// The tables a handler actually reads, recovered from the server's own <c>GetTable&lt;FooT&gt;</c> call
    /// sites. Read out of the source rather than kept as a list here so it cannot go stale: a handler that
    /// starts reading a table brings it under
    /// <see cref="TablesTheServerReadsLoadEveryShippedRow"/> automatically.
    /// </summary>
    private static readonly HashSet<string>? TablesTheServerReads = FindTablesTheServerReads();

    private static HashSet<string>? FindTablesTheServerReads()
    {
        if (RepositoryRoot() is not { } root)
            return null;

        var names = new HashSet<string>(StringComparer.Ordinal);
        var call = new Regex(@"GetTable<(\w+)T>", RegexOptions.Compiled);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            foreach (var match in call.Matches(File.ReadAllText(file)).Cast<Match>())
                names.Add(match.Groups[1].Value);
        }

        return names;
    }

    private static List<TableAudit>? BuildReport()
    {
        var dumped = DumpedDir;
        if (dumped is null)
            return null;

        var report = new List<TableAudit>();
        report.AddRange(AuditBytesFiles(dumped));
        report.AddRange(AuditExcelDb(dumped));

        return report;
    }

    /// <summary>
    /// The .bytes route: one file per table holding a <c>{Name}Table</c> wrapper whose single field is the
    /// row vector. The whole file is XOR-obfuscated under a key derived from the wrapper's name, so it has
    /// to be decoded before any offset in it means anything.
    /// </summary>
    private static IEnumerable<TableAudit> AuditBytesFiles(string dumpedDir)
    {
        var excelDir = Path.Combine(dumpedDir, "Excel");
        if (!Directory.Exists(excelDir))
            yield break;

        foreach (var (model, reader, baseName) in ExcelModels())
        {
            var excelName = baseName + "Table";
            var path = Path.Combine(excelDir, $"{excelName.ToLower()}.bytes");
            if (!File.Exists(path))
                continue;

            var bytes = File.ReadAllBytes(path);
            TableEncryptionService.XOR(excelName, bytes);

            var root = FlatBufferLayout.Root(bytes);

            // Generated wrapper, so this is a self-check on the decode rather than an expectation about
            // the data: byte soup would almost never present as a single-field root table.
            if (FlatBufferLayout.SlotCount(bytes, root) != 1)
                throw new InvalidOperationException(
                    $"{path} did not decode to a single-field table wrapper; the XOR key or the file " +
                    "layout is not what ExcelTableService assumes.");

            var rows = 0;
            var widest = 0;
            foreach (var row in FlatBufferLayout.VectorElements(bytes, root, slot: 0))
            {
                rows++;
                widest = Math.Max(widest, FlatBufferLayout.SlotCount(bytes, row));
            }

            // Verified through the wrapper, which recurses into every row, so one call covers the table.
            var failed = Verifies(bytes, VerifyActionFor(reader.Assembly.GetType($"{reader.Namespace}.{excelName}")!)!)
                ? 0
                : rows;

            yield return new TableAudit(baseName, "bytes", ModelSlots(model), widest, rows, failed);
        }
    }

    /// <summary>
    /// The ExcelDB route: one SQLite table per excel, each row a standalone plaintext FlatBuffer. This is
    /// where the GoodsExcel bug lived, and it covers about five times as many tables as the .bytes files.
    /// </summary>
    private static IEnumerable<TableAudit> AuditExcelDb(string dumpedDir)
    {
        var dbPath = Path.Combine(dumpedDir, "ExcelDB.db");
        if (!File.Exists(dbPath))
            yield break;

        using var connection = ExcelTableService.OpenExcelDbConnection(dbPath);

        var present = new HashSet<string>(StringComparer.Ordinal);
        using (var listing = connection.CreateCommand())
        {
            listing.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            using var reader = listing.ExecuteReader();
            while (reader.Read())
                present.Add(reader.GetString(0));
        }

        foreach (var (model, reader, baseName) in ExcelModels())
        {
            // ExcelTableService's own convention for turning a model name into a table name.
            if (!present.Contains(baseName.Replace("Excel", "DBSchema")))
                continue;

            var verify = VerifyActionFor(reader)!;
            var rows = 0;
            var failed = 0;
            var widest = 0;

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Bytes FROM [{baseName.Replace("Excel", "DBSchema")}]";

            using (var cursor = command.ExecuteReader())
            {
                while (cursor.Read())
                {
                    if (cursor.IsDBNull(0) || cursor[0] is not byte[] { Length: > 8 } row)
                        continue;

                    rows++;

                    // Widest, not first: a row's vtable is truncated after its last non-default field, so
                    // a row leaving the tail at defaults looks narrower than the schema. Only the maximum
                    // over the whole table is the real width.
                    widest = Math.Max(widest, FlatBufferLayout.SlotCount(row, FlatBufferLayout.Root(row)));

                    if (!Verifies(row, verify))
                        failed++;
                }
            }

            yield return new TableAudit(baseName, "ExcelDB", ModelSlots(model), widest, rows, failed);
        }
    }

    /// <summary>
    /// Every generated object-API class (<c>FooT</c>) paired with its reader struct (<c>Foo</c>). Nested
    /// tables come back too; they are filtered out by having neither a .bytes file nor a DBSchema table.
    /// </summary>
    private static IEnumerable<(Type Model, Type Reader, string BaseName)> ExcelModels()
    {
        var assembly = typeof(GoodsExcelT).Assembly;

        foreach (var model in assembly.GetTypes())
        {
            if (!model.IsClass || !model.IsPublic || !model.Name.EndsWith('T'))
                continue;

            var baseName = model.Name[..^1];
            if (assembly.GetType($"{model.Namespace}.{baseName}") is not { } reader)
                continue;

            yield return (model, reader, baseName);
        }
    }

    private static bool Verifies(byte[] buffer, VerifyTableAction verify)
    {
        // The defaults cap tables and depth low enough that a large .bytes wrapper trips the cap and
        // reports a false failure; the caps exist to bound work on untrusted input, which this is not.
        var verifier = new Verifier(new ByteBuffer(buffer), new Options())
            .SetMaxTables(int.MaxValue)
            .SetMaxDepth(int.MaxValue);

        // Excel buffers carry no file identifier and are not size-prefixed. The identifier has to be
        // null rather than empty - Verifier length-checks anything non-null against the 4-byte format.
        return verifier.VerifyBuffer(null!, false, verify);
    }

    private static VerifyTableAction? VerifyActionFor(Type reader)
    {
        var verify = reader.Assembly
            .GetType($"{reader.Namespace}.{reader.Name}Verify")
            ?.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static);

        return verify is null
            ? null
            : (VerifyTableAction)Delegate.CreateDelegate(typeof(VerifyTableAction), verify);
    }

    private static int ModelSlots(Type model) => model.GetProperties().Length;

    private static string? RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Shittim-Server", "Shittim-Server.csproj")))
                return dir.FullName;

        return null;
    }

    private static string? LocateDumpedExcelData()
    {
        if (RepositoryRoot() is { } root)
        {
            var server = Path.Combine(root, "Shittim-Server");

            // ExcelTableService resolves Resources against AppContext.BaseDirectory, which in a test run
            // is the test project's own output. The data belongs to the server, so look there instead.
            return new[]
            {
                Path.Combine(server, "Resources", "Dumped"),
                Path.Combine(server, "bin", "Debug", "net10.0", "Resources", "Dumped"),
                Path.Combine(server, "bin", "Release", "net10.0", "Resources", "Dumped"),
            }.FirstOrDefault(Directory.Exists);
        }

        return null;
    }
}

/// <summary>
/// Just enough of the FlatBuffers binary format to measure a table's width without a schema. Reading a row
/// through its generated model cannot measure drift - a short model is precisely the case that succeeds
/// quietly - so this looks at the vtable the writer emitted instead.
/// </summary>
internal static class FlatBufferLayout
{
    /// <summary>Follows a uoffset_t, which is relative to its own position and always forward.</summary>
    private static int Follow(byte[] buffer, int position) =>
        position + BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(position));

    public static int Root(byte[] buffer) => Follow(buffer, 0);

    /// <summary>Number of field slots the writer emitted for the table at <paramref name="table"/>.</summary>
    public static int SlotCount(byte[] buffer, int table)
    {
        var vtable = VTable(buffer, table);
        // vtable layout: [0] its own size in bytes, [2] the table's inline size, then one uint16 per field.
        return (BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(vtable)) - 4) / 2;
    }

    /// <summary>Positions of the tables held by a vector-of-tables field.</summary>
    public static IEnumerable<int> VectorElements(byte[] buffer, int table, int slot)
    {
        var vtable = VTable(buffer, table);
        var vtableBytes = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(vtable));

        var slotPosition = 4 + slot * 2;
        if (slotPosition + 2 > vtableBytes)
            yield break;

        // A zero entry means the field was never written, i.e. it holds its default.
        var fieldOffset = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(vtable + slotPosition));
        if (fieldOffset == 0)
            yield break;

        var vector = Follow(buffer, table + fieldOffset);
        var length = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(vector));

        for (var i = 0; i < length; i++)
            yield return Follow(buffer, vector + 4 + i * 4);
    }

    // A table stores a soffset_t to its vtable, and the vtable sits behind it - hence subtraction.
    private static int VTable(byte[] buffer, int table) =>
        table - BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(table));
}
