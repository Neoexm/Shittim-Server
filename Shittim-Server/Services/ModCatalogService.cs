using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Ionic.Zip;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using Schale.Crypto;
using Schale.FlatData;
using Serilog;

namespace Shittim_Server.Services
{
    // The Steam client never fetches a catalog over the wire - it boots from catalog_Windows.bytes inside the install and refreshes its LocalLow copy from that - so mods reach it by rewriting the installed file in place. The shipped bytes are kept next to it as catalog_Windows.bytes.premods and stay the splice source, reseeded whenever Steam lands a fresh retail catalog.
    public class ModCatalogService
    {
        private const string BundleProvider = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";
        private const string AssetProvider = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";
        private const string BundleResource = "UnityEngine.ResourceManagement.ResourceProviders.IAssetBundleResource";

        private readonly CustomCharacterService characters;
        private readonly ExcelTableService excel;
        private readonly ILogger<ModCatalogService> logger;
        private readonly object gate = new();

        private byte[] bytes;
        private string hash;
        private string stamp;

        public ModCatalogService(CustomCharacterService characters, ExcelTableService excel, ILogger<ModCatalogService> logger)
        {
            this.characters = characters;
            this.excel = excel;
            this.logger = logger;
        }

        public (byte[] Bytes, string Hash) Current()
        {
            lock (gate)
            {
                var source = SourcePath();
                if (source == null)
                    throw new InvalidOperationException("No catalog_Remote.bytes could be located to rewrite - point SHITTIM_ADDRESSABLE_CATALOG_PATH at one, or run the client once so it caches its own");

                var registry = Path.Combine(CustomCharacterService.ModsDir, "characters.json");
                var current = File.GetLastWriteTimeUtc(source).ToString("O") + "|" + (File.Exists(registry) ? File.GetLastWriteTimeUtc(registry).ToString("O") : "-");
                if (bytes != null && stamp == current)
                    return (bytes, hash);

                bytes = Build(source);
                hash = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
                stamp = current;
                return (bytes, hash);
            }
        }

        public static string SourcePath()
        {
            var configured = Environment.GetEnvironmentVariable("SHITTIM_ADDRESSABLE_CATALOG_PATH");
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            var install = InstallCatalogPath();
            if (install != null && File.Exists(install + ".premods"))
                return install + ".premods";

            var cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NEXON Games", "Blue Archive", "catalog_Remote.bytes");
            return File.Exists(cached) ? cached : null;
        }

        public static string InstallCatalogPath()
        {
            const string relative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Catalog\Windows\catalog_Windows.bytes";

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

            return SteamGameLocator.FindGameFile(relative);
        }

        public void SyncClient()
        {
            var install = InstallCatalogPath();
            if (install == null)
                return;

            lock (gate)
            {
                SyncAnimatorData(install);
                SyncSkillData(install);
                SyncMediaResources(install);
                SyncTableCatalog(install);

                var premods = install + ".premods";
                var mods = characters.List().Where(m => !string.IsNullOrWhiteSpace(m.Bundle) && File.Exists(Path.Combine(CustomCharacterService.ModsDir, m.Id.ToString(), m.Bundle))).ToList();
                var installed = File.ReadAllBytes(install);
                // a steam update or verify drops a fresh retail catalog over the spliced one; that unspliced file becomes the new source
                var spliced = mods.Any(m => ((ReadOnlySpan<byte>)installed).IndexOf(Encoding.UTF8.GetBytes(m.Bundle)) >= 0);

                // steam bumps catalog_Windows.hash with the build, and a copy taken before that names bundle files the update has already deleted, so putting it back leaves the client resolving addresses to files that are not on disk and sitting on the unpack screen
                var shippedHash = Path.ChangeExtension(install, ".hash");
                var seededHash = premods + ".hash";
                if (File.Exists(premods) && !spliced && File.ReadAllText(shippedHash) != (File.Exists(seededHash) ? File.ReadAllText(seededHash) : ""))
                {
                    File.Delete(premods);
                    logger.LogInformation("Client build moved on - dropped the stale catalog copy at {Path}", premods);
                }

                if (mods.Count == 0)
                {
                    if (File.Exists(premods) && !((ReadOnlySpan<byte>)installed).SequenceEqual(File.ReadAllBytes(premods)))
                    {
                        File.Copy(premods, install, true);
                        logger.LogInformation("No modded bundles left - put the shipped catalog back at {Path}", install);
                    }
                    return;
                }

                if (!File.Exists(premods) || (!spliced && !((ReadOnlySpan<byte>)installed).SequenceEqual(File.ReadAllBytes(premods))))
                {
                    File.Copy(install, premods, true);
                    File.Copy(shippedHash, seededHash, true);
                }

                var gameData = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "..", "GameData", "Windows"));
                foreach (var mod in mods)
                {
                    var src = Path.Combine(CustomCharacterService.ModsDir, mod.Id.ToString(), mod.Bundle);
                    var dst = Path.Combine(gameData, mod.Bundle);
                    if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(src).Length || File.GetLastWriteTimeUtc(dst) < File.GetLastWriteTimeUtc(src))
                        File.Copy(src, dst, true);
                }

                var built = Build(premods);
                if (!((ReadOnlySpan<byte>)built).SequenceEqual(installed))
                {
                    File.WriteAllBytes(install, built);
                    var cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NEXON Games", "Blue Archive", "catalog_Remote.bytes");
                    if (File.Exists(cached))
                        File.WriteAllBytes(cached, built);
                    logger.LogInformation("Spliced {Count} modded bundle(s) into {Path}", mods.Count, install);
                }
            }
        }

        // CharacterAnimationController, HexaUnitVisual and the timeline behaviours all resolve a FlatData.AnimatorData by the animator controller's asset name, out of animatordatatable.bytes inside Preload\TableBundles\Excel.zip. A clone whose controllers carry its own name matches nothing there and every one of those call sites throws on the null table, which shows up as a unit that never settles into idle and a deploy that never finishes loading. The rows are pure state metadata over a state machine the clone shares with its donor, so the donor's are copied under the clone's names.
        private void SyncAnimatorData(string install)
        {
            var zipPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "..", "Preload", "TableBundles", "Excel.zip"));
            if (!File.Exists(zipPath))
                return;

            var premods = zipPath + ".premods";
            var stems = AnimatorStems();

            if (stems.Count == 0)
            {
                if (File.Exists(premods))
                {
                    File.Copy(premods, zipPath, true);
                    File.Delete(premods);
                    logger.LogInformation("No modded controllers left - put the shipped animator table back at {Path}", zipPath);
                }
                return;
            }

            if (!File.Exists(premods))
                File.Copy(zipPath, premods, true);

            var pristine = ReadAnimatorTable(premods);

            AnimatorDataTableT table;
            // the row strings are stored plain in here, and this runs at boot next to the first lazy table loads that set the flag the other way
            lock (ExcelTableService.loadLock)
            {
                TableEncryptionService.UseEncryption = false;
                table = AnimatorDataTable.GetRootAsAnimatorDataTable(new ByteBuffer(pristine)).UnPack();
            }

            var added = 0;
            foreach (var (stem, donorStem) in stems)
            {
                foreach (var row in table.DataList.Where(r => r.Name != null && r.Name.Contains(donorStem)).ToList())
                {
                    table.DataList.Add(new AnimatorDataT { DefaultStateName = row.DefaultStateName, Name = row.Name.Replace(donorStem, stem), DataList = row.DataList });
                    added++;
                }
            }

            if (added == 0)
                return;

            var builder = new FlatBufferBuilder(pristine.Length + 0x10000);
            builder.Finish(AnimatorDataTable.Pack(builder, table).Value);
            var patched = builder.SizedByteArray();
            TableEncryptionService.XOR("AnimatorDataTable", patched);

            if (((ReadOnlySpan<byte>)patched).SequenceEqual(ReadAnimatorTable(zipPath, decrypt: false)))
                return;

            using (var zip = ZipFile.Read(premods))
            {
                zip.Password = Convert.ToBase64String(TableService.CreatePassword(Path.GetFileName(zipPath)));
                zip.UpdateEntry("animatordatatable.bytes", patched);
                zip.Save(zipPath);
            }

            logger.LogInformation("Added {Count} animator row(s) for {Mods} modded controller name(s) to {Path}", added, stems.Count, zipPath);
        }

        // Voice never goes through addressables. MediaService asks its own catalog for a key, loads the loose file the row names, and only when that file is missing does it fall through to the packed .molru bank the retail voices live in. 78 of the shipped clips are already loose files with a row apiece, so a clone's clips take that same route and the container never has to be opened or rebuilt.
        private void SyncMediaResources(string install)
        {
            var catalog = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "MediaResources", "MediaCatalog.bytes"));
            if (!File.Exists(catalog))
                return;

            var premods = catalog + ".premods";
            var clips = new List<(string Key, string Path, string Source)>();
            foreach (var mod in characters.List())
            {
                if (mod.Media == null)
                    continue;
                foreach (var pair in mod.Media)
                {
                    var source = Path.Combine(CustomCharacterService.ModsDir, mod.Id.ToString(), pair.Value);
                    if (!File.Exists(source))
                    {
                        logger.LogWarning("Custom character {Id} wants {Key} from {File}, which was never staged", mod.Id, pair.Key, pair.Value);
                        continue;
                    }
                    clips.Add((pair.Key.ToLowerInvariant(), pair.Key + Path.GetExtension(pair.Value), source));
                }
            }

            if (clips.Count == 0)
            {
                if (File.Exists(premods))
                {
                    File.Copy(premods, catalog, true);
                    File.Delete(premods);
                    logger.LogInformation("No modded clips left - put the shipped media catalog back at {Path}", catalog);
                }
                return;
            }

            var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "..", "GameData", "MediaResources"));
            foreach (var clip in clips)
            {
                var dst = Path.Combine(root, clip.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(clip.Source).Length || File.GetLastWriteTimeUtc(dst) < File.GetLastWriteTimeUtc(clip.Source))
                    File.Copy(clip.Source, dst, true);
            }

            var installed = File.ReadAllBytes(catalog);
            var spliced = clips.Any(c => ((ReadOnlySpan<byte>)installed).IndexOf(Encoding.UTF8.GetBytes(c.Key)) >= 0);
            if (!File.Exists(premods) || (!spliced && !((ReadOnlySpan<byte>)installed).SequenceEqual(File.ReadAllBytes(premods))))
                File.Copy(catalog, premods, true);

            var built = SpliceMedia(File.ReadAllBytes(premods), clips);
            if (((ReadOnlySpan<byte>)built).SequenceEqual(installed))
                return;

            File.WriteAllBytes(catalog, built);
            logger.LogInformation("Spliced {Count} modded clip(s) into {Path}", clips.Count, catalog);
        }

        // memorypack lays the catalog out as its member count, the dictionary's own count and then key/value pairs end to end, with whatever members follow the dictionary trailing all of it, so the new rows are walked to rather than appended and the count is bumped in place
        private static byte[] SpliceMedia(byte[] shipped, List<(string Key, string Path, string Source)> clips)
        {
            var i = 1;
            var count = BitConverter.ToInt32(shipped, i);
            i += 4;
            for (var n = 0; n < count; n++)
            {
                i += StringLength(shipped, i);
                i++;
                i += StringLength(shipped, i);
                i += 8;
            }

            var output = new MemoryStream(shipped.Length + clips.Count * 0x80);
            output.WriteByte(shipped[0]);
            output.Write(BitConverter.GetBytes(count + clips.Count));
            output.Write(shipped, 5, i - 5);
            foreach (var clip in clips)
            {
                WriteString(output, clip.Key);
                // every shipped voice row is a three-member Media reading (3, 1), and the 1 is the audio media type the loader switches on
                output.WriteByte(3);
                WriteString(output, clip.Path);
                output.Write(BitConverter.GetBytes(3));
                output.Write(BitConverter.GetBytes(1));
            }
            output.Write(shipped, i, shipped.Length - i);
            return output.ToArray();
        }

        private static int StringLength(byte[] blob, int offset)
        {
            var prefix = BitConverter.ToInt32(blob, offset);
            if (prefix >= 0)
                return 4 + prefix * 2;
            return prefix == -1 ? 4 : 8 + ~prefix;
        }

        private static void WriteString(MemoryStream output, string text)
        {
            var encoded = Encoding.UTF8.GetBytes(text);
            output.Write(BitConverter.GetBytes(~encoded.Length));
            output.Write(BitConverter.GetBytes(text.Length));
            output.Write(encoded);
        }

        // Every .db sitting in Preload\TableBundles is checksummed against TableCatalog.bytes on the way into a battle, and a mismatch is the "resources are damaged" notice plus a restore of the whole set, which puts the shipped ExcelDB.db back and takes the clone's rows with it. The zips are stored as a zero checksum and go unchecked, which is why Excel.zip has never needed any of this.
        internal static void SyncTableCatalog(string install)
        {
            var catalog = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "TableBundles", "TableCatalog.bytes"));
            if (!File.Exists(catalog))
                return;

            var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "..", "Preload", "TableBundles"));
            var blob = File.ReadAllBytes(catalog);
            var patched = 0;

            foreach (var path in Directory.GetFiles(dir, "*.db"))
            {
                var name = Encoding.UTF8.GetBytes(Path.GetFileName(path));
                var anchor = new byte[9 + name.Length];
                anchor[0] = 4;
                BitConverter.GetBytes(~name.Length).CopyTo(anchor, 1);
                BitConverter.GetBytes(name.Length).CopyTo(anchor, 5);
                name.CopyTo(anchor, 9);

                // the row repeats its own file name after the member count, and the checksum is the first thing past it
                var at = ((ReadOnlySpan<byte>)blob).IndexOf(anchor);
                if (at < 0)
                    continue;

                var stored = BitConverter.ToUInt32(blob, at + anchor.Length);
                if (stored == 0)
                    continue;

                var actual = Crc32(path);
                if (actual == stored)
                    continue;

                BitConverter.GetBytes(actual).CopyTo(blob, at + anchor.Length);
                patched++;
                Log.Information("Checksum for {File} read {Stored:X8} in the table catalog, wrote {Actual:X8}", Path.GetFileName(path), stored, actual);
            }

            if (patched > 0)
                File.WriteAllBytes(catalog, blob);
        }

        private static uint Crc32(string path)
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            var crc = 0xFFFFFFFF;
            // sqlite still holds ExcelDB.db open by the time we get here, and File.OpenRead asks for a share mode that denies the writer
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[1 << 20];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                for (var i = 0; i < read; i++)
                    crc = table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        // A cloned skill row names groups of its own - BlacksuitEx01 where the donor said AruEx01 - and the client resolves those names against three databases sitting next to ExcelDB.db that know nothing about the clone, so the student deploys with an EX that costs its cost and does nothing. The donor's rows are copied under the clone's names with every id string inside them rewritten, and the one damage coefficient each LogicEffect group carries is replaced by the value the clone's own tooltip was written from, so the card and the skill agree.
        private void SyncSkillData(string install)
        {
            var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(install), "..", "..", "Preload", "TableBundles"));
            var renames = SkillRenames();
            var stems = renames.Select(r => r.ModStem).Distinct().ToList();

            foreach (var (file, table, keyColumn) in new[]
            {
                ("LevelSkillDataDBSchema.db", "Student", "Key"),
                ("SkillVisualEffectDataDBSchema.db", "Student", "Key"),
                ("LogicEffectDataDBSchema.db", "LogicEffect_PC", "GroupId")
            })
            {
                var path = Path.Combine(dir, file);
                if (!File.Exists(path))
                    continue;

                var premods = path + ".premods";
                if (renames.Count == 0)
                {
                    if (File.Exists(premods))
                    {
                        File.Copy(premods, path, true);
                        File.Delete(premods);
                        logger.LogInformation("No modded skills left - put the shipped {File} back", file);
                    }
                    continue;
                }

                HashSet<string> live;
                using (var probe = CustomCharacterService.Open(path, readOnly: true))
                    live = Keys(probe, table, keyColumn);

                // a steam update lands a fresh table over the spliced one, and that unspliced copy is what the mod rows are deleted against from then on
                if (!File.Exists(premods) || !live.Any(k => stems.Any(s => k.StartsWith(s, StringComparison.Ordinal))))
                    File.Copy(path, premods, true);

                HashSet<string> shipped;
                using (var probe = CustomCharacterService.Open(premods, readOnly: true))
                    shipped = Keys(probe, table, keyColumn);

                using var conn = CustomCharacterService.Open(path, readOnly: false);
                using var tx = conn.BeginTransaction();

                foreach (var stale in live.Where(k => !shipped.Contains(k)))
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = $"DELETE FROM [{table}] WHERE [{keyColumn}] = @k";
                    del.Parameters.AddWithValue("@k", stale);
                    del.ExecuteNonQuery();
                }

                var written = table == "LogicEffect_PC" ? WriteEffects(conn, tx, renames) : WriteSkills(conn, tx, table, renames);
                tx.Commit();

                logger.LogInformation("Wrote {Count} modded skill row(s) into {File}", written, file);
            }
        }

        private List<SkillRename> SkillRenames()
        {
            var mods = characters.List().Where(m => m.Skills != null && m.Skills.Count > 0).ToList();
            if (mods.Count == 0)
                return [];

            var lists = excel.GetTable<CharacterSkillListExcelT>();
            var renames = new List<SkillRename>();
            foreach (var mod in mods)
            {
                var donor = SkillGroups(lists, mod.DonorId, mod.Skills.Keys);
                foreach (var pair in SkillGroups(lists, mod.Id, mod.Skills.Keys))
                {
                    if (!donor.TryGetValue(pair.Key, out var original) || original == pair.Value)
                        continue;
                    renames.Add(new SkillRename(original, pair.Value, original[..^pair.Key.Length], pair.Value[..^pair.Key.Length], pair.Key, mod.Skills[pair.Key], mod.SkillStrings));
                }
            }
            return renames;
        }

        // the manifest names a skill by the suffix it carries in the donor's group ids, and the longest one wins - AruWeaponPassive01 ends with both Passive01 and WeaponPassive01
        private static Dictionary<string, string> SkillGroups(List<CharacterSkillListExcelT> lists, long id, IEnumerable<string> keys)
        {
            var found = new Dictionary<string, string>();
            foreach (var row in lists.Where(r => r.CharacterSkillListGroupId == id))
            {
                foreach (var field in new[] { row.NormalSkillGroupId, row.ExSkillGroupId, row.PublicSkillGroupId, row.PassiveSkillGroupId, row.LeaderSkillGroupId, row.ExtraPassiveSkillGroupId, row.HiddenPassiveSkillGroupId })
                {
                    foreach (var group in field ?? [])
                    {
                        if (string.IsNullOrEmpty(group))
                            continue;
                        var key = keys.Where(k => group.EndsWith(k, StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault();
                        if (key != null)
                            found[key] = group;
                    }
                }
            }
            return found;
        }

        private static int WriteSkills(SqliteConnection conn, SqliteTransaction tx, string table, List<SkillRename> renames)
        {
            var written = 0;
            foreach (var rename in renames)
            {
                byte[] donor;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = $"SELECT Bytes FROM [{table}] WHERE [Key] = @k";
                    cmd.Parameters.AddWithValue("@k", rename.Donor);
                    donor = (byte[])cmd.ExecuteScalar();
                }
                // passives draw nothing, so four of the seven groups miss on the visual table
                if (donor == null)
                    continue;

                using var insert = conn.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = $"INSERT INTO [{table}] ([Key], Bytes) VALUES (@k, @b)";
                insert.Parameters.AddWithValue("@k", rename.Mod);
                insert.Parameters.AddWithValue("@b", RenameStrings(donor, rename.Strings, rename.DonorStem, rename.ModStem));
                insert.ExecuteNonQuery();
                written++;
            }
            return written;
        }

        private static int WriteEffects(SqliteConnection conn, SqliteTransaction tx, List<SkillRename> renames)
        {
            var groups = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT DISTINCT GroupId FROM [LogicEffect_PC]";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    groups.Add(reader.GetString(0));
            }

            var written = 0;
            foreach (var rename in renames)
            {
                var prefix = $"{rename.DonorStem}_{rename.Key}_";
                foreach (var group in groups.Where(g => g.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    var levels = new List<(int Level, byte[] Bytes)>();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "SELECT Level, Bytes FROM [LogicEffect_PC] WHERE GroupId = @g ORDER BY Level";
                        cmd.Parameters.AddWithValue("@g", group);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                            levels.Add((reader.GetInt32(0), (byte[])reader[1]));
                    }

                    var effect = group[prefix.Length..];
                    var tuned = rename.Spec.TryGetProperty(effect, out var values) && values.ValueKind == JsonValueKind.Array;
                    var offset = tuned ? Coefficient(levels.Select(l => l.Bytes).ToList(), group) : -1;

                    foreach (var level in levels)
                    {
                        var bytes = level.Bytes;
                        if (tuned)
                        {
                            bytes = (byte[])bytes.Clone();
                            BitConverter.GetBytes((int)CustomCharacterService.LevelValue(values, level.Level, levels.Count)).CopyTo(bytes, offset);
                        }

                        using var insert = conn.CreateCommand();
                        insert.Transaction = tx;
                        insert.CommandText = "INSERT INTO [LogicEffect_PC] (GroupId, Level, Bytes) VALUES (@g, @l, @b)";
                        insert.Parameters.AddWithValue("@g", $"{rename.ModStem}_{rename.Key}_{effect}");
                        insert.Parameters.AddWithValue("@l", level.Level);
                        insert.Parameters.AddWithValue("@b", RenameStrings(bytes, rename.Strings, rename.DonorStem, rename.ModStem));
                        insert.ExecuteNonQuery();
                        written++;
                    }
                }
            }
            return written;
        }

        // every row in a LogicEffect group is the same bytes but for its level and the one value its tooltip quotes, so diffing the levels locates that value without knowing the row's shape, and goes on locating it after a client update moves it
        private static int Coefficient(List<byte[]> levels, string group)
        {
            var runs = new List<int>();
            for (var i = 0; i < levels[0].Length; i++)
            {
                if (levels.All(b => b[i] == levels[0][i]))
                    continue;
                runs.Add(i);
                while (i < levels[0].Length && levels.Any(b => b[i] != levels[0][i]))
                    i++;
            }

            // the level itself sits at byte 2, behind the union tag and the member count
            var found = runs.Where(start => start > 2).ToList();
            if (found.Count != 1)
                throw new InvalidOperationException($"{group} differs across its levels in {found.Count} places outside the level itself, so which one the tooltip quotes would be a guess");

            var values = levels.Select(b => BitConverter.ToInt32(b, found[0])).ToList();
            if (values.Any(v => v < 100 || v > 1_000_000) || values.Zip(values.Skip(1)).Any(p => p.Second <= p.First))
                throw new InvalidOperationException($"{group} reads back as [{string.Join(", ", values)}] at byte {found[0]}, which is not a coefficient climbing with level");

            return found[0];
        }

        // memorypack writes a row as a tag byte, a member count and then the members end to end with no offset table anywhere, so a name that changes length only needs its own two prefixes fixed and everything behind it slides along
        private static byte[] RenameStrings(byte[] blob, Dictionary<string, string> exact, string donorStem, string modStem)
        {
            var output = new MemoryStream(blob.Length + 0x80);
            var i = 0;
            while (i < blob.Length)
            {
                var text = i + 8 <= blob.Length ? ReadString(blob, i) : null;
                if (text == null)
                {
                    output.WriteByte(blob[i]);
                    i++;
                    continue;
                }

                if (exact == null || !exact.TryGetValue(text, out var replacement))
                    replacement = text.Replace(donorStem, modStem);

                var encoded = Encoding.UTF8.GetBytes(replacement);
                output.Write(BitConverter.GetBytes(~encoded.Length));
                output.Write(BitConverter.GetBytes(replacement.Length));
                output.Write(encoded);
                i += 8 + ~BitConverter.ToInt32(blob, i);
            }
            return output.ToArray();
        }

        private static string ReadString(byte[] blob, int offset)
        {
            var prefix = BitConverter.ToInt32(blob, offset);
            var bytes = ~prefix;
            if (prefix >= -1 || bytes > 4096 || offset + 8 + bytes > blob.Length)
                return null;

            var chars = BitConverter.ToInt32(blob, offset + 4);
            if (chars <= 0 || chars > bytes)
                return null;

            var text = Encoding.UTF8.GetString(blob, offset + 8, bytes);
            return text.Length == chars && text.All(c => c >= 0x20 && c <= 0x7e) ? text : null;
        }

        private static HashSet<string> Keys(SqliteConnection conn, string table, string column)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT [{column}] FROM [{table}]";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                keys.Add(reader.GetString(0));
            return keys;
        }

        private readonly record struct SkillRename(string Donor, string Mod, string DonorStem, string ModStem, string Key, JsonElement Spec, Dictionary<string, string> Strings);

        private List<(string Stem, string DonorStem)> AnimatorStems()
        {
            var mods = characters.List();
            if (mods.Count == 0)
                return [];

            var chars = excel.GetTable<CharacterExcelT>();
            var costumes = excel.GetTable<CostumeExcelT>();

            string Animator(long id)
            {
                var group = chars.FirstOrDefault(c => c.Id == id)?.CostumeGroupId;
                return group == null ? null : costumes.FirstOrDefault(c => c.CostumeGroupId == group)?.AnimatorName;
            }

            var stems = new List<(string, string)>();
            foreach (var mod in mods)
            {
                var mine = Animator(mod.Id);
                var donor = Animator(mod.DonorId);
                if (string.IsNullOrEmpty(mine) || string.IsNullOrEmpty(donor) || mine == donor)
                    continue;

                stems.Add((mine.Replace("_Controller", ""), donor.Replace("_Controller", "")));
            }

            return stems;
        }

        private static byte[] ReadAnimatorTable(string zipPath, bool decrypt = true)
        {
            using var zip = ZipFile.Read(zipPath);
            zip.Password = Convert.ToBase64String(TableService.CreatePassword(Path.GetFileName(zipPath).Replace(".premods", "")));

            using var stream = new MemoryStream();
            zip["animatordatatable.bytes"].Extract(stream);
            var raw = stream.ToArray();

            if (decrypt)
                TableEncryptionService.XOR("AnimatorDataTable", raw);

            return raw;
        }

        private byte[] Build(string source)
        {
            var catalog = AddressableCatalog.Read(File.ReadAllBytes(source));
            var bundleProvider = catalog.IndexOfProvider(BundleProvider);
            var assetProvider = catalog.IndexOfProvider(AssetProvider);
            var bundleType = catalog.IndexOfResourceType(BundleResource);

            // the request options carry a different field set per Addressables version, so a shipped one is cloned rather than composed from what this version happens to want. newer clients write the class name namespace-qualified where older catalogs kept it bare, hence the suffix match.
            var template = catalog.Extras.First(e => e.ClassName != null && e.ClassName.EndsWith("AssetBundleRequestOptions"));

            var added = 0;
            foreach (var mod in characters.List())
            {
                if (string.IsNullOrWhiteSpace(mod.Bundle) || mod.Addressables == null || mod.Addressables.Count == 0)
                    continue;

                var path = Path.Combine(CustomCharacterService.ModsDir, mod.Id.ToString(), mod.Bundle);
                if (!File.Exists(path))
                {
                    logger.LogWarning("Custom character {Id} names bundle {Bundle}, which was never staged - its addressables are being left out", mod.Id, mod.Bundle);
                    continue;
                }

                var options = JObject.Parse(template.Json);
                options["m_BundleName"] = Path.GetFileNameWithoutExtension(mod.Bundle);
                options["m_BundleSize"] = new FileInfo(path).Length;
                options["m_Hash"] = "";
                options["m_Crc"] = 0;

                // retail entries address their bundles as {PlatformUtils.AddressableLoadPath}\name, which the client expands to the GameData folder it loads every other bundle from; SyncClient copies the mod bundle there so the same expansion finds it
                var bundle = new CatalogEntry
                {
                    InternalId = catalog.AddInternalId($@"{{PlatformUtils.AddressableLoadPath}}\{mod.Bundle}"),
                    ProviderIndex = bundleProvider,
                    ExtraIndex = catalog.AddExtra(CatalogKey.Json7(template.AssemblyName, template.ClassName, options.ToString(Newtonsoft.Json.Formatting.None))),
                    ResourceType = bundleType
                };
                var bundleBucket = catalog.AddBucket(CatalogKey.Ascii(mod.Bundle), catalog.AddEntry(bundle));
                bundle.PrimaryKey = catalog.Keys.Count - 1;

                using var hasher = XXHash32.Create();
                hasher.ComputeHash(Encoding.UTF8.GetBytes(mod.Bundle));
                var dependencyHash = unchecked((int)hasher.HashUInt32);

                var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in mod.Addressables)
                {
                    var className = string.IsNullOrWhiteSpace(pair.Value) ? TypeOf(pair.Key) : pair.Value;
                    var resourceType = catalog.IndexOfResourceType(className);
                    if (resourceType < 0)
                    {
                        logger.LogWarning("Custom character {Id} wants {Address} as {Type}, which nothing in the shipped catalog uses", mod.Id, pair.Key, className);
                        continue;
                    }

                    var asset = new CatalogEntry
                    {
                        InternalId = catalog.AddInternalId(pair.Key),
                        ProviderIndex = assetProvider,
                        DependencyBucket = bundleBucket,
                        DependencyHash = dependencyHash,
                        ResourceType = resourceType
                    };
                    var index = catalog.AddEntry(asset);

                    var lookup = LookupKey(pair.Key);
                    catalog.AddBucket(CatalogKey.Ascii(lookup), index);
                    asset.PrimaryKey = catalog.Keys.Count - 1;
                    // the verbatim path is minted alongside so a hand-written Addressables.Load of the real asset path resolves too
                    if (lookup != pair.Key)
                        catalog.AddBucket(CatalogKey.Ascii(pair.Key), index);

                    indices[pair.Key] = index;
                    added++;
                }

                // an alias rides the target's entry as one more key, keeping the internal id the bundle provider actually loads by - a guid minted as its own entry would put the guid where the asset path belongs and load nothing
                if (mod.Aliases != null)
                    foreach (var alias in mod.Aliases)
                    {
                        if (!indices.TryGetValue(alias.Value, out var target))
                        {
                            logger.LogWarning("Custom character {Id} aliases {Alias} to {Address}, which is not among its addressables", mod.Id, alias.Key, alias.Value);
                            continue;
                        }
                        catalog.AddBucket(CatalogKey.Ascii(alias.Key.ToLowerInvariant()), target);
                        added++;
                    }
            }

            logger.LogInformation("Rewrote {Source} with {Added} modded addressable(s), {Total} entries total", Path.GetFileName(source), added, catalog.Entries.Count);
            return catalog.Write();
        }

        // Everything the shipped catalog keeps under AddressableAsset is looked up by the path below that folder with the extension dropped - CostumeExcel.TextureDir is "UIs/01_Common/01_Character/Student_Portrait_Aru" for an asset that lives at Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/Student_Portrait_Aru.png - while everything outside it, the spine rigs included, is keyed by its whole path. Both are lowercased.
        internal static string LookupKey(string address)
        {
            const string prefix = "Assets/_MX/AddressableAsset/";
            if (!address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return address.ToLowerInvariant();

            var trimmed = address.Substring(prefix.Length);
            var dot = trimmed.LastIndexOf('.');
            if (dot > trimmed.LastIndexOf('/'))
                trimmed = trimmed.Substring(0, dot);
            return trimmed.ToLowerInvariant();
        }

        private static string TypeOf(string address)
        {
            var name = address.ToLowerInvariant();
            if (name.EndsWith("_atlas.asset")) return "Spine.Unity.SpineAtlasAsset";
            if (name.EndsWith("_skeletondata.asset")) return "Spine.Unity.SkeletonDataAsset";
            if (name.EndsWith(".mat")) return "UnityEngine.Material";
            if (name.EndsWith(".prefab")) return "UnityEngine.GameObject";
            if (name.EndsWith(".ogg") || name.EndsWith(".wav")) return "UnityEngine.AudioClip";
            if (name.EndsWith(".png") || name.EndsWith(".jpg")) return "UnityEngine.Texture2D";
            return "UnityEngine.TextAsset";
        }
    }
}
