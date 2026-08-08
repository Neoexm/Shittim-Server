using System.Text.Json;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.Crypto;
using Schale.FlatData;
using Serilog;

namespace Shittim_Server.Services;

// Whether an event is running is decided entirely inside the client: MX.Data.EventContentData compares the current time against the EventContentSeasonExcel date strings from its own ExcelDB.db (IsEventActivated over BeforehandExposedTime..ExtensionTime, IsEventPlayable over OpenTime..CloseTime, IsEventRewardable over OpenTime..ExtensionTime), and none of that is on the wire. So running an old event again means rewriting those dates in the client's table, the same way ClientExcelBannerPatchService rewrites banner art.
// The server's own dump is never patched, so it stays the source of the shipped dates a switched-off event gets restored to.
public static class EventScheduleService
{
    private const string SchemaTable = "EventContentSeasonDBSchema";
    private const string ItemSchemaTable = "ItemDBSchema";
    private const string ForcedOpen = "2020-01-01 00:00:00";
    private const string ForcedClose = "2099-12-31 23:59:59";

    private static string StatePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "event_schedule.json");

    private class ScheduleState
    {
        public List<long> enabled { get; set; } = new();
    }

    public static HashSet<long> Enabled { get; private set; } = new();

    // true once the schedule has been touched at all, empty set included. The boot re-apply keys off this rather than off the enabled count, so an operator who switches everything back off still gets the shipped dates restored after the next client update instead of being stuck with whatever the last run forced.
    public static bool Configured { get; private set; }

    public static void Load()
    {
        try
        {
            var path = Path.GetFullPath(StatePath);
            if (!File.Exists(path))
                return;

            var state = JsonSerializer.Deserialize<ScheduleState>(File.ReadAllText(path));
            if (state == null)
                return;

            Enabled = state.enabled.ToHashSet();
            Configured = true;
            if (Enabled.Count > 0)
                Log.Information("Event schedule override: {Count} event(s) forced open", Enabled.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read {Path}, the event schedule stays as the client shipped it", StatePath);
        }
    }

    public static int Set(IEnumerable<long> enabled, ExcelTableService excel)
    {
        var previous = Enabled;
        Enabled = enabled.ToHashSet();

        int written;
        try
        {
            written = Apply(excel);
        }
        catch
        {
            // the caller turns this into a BadRequest, so persisting first would leave disk and process state insisting the events are on while the operator was told it failed - and Load() would pick that up again at the next start
            Enabled = previous;
            throw;
        }

        var state = new ScheduleState { enabled = Enabled.OrderBy(x => x).ToList() };
        File.WriteAllText(Path.GetFullPath(StatePath), JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        Configured = true;
        return written;
    }

    // Rewrites every season row in the client's ExcelDB to either the forced-open window or its shipped one, so the result depends only on the current enabled set and re-applying it changes nothing.
    public static int Apply(ExcelTableService excel)
    {
        // grouped rather than a plain ToDictionary because a client that ever ships two rows for one (id, type) would otherwise throw out of the startup re-apply, which GameServer only logs - every forced date would quietly go back to shipped
        var pristine = excel.GetTable<EventContentSeasonExcelT>()
            .GroupBy(s => (s.EventContentId, s.EventContentType))
            .ToDictionary(g => g.Key, g => g.First());
        if (pristine.Count == 0)
            throw new InvalidOperationException("EventContentSeasonExcel loaded empty on the server side, so there is nothing to restore switched-off events to");

        var dbPath = GetExcelDbPath();
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            throw new FileNotFoundException("The installed client's ExcelDB.db was not found, so the event schedule cannot be changed", dbPath);

        SqliteProvider.EnsureInitialized();

        var written = 0;
        var unmatched = 0;
        var expiryChanged = 0;

        // ExcelDB flatbuffer strings are stored plaintext (the SQLCipher layer is the only encryption), so read and write them with the string cipher off or every untouched field comes back mangled. The repack is not byte-identical - the builder lays a buffer out its own way - but it is value-identical and stable, so applying the same enabled set twice writes the same bytes the second time. UseEncryption is the same process-global a table load flips, and this runs on a request thread, so it takes the loader's lock for as long as the unpack and repack below need the flag to stay put.
        lock (ExcelTableService.loadLock)
        {
            TableEncryptionService.UseEncryption = false;

            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWrite
            }.ToString());
            conn.Open();
            ApplyKey(conn);

            var rows = new List<(long RowId, byte[] Bytes)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT rowid, Bytes FROM [{SchemaTable}]";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    rows.Add((reader.GetInt64(0), (byte[])reader[1]));
            }

            using (var tx = conn.BeginTransaction())
            {
                var raid = WorldRaidService.Manifest;

                foreach (var row in rows)
                {
                    var rec = EventContentSeasonExcel.GetRootAsEventContentSeasonExcel(new ByteBuffer(row.Bytes)).UnPack();

                    // World raid seasons get the manifest's real window rather than the forced-open one: SetTimeTableFromEvent hands these dates straight to the season timer the raid ui counts down on, so 2099 would show a raid that never ends and the boss spawn maths would sit before every window. WorldRaidEntrance is the event-lobby door for the same season id and rides along.
                    if (raid != null && rec.EventContentId == raid.seasonId && (rec.EventContentType == EventContentType.WorldRaid || rec.EventContentType == EventContentType.WorldRaidEntrance))
                    {
                        rec.BeforehandExposedTime = string.IsNullOrEmpty(raid.exposed) ? raid.open : raid.exposed;
                        rec.EventContentOpenTime = raid.open;
                        rec.EventContentCloseTime = raid.close;
                        rec.ExtensionTime = string.IsNullOrEmpty(raid.extension) ? raid.close : raid.extension;
                        rec.EventContentCloseNoteTime = "";
                    }
                    else if (Enabled.Contains(rec.EventContentId))
                    {
                        rec.BeforehandExposedTime = ForcedOpen;
                        rec.EventContentOpenTime = ForcedOpen;
                        rec.EventContentCloseTime = ForcedClose;
                        rec.ExtensionTime = ForcedClose;
                        // TimeService.TryParse only runs on a non-empty string, so blanking this leaves CloseNoteTime at DateTime.MinValue and IsEventCloseNotifyPeriod short-circuits instead of nagging that a permanently-open event is about to end.
                        rec.EventContentCloseNoteTime = "";
                    }
                    else if (pristine.TryGetValue((rec.EventContentId, rec.EventContentType), out var original))
                    {
                        rec.BeforehandExposedTime = original.BeforehandExposedTime;
                        rec.EventContentOpenTime = original.EventContentOpenTime;
                        rec.EventContentCloseTime = original.EventContentCloseTime;
                        rec.ExtensionTime = original.ExtensionTime;
                        rec.EventContentCloseNoteTime = original.EventContentCloseNoteTime;
                    }
                    else
                    {
                        unmatched++;
                        continue;
                    }

                    var fbb = new FlatBufferBuilder(Math.Max(64, row.Bytes.Length + 64));
                    fbb.Finish(EventContentSeasonExcel.Pack(fbb, rec).Value);
                    var newBytes = fbb.SizedByteArray();

                    using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = $"UPDATE [{SchemaTable}] SET Bytes = @b WHERE rowid = @r";
                    upd.Parameters.Add("@b", SqliteType.Blob).Value = newBytes;
                    upd.Parameters.Add("@r", SqliteType.Integer).Value = row.RowId;
                    upd.ExecuteNonQuery();
                    written++;
                }
                tx.Commit();
            }

            // Event currency disappears from the inventory once its ItemExcel.ExpirationDateTime is in the past: ItemObject.IsValid is nothing but IsRemainExpirationTime and InventoryObjectBase.GetList drops whatever is invalid, so a replayed event hands out tokens the player is told they received and then cannot find. Blanking the date on the forced-open events leaves IsRemainExpirationTime on its is-null-or-empty early out, which also keeps them out of the lobby's expiring-soon popup.
            var itemOwners = new Dictionary<long, List<long>>();
            foreach (var currency in excel.GetTable<EventContentCurrencyItemExcelT>().Where(x => x.ItemUniqueId != 0))
            {
                if (!itemOwners.TryGetValue(currency.ItemUniqueId, out var owners))
                    itemOwners[currency.ItemUniqueId] = owners = new List<long>();
                owners.Add(currency.EventContentId);
            }
            foreach (var season in pristine.Values.Where(x => x.EventItemId != 0))
            {
                if (!itemOwners.TryGetValue(season.EventItemId, out var owners))
                    itemOwners[season.EventItemId] = owners = new List<long>();
                owners.Add(season.EventContentId);
            }

            var shippedExpiry = excel.GetTable<ItemExcelT>()
                .Where(x => itemOwners.ContainsKey(x.Id) && !string.IsNullOrEmpty(x.ExpirationDateTime))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().ExpirationDateTime);

            var itemRows = new List<(long RowId, byte[] Bytes)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT rowid, Bytes FROM [{ItemSchemaTable}]";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    itemRows.Add((reader.GetInt64(0), (byte[])reader[1]));
            }

            using (var tx = conn.BeginTransaction())
            {
                foreach (var row in itemRows)
                {
                    var rec = ItemExcel.GetRootAsItemExcel(new ByteBuffer(row.Bytes)).UnPack();
                    if (!itemOwners.TryGetValue(rec.Id, out var owners) || !shippedExpiry.TryGetValue(rec.Id, out var shipped))
                        continue;

                    var wanted = owners.Any(Enabled.Contains) ? "" : shipped;
                    if (rec.ExpirationDateTime == wanted)
                        continue;

                    rec.ExpirationDateTime = wanted;

                    var fbb = new FlatBufferBuilder(Math.Max(64, row.Bytes.Length + 64));
                    fbb.Finish(ItemExcel.Pack(fbb, rec).Value);

                    using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = $"UPDATE [{ItemSchemaTable}] SET Bytes = @b WHERE rowid = @r";
                    upd.Parameters.Add("@b", SqliteType.Blob).Value = fbb.SizedByteArray();
                    upd.Parameters.Add("@r", SqliteType.Integer).Value = row.RowId;
                    upd.ExecuteNonQuery();
                    expiryChanged++;
                }
                tx.Commit();
            }
        }

        if (expiryChanged > 0)
            Log.Information("Event schedule: {Count} event currency item(s) had their expiry date rewritten", expiryChanged);

        if (unmatched > 0)
            Log.Warning("Event schedule: {Count} client season row(s) have no counterpart in the server's dump and were left alone", unmatched);

        Log.Information("Event schedule: {Written} season row(s) rewritten, {Enabled} event(s) forced open", written, Enabled.Count);
        return written;
    }

    private static void ApplyKey(SqliteConnection conn)
    {
        var key = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY");
        if (string.IsNullOrWhiteSpace(key))
            key = Config.Instance.ServerConfiguration.ExcelDbSqlCipherKey;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA key = \"x'{key.Trim()}'\";";
        cmd.ExecuteNonQuery();
    }

    private static string GetExcelDbPath()
    {
        var configured = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH");
        if (string.IsNullOrWhiteSpace(configured))
            configured = Config.Instance.ServerConfiguration.ClientExcelDbPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            // scanning on from here would rewrite whichever install the Steam locator turns up rather than the one that was named, and on a machine holding two copies that is the wrong game getting patched with no sign it happened
            if (!File.Exists(configured))
                throw new FileNotFoundException("The configured client ExcelDB.db does not exist", configured);
            return configured;
        }

        const string relative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\ExcelDB.db";

        // Derive from the configured client-metadata install root (same install tree).
        var metaPath = Config.Instance.ServerConfiguration.ClientMetadataPath;
        if (!string.IsNullOrWhiteSpace(metaPath))
        {
            var idx = metaPath.IndexOf(@"\BlueArchive_Data", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                var candidate = Path.Combine(metaPath[..idx], relative);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        // Any Steam library can hold the install.
        return SteamGameLocator.FindGameFile(relative) ?? string.Empty;
    }
}
