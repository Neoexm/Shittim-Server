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
            new("CharacterExcel", "Id", new[] { "Id", "CostumeGroupId", "CharacterPieceItemId", "CombineRecipeId" }),
            new("CostumeExcel", "CostumeGroupId", new[] { "CostumeGroupId", "CostumeUniqueId", "CharacterSkillListGroupId", "CharacterVoiceGroupId" }),
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
            new("CharacterVoiceExcel", "CharacterVoiceGroupId", []),
            // the eleph is an item id and a craft recipe that both happen to equal the character id, so the clone gets its own pair rather than minting the donor's eleph on every dupe
            new("ItemExcel", "Id", new[] { "Id", "GroupId" }),
            new("RecipeIngredientExcel", "Id", new[] { "Id", "IngredientId" }),
            new("RecipeExcel", "Id", new[] { "Id", "RecipeIngredientId", "ParcelId" }),
        };

        // Anything outside these is left pointing at the donor's rows, which is the whole point - the clone borrows the donor's art, voice, skills and AI.
        private static readonly string[] EditableCharacterFields = { "DevName", "Rarity", "School", "Club", "SquadType", "TacticRole", "WeaponType", "BulletType", "ArmorType", "DefaultStarGrade", "MaxStarGrade" };
        private static readonly string[] EditableProfileFields = { "FullNameEn", "FamilyNameEn", "PersonalNameEn", "StatusMessageEn", "SchoolYearEn", "CharacterAgeEn", "BirthDay", "BirthdayEn", "CharHeightEn", "HobbyEn", "DesignerNameEn", "IllustratorNameEn", "CharacterVoiceEn", "WeaponNameEn", "WeaponDescEn", "ProfileIntroductionEn" };
        // the costume row is where the asset names live, so a mod shipping its own bundle points these at its own addresses instead of leaving them on the donor's
        private static readonly string[] EditableCostumeFields = { "SpineResourceName", "SpineResourceNameDiorama", "ModelPrefabName", "AnimatorName", "CafeModelPrefabName", "EchelonModelPrefabName", "StrategyModelPrefabName", "TextureDir", "CollectionTexturePath", "CollectionBGTexturePath", "CombatStyleTexturePath", "TextureSkillCard", "TextureBoss", "InformationPacel", "AnimationSSR" };
        private static readonly string[] EditableItemFields = { "Icon" };
        private static readonly string[] EditableLobbyFields = { "PrefabName", "SlotTextureName", "RewardTextureName", "BGMId" };
        private static readonly string[] EditableStatFields = { "MaxHP1", "MaxHP100", "AttackPower1", "AttackPower100", "DefensePower1", "DefensePower100", "HealPower1", "HealPower100", "CriticalPoint", "DodgePoint", "AccuracyPoint", "Range", "AmmoCount", "AmmoCost" };
        private static readonly string[] EditableWeaponFields = { "ImagePath" };

        // the skill list points at skill groups by name, and a group name is the only thing tying a student to its own skill rows rather than the donor's
        private static readonly string[] SkillGroupFields = { "NormalSkillGroupId", "ExSkillGroupId", "PublicSkillGroupId", "PassiveSkillGroupId", "LeaderSkillGroupId", "ExtraPassiveSkillGroupId", "HiddenPassiveSkillGroupId" };

        private static readonly string[] AssetExtensions = { ".png", ".jpg", ".jpeg", ".bundle", ".skel", ".atlas", ".json", ".bytes", ".ogg", ".wav", ".mp4" };

        public static string ModsDir => Path.Combine(AppContext.BaseDirectory, "Data", "Mods");
        private static string RegistryPath => Path.Combine(ModsDir, "characters.json");

        private readonly ILogger<CustomCharacterService> logger;
        private readonly ExcelTableService excel;

        public CustomCharacterService(ILogger<CustomCharacterService> logger, ExcelTableService excel)
        {
            this.logger = logger;
            this.excel = excel;
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
                overrides
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
            var aliases = new Dictionary<string, string>();
            var skillStrings = new Dictionary<string, string>();
            var clips = new Dictionary<string, string>();
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
                    if (doc.RootElement.TryGetProperty("aliases", out var guids))
                        foreach (var alias in guids.EnumerateObject())
                            aliases[alias.Name] = alias.Value.GetString();
                    if (doc.RootElement.TryGetProperty("strings", out var renamed))
                        foreach (var pair in renamed.EnumerateObject())
                            skillStrings[pair.Name] = pair.Value.GetString();
                    if (doc.RootElement.TryGetProperty("media", out var voices))
                        foreach (var clip in voices.EnumerateObject())
                            clips[clip.Name] = clip.Value.GetString();
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
                Addressables = addressables,
                Aliases = aliases,
                Skills = Section(overrides, "skills"),
                SkillStrings = skillStrings,
                Media = clips
            };
            registry.RemoveAll(e => e.Id == newId);
            registry.Add(entry);
            SaveRegistry(registry);
            excel.DropCache();

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

            excel.DropCache();

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
                var piece = LoadOne(conn, "ItemExcel", "Id", id, tx);
                DropSkills(conn, tx, id);
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
                // the eleph name is not in the registry, so it is read back off the item row before that row goes
                if (piece != null)
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM [LocalizeEtcDBSchema] WHERE [Key] = @k";
                    del.Parameters.AddWithValue("@k", (uint)piece.GetType().GetProperty("LocalizeEtcId").GetValue(piece));
                    del.ExecuteNonQuery();
                }
                tx.Commit();
            }

            var dir = Path.Combine(ModsDir, id.ToString());
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);

            registry.Remove(entry);
            SaveRegistry(registry);
            excel.DropCache();
            logger.LogInformation("Custom character {Id} removed", id);
        }

        // A game update or a resource re-download replaces the client's ExcelDB.db wholesale, which takes every cloned student back out of it while the account still owns one. The client cannot resolve the id anywhere in its login sync and dies there - popup, then no lobby - so any student the server's dump has and a copy does not is put back before anyone logs in.
        public int SyncMissing()
        {
            var dbs = DatabasePaths();
            if (dbs.Count < 2)
                return 0;

            using var source = Open(dbs[0], readOnly: true);
            var known = StudentIds(source);
            var copied = 0;

            foreach (var db in dbs.Skip(1))
            {
                List<long> missing;
                using (var probe = Open(db, readOnly: true))
                    missing = known.Except(StudentIds(probe)).ToList();
                if (missing.Count == 0)
                    continue;

                using (var target = Open(db, readOnly: false))
                using (var tx = target.BeginTransaction())
                {
                    foreach (var id in missing)
                        CopyCharacter(source, target, tx, id);
                    tx.Commit();
                }

                copied += missing.Count;
                logger.LogInformation("Restored student {Ids} into {Db}", string.Join(", ", missing), db);
            }

            return copied;
        }

        private static void CopyCharacter(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, long id)
        {
            var character = LoadOne(source, "CharacterExcel", "Id", id);
            var costumeGroup = (long)character.GetType().GetProperty("CostumeGroupId").GetValue(character);
            var etcKey = (uint)character.GetType().GetProperty("LocalizeEtcId").GetValue(character);

            foreach (var spec in Tables)
                CopyRows(source, target, tx, TableName(spec.Type), spec.Key, spec.Type == "CostumeExcel" ? costumeGroup : id);

            CopyRows(source, target, tx, "LocalizeEtcDBSchema", "Key", etcKey);

            var piece = LoadOne(source, "ItemExcel", "Id", id);
            if (piece != null)
                CopyRows(source, target, tx, "LocalizeEtcDBSchema", "Key", (uint)piece.GetType().GetProperty("LocalizeEtcId").GetValue(piece));
        }

        // The blobs move across verbatim - both copies are the same schema at the same version, and repacking them through FlatData would rewrite fields the clone never touched.
        private static void CopyRows(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, string table, string keyColumn, object key)
        {
            using (var taken = target.CreateCommand())
            {
                taken.Transaction = tx;
                taken.CommandText = $"SELECT 1 FROM [{table}] WHERE [{keyColumn}] = @k LIMIT 1";
                taken.Parameters.AddWithValue("@k", key);
                if (taken.ExecuteScalar() != null)
                    return;
            }

            using var read = source.CreateCommand();
            read.CommandText = $"SELECT * FROM [{table}] WHERE [{keyColumn}] = @k";
            read.Parameters.AddWithValue("@k", key);
            using var reader = read.ExecuteReader();

            string insert = null;
            while (reader.Read())
            {
                if (insert == null)
                {
                    var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                    insert = $"INSERT INTO [{table}] ({string.Join(", ", columns.Select(c => $"[{c}]"))}) VALUES ({string.Join(", ", columns.Select((c, i) => "@p" + i))})";
                }

                using var write = target.CreateCommand();
                write.Transaction = tx;
                write.CommandText = insert;
                for (var i = 0; i < reader.FieldCount; i++)
                    write.Parameters.AddWithValue("@p" + i, reader.GetValue(i));
                write.ExecuteNonQuery();
            }
        }

        private static HashSet<long> StudentIds(SqliteConnection conn)
        {
            var ids = new HashSet<long>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM CharacterDBSchema WHERE Id BETWEEN 10000 AND 19999";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetInt64(0));
            return ids;
        }

        private uint Clone(SqliteConnection conn, SqliteTransaction tx, long donorId, long newId, string name, Dictionary<string, JsonElement> overrides)
        {
            var donorCharacter = LoadOne(conn, "CharacterExcel", "Id", donorId, tx);
            var donorCostumeGroup = (long)donorCharacter.GetType().GetProperty("CostumeGroupId").GetValue(donorCharacter);
            var donorEtcKey = (uint)donorCharacter.GetType().GetProperty("LocalizeEtcId").GetValue(donorCharacter);
            var etcKey = FreeKey(conn, tx, "LocalizeEtcDBSchema", "Key", donorEtcKey);
            var donorPiece = LoadOne(conn, "ItemExcel", "Id", (long)donorCharacter.GetType().GetProperty("CharacterPieceItemId").GetValue(donorCharacter), tx);
            var donorPieceEtcKey = (uint)donorPiece.GetType().GetProperty("LocalizeEtcId").GetValue(donorPiece);
            var pieceEtcKey = FreeKey(conn, tx, "LocalizeEtcDBSchema", "Key", donorPieceEtcKey);

            var donorCostume = LoadOne(conn, "CostumeExcel", "CostumeGroupId", donorCostumeGroup, tx);
            var donorVoiceGroup = (long)donorCostume.GetType().GetProperty("CharacterVoiceGroupId").GetValue(donorCostume);

            var stem = StemOf(overrides, name);
            var skills = Section(overrides, "skills");
            var dialog = Section(overrides, "dialog");
            var media = Section(overrides, "media");
            var groups = skills.Count == 0 ? new List<SkillGroup>() : MapSkillGroups(conn, tx, donorId, stem, skills);
            var renames = groups.ToDictionary(g => g.Donor, g => g.Mod);
            var spoken = new Dictionary<string, int>();

            // the one-shot voice rows are keyed by a hash of nothing the clone can derive, and the only thing that ever names them is the dialog row's VoiceId, so each one is cloned under a free id and the dialog is pointed at it
            var dubbed = new Dictionary<uint, uint>();
            uint Redub(uint donorVoice)
            {
                if (dubbed.TryGetValue(donorVoice, out var mine))
                    return mine;
                var row = LoadOne(conn, "VoiceExcel", "Id", donorVoice, tx);
                mine = FreeKey(conn, tx, "VoiceDBSchema", "Id", donorVoice);
                row.GetType().GetProperty("Id").SetValue(row, mine);
                Revoice(row, stem);
                Insert(conn, tx, "VoiceDBSchema", ExcelType("VoiceExcel"), row, 128);
                dubbed[donorVoice] = mine;
                return mine;
            }

            foreach (var spec in Tables)
            {
                var type = ExcelType(spec.Type);
                var table = TableName(spec.Type);
                var lookup = spec.Type == "CostumeExcel" ? donorCostumeGroup : spec.Type == "CharacterVoiceExcel" ? donorVoiceGroup : donorId;

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
                    else if (spec.Type == "CharacterWeaponExcel" && overrides != null)
                        Apply(obj, overrides, EditableWeaponFields);
                    else if (spec.Type == "CharacterSkillListExcel" && renames.Count > 0)
                        RenameSkillGroups(obj, renames);
                    else if (spec.Type == "CharacterVoiceExcel")
                    {
                        // the group has to move even when the package ships no clips of its own, or the clone's 46 rows land back on top of the donor's and every line plays twice
                        obj.GetType().GetProperty("CharacterVoiceUniqueId").SetValue(obj, newId * 100 + (long)obj.GetType().GetProperty("CharacterVoiceUniqueId").GetValue(obj) % 100);
                        obj.GetType().GetProperty("CharacterVoiceGroupId").SetValue(obj, newId);
                        if (media.Count > 0)
                            Revoice(obj, stem);
                    }
                    else if (spec.Type == "CharacterDialogExcel")
                    {
                        if (dialog.Count > 0)
                            Speak(obj, dialog, spoken);
                        if (media.Count > 0)
                        {
                            var voices = (List<uint>)obj.GetType().GetProperty("VoiceId").GetValue(obj);
                            for (var i = 0; i < voices.Count; i++)
                                voices[i] = Redub(voices[i]);
                        }
                    }
                    else if (spec.Type == "ItemExcel")
                    {
                        obj.GetType().GetProperty("LocalizeEtcId").SetValue(obj, pieceEtcKey);
                        if (overrides != null)
                            Apply(obj, overrides, EditableItemFields);
                    }

                    Insert(conn, tx, table, type, obj, bytes.Length);
                }
            }

            foreach (var group in groups)
                CloneSkill(conn, tx, donorId, newId, group, skills[group.Key]);

            var etcRow = LoadOne(conn, "LocalizeEtcExcel", "Key", donorEtcKey, tx);
            if (etcRow == null)
                throw new InvalidOperationException($"Donor {donorId} points at LocalizeEtc key {donorEtcKey}, which has no row");
            etcRow.GetType().GetProperty("Key").SetValue(etcRow, etcKey);
            var donorNames = new[] { "NameEn", "NameKr", "NameJp", "NameTh", "NameTw" }.ToDictionary(s => s, s => (string)etcRow.GetType().GetProperty(s).GetValue(etcRow));
            SetNames(etcRow, name);
            Insert(conn, tx, "LocalizeEtcDBSchema", ExcelType("LocalizeEtcExcel"), etcRow, 128);

            // the eleph's name and its flavour line both wrap the student's name in a per-language frame - "Aru's Eleph", "아루의 엘프", "The mystical Eleph of Aru." - so the donor's name is swapped out of them rather than the whole string being replaced
            var pieceEtcRow = LoadOne(conn, "LocalizeEtcExcel", "Key", donorPieceEtcKey, tx);
            pieceEtcRow.GetType().GetProperty("Key").SetValue(pieceEtcRow, pieceEtcKey);
            foreach (var pair in donorNames)
            {
                foreach (var field in new[] { pair.Key, pair.Key.Replace("Name", "Description") })
                {
                    var property = pieceEtcRow.GetType().GetProperty(field);
                    var text = (string)property.GetValue(pieceEtcRow);
                    if (!string.IsNullOrEmpty(pair.Value) && !string.IsNullOrEmpty(text))
                        property.SetValue(pieceEtcRow, text.Replace(pair.Value, name));
                }
            }
            Insert(conn, tx, "LocalizeEtcDBSchema", ExcelType("LocalizeEtcExcel"), pieceEtcRow, 128);

            return etcKey;
        }

        private static void Retarget(object obj, string[] properties, long donorId, long donorCostumeGroup, long newId)
        {
            foreach (var propertyName in properties)
            {
                var property = obj.GetType().GetProperty(propertyName);
                if (property.GetValue(obj) is List<long> ingredients)
                {
                    for (var i = 0; i < ingredients.Count; i++)
                    {
                        if (ingredients[i] == donorId)
                            ingredients[i] = newId;
                    }
                    continue;
                }

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

        private static Dictionary<string, JsonElement> Section(Dictionary<string, JsonElement> overrides, string name)
        {
            if (overrides == null || !overrides.TryGetValue(name, out var node) || node.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, JsonElement>();
            return node.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        }

        // skill groups, effect ids and the mod's own FX all spell the student the same way DevName's first segment does - AruEx01, Aru_Ex01_Effect01, FX_Aru_Original_Ex01_Projectile
        private static string StemOf(Dictionary<string, JsonElement> overrides, string name)
        {
            if (overrides != null && overrides.TryGetValue("DevName", out var dev) && dev.ValueKind == JsonValueKind.String)
                return dev.GetString().Split('_')[0];
            return name;
        }

        // the manifest names skills by the suffix they carry in the donor's group ids, and the longest one wins - AruWeaponPassive01 ends with both Passive01 and WeaponPassive01
        private static List<SkillGroup> MapSkillGroups(SqliteConnection conn, SqliteTransaction tx, long donorId, string stem, Dictionary<string, JsonElement> skills)
        {
            var found = new List<SkillGroup>();
            var seen = new HashSet<string>();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT Bytes FROM [CharacterSkillListDBSchema] WHERE CharacterSkillListGroupId = @k";
            cmd.Parameters.AddWithValue("@k", donorId);
            var blobs = new List<byte[]>();
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    blobs.Add((byte[])reader[0]);

            foreach (var blob in blobs)
            {
                var row = Unpack(ExcelType("CharacterSkillListExcel"), blob);
                foreach (var field in SkillGroupFields)
                {
                    foreach (var group in (List<string>)row.GetType().GetProperty(field).GetValue(row))
                    {
                        if (string.IsNullOrEmpty(group) || !seen.Add(group))
                            continue;
                        var key = skills.Keys.Where(k => group.EndsWith(k, StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault();
                        if (key != null)
                            found.Add(new SkillGroup(group, stem + key, key));
                    }
                }
            }
            return found;
        }

        // a student cloned before the skill tables were split off still names the donor's groups, so only groups nobody else lists are dropped
        private static void DropSkills(SqliteConnection conn, SqliteTransaction tx, long id)
        {
            var type = ExcelType("CharacterSkillListExcel");
            var mine = new HashSet<string>();
            var theirs = new HashSet<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT CharacterSkillListGroupId, Bytes FROM [CharacterSkillListDBSchema]";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var target = reader.GetInt64(0) == id ? mine : theirs;
                    var row = Unpack(type, (byte[])reader[1]);
                    foreach (var field in SkillGroupFields)
                        foreach (var group in (List<string>)row.GetType().GetProperty(field).GetValue(row))
                            if (!string.IsNullOrEmpty(group))
                                target.Add(group);
                }
            }
            mine.ExceptWith(theirs);

            foreach (var group in mine)
            {
                var keys = new List<uint>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "SELECT Bytes FROM [SkillDBSchema] WHERE GroupId = @g";
                    cmd.Parameters.AddWithValue("@g", group);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var skill = Unpack(ExcelType("SkillExcel"), (byte[])reader[0]);
                        keys.Add((uint)skill.GetType().GetProperty("LocalizeSkillId").GetValue(skill));
                    }
                }
                foreach (var key in keys)
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM [LocalizeSkillDBSchema] WHERE [Key] = @k";
                    del.Parameters.AddWithValue("@k", key);
                    del.ExecuteNonQuery();
                }
                using (var del = conn.CreateCommand())
                {
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM [SkillDBSchema] WHERE GroupId = @g";
                    del.Parameters.AddWithValue("@g", group);
                    del.ExecuteNonQuery();
                }
            }
        }

        private static void RenameSkillGroups(object row, Dictionary<string, string> renames)
        {
            foreach (var field in SkillGroupFields)
            {
                var list = (List<string>)row.GetType().GetProperty(field).GetValue(row);
                for (var i = 0; i < list.Count; i++)
                {
                    if (renames.TryGetValue(list[i], out var replacement))
                        list[i] = replacement;
                }
            }
        }

        private static void CloneSkill(SqliteConnection conn, SqliteTransaction tx, long donorId, long newId, SkillGroup group, JsonElement spec)
        {
            var type = ExcelType("SkillExcel");
            var rows = new List<byte[]>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT Bytes FROM [SkillDBSchema] WHERE GroupId = @g ORDER BY Level";
                cmd.Parameters.AddWithValue("@g", group.Donor);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    rows.Add((byte[])reader[0]);
            }
            if (rows.Count == 0)
                throw new InvalidOperationException($"Donor skill group {group.Donor} has no rows to clone");

            foreach (var bytes in rows)
            {
                var obj = Unpack(type, bytes);
                var props = obj.GetType();
                foreach (var field in new[] { "GroupId", "SkillDataKey", "VisualDataKey" })
                {
                    if ((string)props.GetProperty(field).GetValue(obj) == group.Donor)
                        props.GetProperty(field).SetValue(obj, group.Mod);
                }
                // skill ids are the character id shifted up three places plus a slot, so the slot survives the move
                props.GetProperty("Id").SetValue(obj, newId * 1000 + ((long)props.GetProperty("Id").GetValue(obj) - donorId * 1000));

                var level = (int)props.GetProperty("Level").GetValue(obj);
                if (spec.TryGetProperty("Cost", out var cost))
                    props.GetProperty("SkillCost").SetValue(obj, cost.GetInt32());

                var donorText = LoadOne(conn, "LocalizeSkillExcel", "Key", (uint)props.GetProperty("LocalizeSkillId").GetValue(obj), tx);
                if (donorText != null)
                {
                    var key = FreeKey(conn, tx, "LocalizeSkillDBSchema", "Key", (uint)props.GetProperty("LocalizeSkillId").GetValue(obj));
                    props.GetProperty("LocalizeSkillId").SetValue(obj, key);
                    donorText.GetType().GetProperty("Key").SetValue(donorText, key);
                    foreach (var slot in new[] { "Name", "Description", "SkillInvokeLocalize" })
                    {
                        if (!spec.TryGetProperty(slot + "En", out var template))
                            continue;
                        var filled = Fill(template.GetString(), spec, level, rows.Count);
                        foreach (var lang in new[] { "En", "Kr", "Jp", "Th", "Tw" })
                            donorText.GetType().GetProperty(slot + lang).SetValue(donorText, filled);
                    }
                    Insert(conn, tx, "LocalizeSkillDBSchema", ExcelType("LocalizeSkillExcel"), donorText, 512);
                }

                Insert(conn, tx, "SkillDBSchema", type, obj, bytes.Length);
            }
        }

        // a coefficient is stored hundredths of a percent and shown three different ways across one student's kit - 1400 as 14%, 2016 as 20.1%, 2000 as a flat 2000
        private static string Fill(string template, JsonElement spec, int level, int levels)
        {
            foreach (var prop in spec.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array)
                    continue;
                var value = LevelValue(prop.Value, level, levels);
                template = template.Replace("{" + prop.Name + ":raw}", value.ToString(CultureInfo.InvariantCulture))
                    .Replace("{" + prop.Name + ":1}", (Math.Truncate(value / 10m) / 10m).ToString("0.0", CultureInfo.InvariantCulture))
                    .Replace("{" + prop.Name + "}", (value / 100).ToString(CultureInfo.InvariantCulture));
            }
            return template;
        }

        internal static long LevelValue(JsonElement values, int level, int levels)
        {
            var stops = values.EnumerateArray().Select(e => e.GetInt64()).ToList();
            if (stops.Count >= levels)
                return stops[level - 1];
            if (stops.Count == 1 || levels == 1)
                return stops[0];
            return stops[0] + (stops[^1] - stops[0]) * (level - 1) / (levels - 1);
        }

        // dialog rows come back in the order they were written, so a category's lines are handed out down that same order
        private static void Speak(object row, Dictionary<string, JsonElement> dialog, Dictionary<string, int> spoken)
        {
            var props = row.GetType();
            var slot = $"{props.GetProperty("DialogCategory").GetValue(row)}/{props.GetProperty("DialogCondition").GetValue(row)}/{props.GetProperty("GroupId").GetValue(row)}";
            if (!dialog.TryGetValue(slot, out var lines) || lines.GetArrayLength() == 0)
                return;
            spoken.TryGetValue(slot, out var used);
            spoken[slot] = used + 1;
            var text = lines[used % lines.GetArrayLength()].GetString();
            foreach (var lang in new[] { "EN", "KR", "JP", "TH", "TW" })
                props.GetProperty("Localize" + lang).SetValue(row, text);
        }

        // a voice path is Audio/VOC_JP/JP_Aru/Aru_Battle_In_1, one language folder and the same stem twice, so the stem is read back off the folder rather than out of the donor's DevName
        private static void Revoice(object row, string stem)
        {
            var paths = (List<string>)row.GetType().GetProperty("Path").GetValue(row);
            for (var i = 0; i < paths.Count; i++)
            {
                var parts = paths[i].Split('/');
                paths[i] = paths[i].Replace(parts[^2].Substring(parts[^2].IndexOf('_') + 1), stem);
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
                    var property = obj.GetType().GetProperty(columns[i]) ?? throw new InvalidOperationException($"{table} has a key column {columns[i]} with no matching field on {type.Name}");
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
            if (target == typeof(List<string>))
                return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(e => e.GetString()).ToList() : new List<string> { value.GetString() };
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

        private static uint FreeKey(SqliteConnection conn, SqliteTransaction tx, string table, string column, uint donorKey)
        {
            var key = donorKey + 1;
            while (true)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"SELECT 1 FROM [{table}] WHERE [{column}] = @k LIMIT 1";
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

        internal static SqliteConnection Open(string dbPath, bool readOnly)
        {
            SqliteProvider.EnsureInitialized();
            TableEncryptionService.UseEncryption = false;

            // a pooled handle outlives Dispose still holding write access, and ExcelTableService probes the same file with a plain File.OpenRead that a write handle refuses - the sync straight after an import then reads every table empty
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
                Pooling = false
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
        private readonly record struct SkillGroup(string Donor, string Mod, string Key);
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
        // extra catalog keys over addresses already listed above - an AssetReference resolves by the asset's guid rather than any address string, so the guid maps here to the address whose entry it should share
        public Dictionary<string, string> Aliases { get; set; }
        // the skill section is kept whole because the coefficients the tooltips were written from have to be written into the client's battle tables too, and that happens at every sync rather than once at import
        public Dictionary<string, JsonElement> Skills { get; set; }
        public Dictionary<string, string> SkillStrings { get; set; }
        // voice clips sit outside addressables entirely - the client resolves them through its own media catalog - so the destination path each staged clip answers to is carried here rather than in Addressables
        public Dictionary<string, string> Media { get; set; }
    }
}
