using System.Globalization;
using System.Reflection;
using System.Text.Json;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Ionic.Zip;
using Microsoft.Data.Sqlite;
using Schale.Crypto;
using Schale.FlatData;

namespace Shittim_Server.Services
{
    // A custom student is a donor's rows copied to a free id, so nothing about the copy is distinguishable from a shipped character once it is in the DB. The registry under Data/Mods is the only record that the id was minted here rather than by Nexon, and it is what the Control Center lists.
    public class CustomCharacterService
    {
        // Every per-character table the clone touches, with the column the donor's rows are found by and the id-bearing properties inside the blob. Blanket-rewriting every *Id property would repoint CharacterAIId / PersonalityId / SpawnTemplateId at rows that do not exist, so only these are considered, and only when they actually hold the donor's id.
        private static readonly CloneTable[] Tables =
        {
            new("CharacterExcel", "Id", new[] { "Id", "CostumeGroupId" }),
            new("CostumeExcel", "CostumeGroupId", new[] { "CostumeGroupId", "CostumeUniqueId" }),
            new("CharacterStatExcel", "CharacterId", new[] { "CharacterId" }),
            new("CharacterWeaponExcel", "Id", new[] { "Id" }),
            new("CharacterGearExcel", "Id", new[] { "Id" }),
            new("CharacterTranscendenceExcel", "CharacterId", new[] { "CharacterId" }),
            new("CharacterPotentialExcel", "Id", new[] { "Id" }),
            new("CharacterPotentialRewardExcel", "Id", new[] { "Id" }),
            new("CharacterSkillListExcel", "CharacterSkillListGroupId", new[] { "CharacterSkillListGroupId" }),
            new("CharacterAcademyTagsExcel", "Id", new[] { "Id" }),
            new("LocalizeCharProfileExcel", "CharacterId", new[] { "CharacterId" }),
            new("FavorLevelRewardExcel", "CharacterId", new[] { "CharacterId" }),
            new("CafeInteractionExcel", "CharacterId", new[] { "CharacterId" }),
            new("MemoryLobbyExcel", "Id", new[] { "Id", "CharacterId" }),
            new("CharacterIllustCoordinateExcel", "Id", new[] { "Id" }),
            new("PresetCharacterGroupSettingExcel", "CharacterId", new[] { "CharacterId" }),
            new("CharacterDialogExcel", "CharacterId", new[] { "CharacterId" }),
            new("CharacterDialogSubtitleExcel", "CharacterId", new[] { "CharacterId" }),
        };

        // Anything outside these is left pointing at the donor's rows, which is the whole point - the clone borrows the donor's art, voice, skills and AI.
        private static readonly string[] EditableCharacterFields = { "DevName", "Rarity", "School", "Club", "SquadType", "TacticRole", "WeaponType", "BulletType", "ArmorType", "DefaultStarGrade", "MaxStarGrade" };
        private static readonly string[] EditableProfileFields = { "FullNameEn", "FamilyNameEn", "PersonalNameEn", "StatusMessageEn", "SchoolYearEn", "CharacterAgeEn", "BirthDay", "BirthdayEn", "CharHeightEn", "HobbyEn", "DesignerNameEn", "IllustratorNameEn", "CharacterVoiceEn", "WeaponNameEn", "WeaponDescEn", "ProfileIntroductionEn" };
        // the costume row is where the asset names live, so a mod shipping its own bundle points these at its own addresses instead of leaving them on the donor's
        private static readonly string[] EditableCostumeFields = { "SpineResourceName", "SpineResourceNameDiorama", "ModelPrefabName", "AnimatorName", "CafeModelPrefabName", "EchelonModelPrefabName", "StrategyModelPrefabName", "TextureDir", "CollectionTexturePath", "CollectionBGTexturePath", "CombatStyleTexturePath", "TextureBoss", "InformationPacel" };
        private static readonly string[] EditableLobbyFields = { "PrefabName", "SlotTextureName", "RewardTextureName", "BGMId" };
        private static readonly string[] EditableStatFields = { "MaxHP1", "MaxHP100", "AttackPower1", "AttackPower100", "DefensePower1", "DefensePower100", "HealPower1", "HealPower100", "CriticalPoint", "DodgePoint", "AccuracyPoint", "Range", "AmmoCount", "AmmoCost" };

        private static readonly string[] AssetExtensions = { ".png", ".jpg", ".jpeg", ".bundle", ".skel", ".atlas", ".json", ".bytes", ".ogg", ".wav", ".mp4" };

        public static string ModsDir => Path.Combine(AppContext.BaseDirectory, "Data", "Mods");
        private static string RegistryPath => Path.Combine(ModsDir, "characters.json");

        private readonly ILogger<CustomCharacterService> logger;

        public CustomCharacterService(ILogger<CustomCharacterService> logger)
        {
            this.logger = logger;
        }

        public List<ModCharacter> List()
        {
            if (!File.Exists(RegistryPath))
                return new List<ModCharacter>();
            var json = File.ReadAllText(RegistryPath);
            return JsonSerializer.Deserialize<List<ModCharacter>>(json) ?? new List<ModCharacter>();
        }

        private void SaveRegistry(List<ModCharacter> entries)
        {
            Directory.CreateDirectory(ModsDir);
            File.WriteAllText(RegistryPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }

        public object Inspect(string zipPath)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Zip not found", zipPath);

            var files = new List<string>();
            ZipEntry manifestEntry = null;
            using var zip = ZipFile.Read(zipPath);
            foreach (var entry in zip)
            {
                if (entry.IsDirectory) continue;
                files.Add(entry.FileName);
                if (manifestEntry == null && Path.GetFileName(entry.FileName).Equals("character.json", StringComparison.OrdinalIgnoreCase))
                    manifestEntry = entry;
            }

            var name = Path.GetFileNameWithoutExtension(zipPath);
            long? donorId = null;
            var overrides = new Dictionary<string, JsonElement>();
            if (manifestEntry != null)
            {
                using var ms = new MemoryStream();
                manifestEntry.Extract(ms);
                ms.Position = 0;
                using var doc = JsonDocument.Parse(ms);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("name")) name = prop.Value.GetString();
                    else if (prop.NameEquals("donorId")) donorId = prop.Value.GetInt64();
                    else overrides[prop.Name] = prop.Value.Clone();
                }
            }

            return new
            {
                name,
                donorId,
                hasManifest = manifestEntry != null,
                files,
                assets = files.Where(IsAsset).ToList(),
                overrides = overrides.ToDictionary(kv => kv.Key, kv => kv.Value.ToString())
            };
        }

        public ModCharacter Import(string zipPath, long donorId, long? requestedId, string name, Dictionary<string, JsonElement> overrides)
        {
            var registry = List();
            var dbs = DatabasePaths();
            if (dbs.Count == 0)
                throw new InvalidOperationException("No ExcelDB.db could be located to write to");

            long newId;
            using (var probe = Open(dbs[0], readOnly: true))
            {
                if (!RowExists(probe, "CharacterDBSchema", "Id", donorId))
                    throw new InvalidOperationException($"Donor character {donorId} was not found");

                newId = requestedId ?? NextFreeId(probe);
            }

            // an id free in one copy but used in another would fold the new student into an existing character's rows there, so every copy is cleared before any of them is written
            foreach (var db in dbs)
            {
                using var conn = Open(db, readOnly: true);
                var taken = OccupiedTable(conn, newId);
                if (taken != null)
                    throw new InvalidOperationException($"Character id {newId} is already in use by {taken} in {Path.GetFileName(db)} - pick another, or leave the id blank to take the next free one");
            }

            uint etcKey = 0;
            foreach (var db in dbs)
            {
                Backup(db);
                using var conn = Open(db, readOnly: false);
                using var tx = conn.BeginTransaction();
                etcKey = Clone(conn, tx, donorId, newId, name, overrides);
                tx.Commit();
            }

            var staged = StageAssets(zipPath, newId);

            // the bundle and its addresses describe the package rather than the student, so they are read back off the manifest instead of coming in through the overrides the editor round-trips
            string bundle = null;
            var addressables = new Dictionary<string, string>();
            using (var zip = ZipFile.Read(zipPath))
            {
                var manifest = zip.FirstOrDefault(e => !e.IsDirectory && Path.GetFileName(e.FileName).Equals("character.json", StringComparison.OrdinalIgnoreCase));
                if (manifest != null)
                {
                    using var ms = new MemoryStream();
                    manifest.Extract(ms);
                    ms.Position = 0;
                    using var doc = JsonDocument.Parse(ms);
                    if (doc.RootElement.TryGetProperty("bundle", out var declared))
                        bundle = declared.GetString();
                    if (doc.RootElement.TryGetProperty("addressables", out var addresses))
                    {
                        if (addresses.ValueKind == JsonValueKind.Array)
                            foreach (var address in addresses.EnumerateArray())
                                addressables[address.GetString()] = null;
                        else
                            foreach (var address in addresses.EnumerateObject())
                                addressables[address.Name] = address.Value.GetString();
                    }
                }
            }

            var entry = new ModCharacter
            {
                Id = newId,
                DonorId = donorId,
                Name = name,
                LocalizeEtcKey = etcKey,
                Source = Path.GetFileName(zipPath),
                InstalledAt = DateTime.Now.ToString("s"),
                Assets = staged,
                Bundle = bundle,
                Addressables = addressables
            };
            registry.RemoveAll(e => e.Id == newId);
            registry.Add(entry);
            SaveRegistry(registry);

            logger.LogInformation("Custom character {Id} cloned from {Donor} into {Count} database(s)", newId, donorId, dbs.Count);
            return entry;
        }

        public object Detail(long id)
        {
            var entry = List().FirstOrDefault(e => e.Id == id);
            var dbs = DatabasePaths();
            if (dbs.Count == 0)
                throw new InvalidOperationException("No ExcelDB.db could be located");

            using var conn = Open(dbs[0], readOnly: true);
            var character = LoadOne(conn, "CharacterExcel", "Id", id);
            if (character == null)
                throw new InvalidOperationException($"Character {id} is not in the ExcelDB");

            var profile = LoadOne(conn, "LocalizeCharProfileExcel", "CharacterId", id);
            var stat = LoadOne(conn, "CharacterStatExcel", "CharacterId", id);
            var etcKey = (uint)character.GetType().GetProperty("LocalizeEtcId").GetValue(character);
            var etc = LoadOne(conn, "LocalizeEtcExcel", "Key", etcKey);

            return new
            {
                id,
                donorId = entry?.DonorId,
                source = entry?.Source,
                assets = entry?.Assets ?? new List<string>(),
                name = etc == null ? null : (string)etc.GetType().GetProperty("NameEn").GetValue(etc),
                character = Snapshot(character, EditableCharacterFields),
                profile = profile == null ? null : Snapshot(profile, EditableProfileFields),
                stat = stat == null ? null : Snapshot(stat, EditableStatFields)
            };
        }

        public void Update(long id, string name, Dictionary<string, JsonElement> character, Dictionary<string, JsonElement> profile, Dictionary<string, JsonElement> stat)
        {
            var dbs = DatabasePaths();
            foreach (var db in dbs)
            {
                using var conn = Open(db, readOnly: false);
                using var tx = conn.BeginTransaction();

                if (character != null && character.Count > 0)
                    Rewrite(conn, tx, "CharacterExcel", "Id", id, o => Apply(o, character, EditableCharacterFields));
                if (profile != null && profile.Count > 0)
                    Rewrite(conn, tx, "LocalizeCharProfileExcel", "CharacterId", id, o => Apply(o, profile, EditableProfileFields));
                if (stat != null && stat.Count > 0)
                    Rewrite(conn, tx, "CharacterStatExcel", "CharacterId", id, o => Apply(o, stat, EditableStatFields));

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var row = LoadOne(conn, "CharacterExcel", "Id", id, tx);
                    if (row == null)
                        throw new InvalidOperationException($"Character {id} is not in {Path.GetFileName(db)}");
                    var key = (uint)row.GetType().GetProperty("LocalizeEtcId").GetValue(row);
                    Rewrite(conn, tx, "LocalizeEtcExcel", "Key", key, o => SetNames(o, name));
                }

                tx.Commit();
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var registry = List();
                var entry = registry.FirstOrDefault(e => e.Id == id);
                if (entry != null)
                {
                    entry.Name = name;
                    SaveRegistry(registry);
                }
            }
        }

        public void Remove(long id)
        {
            var registry = List();
            var entry = registry.FirstOrDefault(e => e.Id == id);
            if (entry == null)
                throw new InvalidOperationException($"{id} is not a custom character");

            foreach (var db in DatabasePaths())
            {
                using var conn = Open(db, readOnly: false);
                using var tx = conn.BeginTransaction();
                foreach (var spec in Tables)
                {
                    var key = spec.Type == "CostumeExcel" ? CostumeGroupOf(conn, id, tx) : id;
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = $"DELETE FROM [{TableName(spec.Type)}] WHERE [{spec.Key}] = @k";
                    del.Parameters.AddWithValue("@k", key);
                    del.ExecuteNonQuery();
                }
                if (entry.LocalizeEtcKey != 0)
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM [LocalizeEtcDBSchema] WHERE [Key] = @k";
                    del.Parameters.AddWithValue("@k", entry.LocalizeEtcKey);
                    del.ExecuteNonQuery();
                }
                tx.Commit();
            }

            var dir = Path.Combine(ModsDir, id.ToString());
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);

            registry.Remove(entry);
            SaveRegistry(registry);
            logger.LogInformation("Custom character {Id} removed", id);
        }

        private uint Clone(SqliteConnection conn, SqliteTransaction tx, long donorId, long newId, string name, Dictionary<string, JsonElement> overrides)
        {
            var donorCharacter = LoadOne(conn, "CharacterExcel", "Id", donorId, tx);
            var donorCostumeGroup = (long)donorCharacter.GetType().GetProperty("CostumeGroupId").GetValue(donorCharacter);
            var donorEtcKey = (uint)donorCharacter.GetType().GetProperty("LocalizeEtcId").GetValue(donorCharacter);
            var etcKey = FreeEtcKey(conn, tx, donorEtcKey);

            foreach (var spec in Tables)
            {
                var type = ExcelType(spec.Type);
                var table = TableName(spec.Type);
                var lookup = spec.Type == "CostumeExcel" ? donorCostumeGroup : donorId;

                var rows = new List<byte[]>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = $"SELECT Bytes FROM [{table}] WHERE [{spec.Key}] = @k";
                    cmd.Parameters.AddWithValue("@k", lookup);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        rows.Add((byte[])reader[0]);
                }

                foreach (var bytes in rows)
                {
                    var obj = Unpack(type, bytes);
                    Retarget(obj, spec.Retarget, donorId, donorCostumeGroup, newId);

                    if (spec.Type == "CharacterExcel")
                    {
                        obj.GetType().GetProperty("LocalizeEtcId").SetValue(obj, etcKey);
                        if (overrides != null)
                            Apply(obj, overrides, EditableCharacterFields);
                    }
                    else if (spec.Type == "LocalizeCharProfileExcel" && overrides != null)
                        Apply(obj, overrides, EditableProfileFields);
                    else if (spec.Type == "CharacterStatExcel" && overrides != null)
                        Apply(obj, overrides, EditableStatFields);
                    else if (spec.Type == "CostumeExcel" && overrides != null)
                        Apply(obj, overrides, EditableCostumeFields);
                    else if (spec.Type == "MemoryLobbyExcel" && overrides != null)
                        Apply(obj, overrides, EditableLobbyFields);

                    Insert(conn, tx, table, type, obj, bytes.Length);
                }
            }

            var etcRow = LoadOne(conn, "LocalizeEtcExcel", "Key", donorEtcKey, tx);
            if (etcRow == null)
                throw new InvalidOperationException($"Donor {donorId} points at LocalizeEtc key {donorEtcKey}, which has no row");
            etcRow.GetType().GetProperty("Key").SetValue(etcRow, etcKey);
            SetNames(etcRow, name);
            Insert(conn, tx, "LocalizeEtcDBSchema", ExcelType("LocalizeEtcExcel"), etcRow, 128);

            return etcKey;
        }

        private static void Retarget(object obj, string[] properties, long donorId, long donorCostumeGroup, long newId)
        {
            foreach (var propertyName in properties)
            {
                var property = obj.GetType().GetProperty(propertyName);
                var value = Convert.ToInt64(property.GetValue(obj));

                if (propertyName == "CostumeUniqueId")
                {
                    // costume uniques are group * 100 + variant, so the variant has to survive the move
                    if (value / 100 == donorCostumeGroup)
                        property.SetValue(obj, Convert.ChangeType(newId * 100 + value % 100, property.PropertyType));
                    continue;
                }

                if (value == donorId || (propertyName == "CostumeGroupId" && value == donorCostumeGroup))
                    property.SetValue(obj, Convert.ChangeType(newId, property.PropertyType));
            }
        }

        private static void Insert(SqliteConnection conn, SqliteTransaction tx, string table, Type type, object obj, int sizeHint)
        {
            var columns = new List<string>();
            using (var info = conn.CreateCommand())
            {
                info.Transaction = tx;
                info.CommandText = $"PRAGMA table_info([{table}])";
                using var reader = info.ExecuteReader();
                while (reader.Read())
                    columns.Add(reader.GetString(1));
            }

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            var names = string.Join(", ", columns.Select(c => $"[{c}]"));
            var placeholders = string.Join(", ", columns.Select((c, i) => "@p" + i));
            cmd.CommandText = $"INSERT INTO [{table}] ({names}) VALUES ({placeholders})";
            for (var i = 0; i < columns.Count; i++)
            {
                object value;
                if (columns[i] == "Bytes")
                    value = Pack(type, obj, sizeHint);
                else
                {
                    var property = type.GetProperty(columns[i]) ?? throw new InvalidOperationException($"{table} has a key column {columns[i]} with no matching field on {type.Name}");
                    value = ColumnValue(property.GetValue(obj));
                }
                cmd.Parameters.AddWithValue("@p" + i, value);
            }
            cmd.ExecuteNonQuery();
        }

        private static void Rewrite(SqliteConnection conn, SqliteTransaction tx, string typeName, string keyColumn, object key, Action<object> mutate)
        {
            var type = ExcelType(typeName);
            var table = TableName(typeName);

            var targets = new List<(long RowId, byte[] Bytes)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $"SELECT rowid, Bytes FROM [{table}] WHERE [{keyColumn}] = @k";
                cmd.Parameters.AddWithValue("@k", key);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    targets.Add((reader.GetInt64(0), (byte[])reader[1]));
            }

            foreach (var target in targets)
            {
                var obj = Unpack(type, target.Bytes);
                mutate(obj);
                using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = $"UPDATE [{table}] SET Bytes = @b WHERE rowid = @r";
                upd.Parameters.Add("@b", SqliteType.Blob).Value = Pack(type, obj, target.Bytes.Length);
                upd.Parameters.Add("@r", SqliteType.Integer).Value = target.RowId;
                upd.ExecuteNonQuery();
            }
        }

        private static void Apply(object obj, Dictionary<string, JsonElement> values, string[] allowed)
        {
            foreach (var pair in values)
            {
                if (!allowed.Contains(pair.Key))
                    continue;
                var property = obj.GetType().GetProperty(pair.Key);
                if (property == null)
                    continue;
                property.SetValue(obj, Coerce(property.PropertyType, pair.Value));
            }
        }

        private static void SetNames(object etc, string name)
        {
            foreach (var suffix in new[] { "NameEn", "NameKr", "NameJp", "NameTh", "NameTw" })
                etc.GetType().GetProperty(suffix).SetValue(etc, name);
        }

        private static Dictionary<string, object> Snapshot(object obj, string[] fields)
        {
            var result = new Dictionary<string, object>();
            foreach (var name in fields)
            {
                var property = obj.GetType().GetProperty(name);
                if (property == null)
                    continue;
                var value = property.GetValue(obj);
                result[name] = value is Enum ? value.ToString() : value;
            }
            return result;
        }

        private static object Coerce(Type target, JsonElement value)
        {
            if (target.IsEnum)
                return value.ValueKind == JsonValueKind.String ? Enum.Parse(target, value.GetString(), true) : Enum.ToObject(target, value.GetInt64());
            if (target == typeof(string))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (target == typeof(bool))
                return value.ValueKind == JsonValueKind.String ? bool.Parse(value.GetString()) : value.GetBoolean();
            var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (string.IsNullOrWhiteSpace(raw))
                raw = "0";
            return Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }

        private static object ColumnValue(object value)
        {
            if (value == null) return DBNull.Value;
            if (value is Enum) return Convert.ToInt64(value);
            if (value is bool flag) return flag ? 1L : 0L;
            return value;
        }

        private static Type ExcelType(string name) => typeof(CharacterExcel).Assembly.GetType("Schale.FlatData." + name)
            ?? throw new InvalidOperationException($"No FlatData type named {name}");

        private static string TableName(string typeName) => typeName.Replace("Excel", "DBSchema");

        private static object Unpack(Type type, byte[] bytes)
        {
            var getRoot = type.GetMethod("GetRootAs" + type.Name, BindingFlags.Public | BindingFlags.Static, new[] { typeof(ByteBuffer) });
            var root = getRoot.Invoke(null, new object[] { new ByteBuffer(bytes) });
            return type.GetMethod("UnPack").Invoke(root, null);
        }

        private static byte[] Pack(Type type, object obj, int sizeHint)
        {
            var builder = new FlatBufferBuilder(Math.Max(64, sizeHint + 256));
            var pack = type.GetMethod("Pack", BindingFlags.Public | BindingFlags.Static);
            var offset = pack.Invoke(null, new object[] { builder, obj });
            builder.Finish((int)offset.GetType().GetField("Value").GetValue(offset));
            return builder.SizedByteArray();
        }

        private static object LoadOne(SqliteConnection conn, string typeName, string keyColumn, object key, SqliteTransaction tx = null)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"SELECT Bytes FROM [{TableName(typeName)}] WHERE [{keyColumn}] = @k LIMIT 1";
            cmd.Parameters.AddWithValue("@k", key);
            var bytes = (byte[])cmd.ExecuteScalar();
            return bytes == null ? null : Unpack(ExcelType(typeName), bytes);
        }

        private static long CostumeGroupOf(SqliteConnection conn, long characterId, SqliteTransaction tx)
        {
            var row = LoadOne(conn, "CharacterExcel", "Id", characterId, tx);
            return row == null ? characterId : (long)row.GetType().GetProperty("CostumeGroupId").GetValue(row);
        }

        private static bool RowExists(SqliteConnection conn, string table, string column, long value)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT 1 FROM [{table}] WHERE [{column}] = @k LIMIT 1";
            cmd.Parameters.AddWithValue("@k", value);
            return cmd.ExecuteScalar() != null;
        }

        // Students live in the 10000 block; staying inside it keeps the client's own id-range checks happy.
        private static long NextFreeId(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MAX(Id) FROM CharacterDBSchema WHERE Id BETWEEN 10000 AND 19999";
            var max = cmd.ExecuteScalar();
            var next = max == null || max == DBNull.Value ? 10100L : Convert.ToInt64(max) + 1;
            while (OccupiedTable(conn, next) != null)
            {
                next++;
                if (next > 19999)
                    throw new InvalidOperationException("The 10000 student id block is full");
            }
            return next;
        }

        // Absence from CharacterDBSchema is not enough: an id with no character row can still own costume, dialog or memory-lobby rows, and inserting there would fold the new student into whatever those belong to.
        private static string OccupiedTable(SqliteConnection conn, long id)
        {
            foreach (var spec in Tables)
            {
                if (RowExists(conn, TableName(spec.Type), spec.Key, id))
                    return TableName(spec.Type);
            }
            return null;
        }

        private static uint FreeEtcKey(SqliteConnection conn, SqliteTransaction tx, uint donorKey)
        {
            var key = donorKey + 1;
            while (true)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT 1 FROM [LocalizeEtcDBSchema] WHERE [Key] = @k LIMIT 1";
                cmd.Parameters.AddWithValue("@k", key);
                if (cmd.ExecuteScalar() == null)
                    return key;
                key++;
            }
        }

        private static bool IsAsset(string name) => AssetExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()) && !Path.GetFileName(name).Equals("character.json", StringComparison.OrdinalIgnoreCase);

        // Art travelling with a mod lands next to the registry, and ModCatalogService serves whatever bundle among it the manifest named. Without a bundle and an address list the character just keeps drawing the donor's assets.
        private List<string> StageAssets(string zipPath, long id)
        {
            var staged = new List<string>();
            var dir = Path.Combine(ModsDir, id.ToString());
            using var zip = ZipFile.Read(zipPath);
            foreach (var entry in zip)
            {
                if (entry.IsDirectory || !IsAsset(entry.FileName))
                    continue;
                Directory.CreateDirectory(dir);
                var target = Path.Combine(dir, Path.GetFileName(entry.FileName));
                using (var fs = File.Create(target))
                    entry.Extract(fs);
                staged.Add(Path.GetFileName(entry.FileName));
            }
            return staged;
        }

        private static void Backup(string dbPath)
        {
            var backup = dbPath + ".premods";
            if (!File.Exists(backup))
                File.Copy(dbPath, backup);
        }

        private static SqliteConnection Open(string dbPath, bool readOnly)
        {
            SqliteProvider.EnsureInitialized();
            TableEncryptionService.UseEncryption = false;

            var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite
            }.ToString());
            conn.Open();

            var key = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY");
            if (string.IsNullOrWhiteSpace(key))
                key = Config.Instance.ServerConfiguration.ExcelDbSqlCipherKey;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA key = \"x'{key.Trim()}'\";";
            cmd.ExecuteNonQuery();
            return conn;
        }

        // The server reads Dumped, the resource loader can restore from Downloaded, and the client reads its own copy - a clone written to fewer than all three drifts back the next time one of them is copied over another.
        public static List<string> DatabasePaths()
        {
            var paths = new List<string>
            {
                Path.Combine(ResourceService.DumpedDir, "ExcelDB.db"),
                Path.Combine(ResourceService.DownloadDir, "ExcelDB.db")
            };

            var client = ClientExcelDbPath();
            if (!string.IsNullOrWhiteSpace(client))
                paths.Add(client);

            return paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string ClientExcelDbPath()
        {
            var configured = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH");
            if (string.IsNullOrWhiteSpace(configured))
                configured = Config.Instance.ServerConfiguration.ClientExcelDbPath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            const string relative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\ExcelDB.db";

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

            return SteamGameLocator.FindGameFile(relative) ?? string.Empty;
        }

        private readonly record struct CloneTable(string Type, string Key, string[] Retarget);
    }

    public class ModCharacter
    {
        public long Id { get; set; }
        public long DonorId { get; set; }
        public string Name { get; set; }
        public uint LocalizeEtcKey { get; set; }
        public string Source { get; set; }
        public string InstalledAt { get; set; }
        public List<string> Assets { get; set; } = new();
        public string Bundle { get; set; }
        public Dictionary<string, string> Addressables { get; set; } = new();
    }
}
