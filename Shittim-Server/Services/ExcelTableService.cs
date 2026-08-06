using System.Collections.Concurrent;
using System.Reflection;
using BlueArchiveAPI.Configuration;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.Crypto;

namespace BlueArchiveAPI.Services
{
    public class ExcelTableService
    {
        private readonly ConcurrentDictionary<Type, object> caches = [];

        // TableEncryptionService.UseEncryption is a global static selecting XOR-decryption of every string and numeric field, read per field deep inside the generated UnPackTo methods rather than once up front.
        // The two branches below set it to opposite values and ConcurrentDictionary.GetOrAdd does not serialize factories across keys, so two first-time loads of different tables can flip the flag out from under each other's in-flight unpack and produce garbled rows that the row-level catch swallows. Both branches are live - .bytes files and ExcelDB.db coexist in Resources/Dumped.
        // Threading the flag through as a parameter means regenerating several hundred FlatData files, so loads are serialized instead; this contends only on the first load of each table, every later call hits the lock-free TryGetValue above.
        internal static readonly object loadLock = new();

        public static string ResourceDir = Path.Join(Path.GetDirectoryName(AppContext.BaseDirectory), "Resources");
        public static string DumpedDir = Path.Combine(ResourceDir, "Dumped");

        public List<T> GetTable<T>(bool bypassCache = false, bool isExcelDB = false)
        {
            var type = typeof(T);
            List<T> unpacked;

            if (!bypassCache && caches.TryGetValue(type, out var cache))
                return (List<T>)cache;

            unpacked = (List<T>)caches.GetOrAdd(type, (t) =>
            {
                lock (loadLock)
                {
                try
                {
                var excelDir = Path.Combine(DumpedDir, "Excel");
                var excelDBDir = Path.Combine(DumpedDir, "ExcelDB.db");

                string baseTypeName = type.Name.EndsWith("T") ? type.Name[..^1] : type.Name;
                var excelName = baseTypeName + "Table";
                var schemaName = baseTypeName.Replace("Excel", "DBSchema");

                var bytesFileName = $"{excelName.ToLower()}.bytes";
                var bytesFilePath = Path.Join(excelDir, bytesFileName);
                
                if (File.Exists(bytesFilePath) && !isExcelDB)
                {
                    TableEncryptionService.UseEncryption = true;

                    var fbType = type.Assembly.GetType($"{type.Namespace}.{excelName}");
                    if (fbType == null)
                        throw new InvalidOperationException($"FlatBuffer type '{type.Namespace}.{excelName}' not found for {type.FullName}");

                    var bytes = File.ReadAllBytes(bytesFilePath);
                    TableEncryptionService.XOR(excelName, bytes);

                    var byteBuffer = new ByteBuffer(bytes);
                    var getRootMethod = fbType.GetMethod($"GetRootAs{excelName}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)]);
                    if (getRootMethod == null)
                        throw new MissingMethodException($"Could not find GetRootAs{excelName} on type {fbType.FullName}");

                    var flatInstance = getRootMethod.Invoke(null, [byteBuffer]);
                    var unpackMethod = fbType.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public);
                    if (unpackMethod == null)
                        throw new MissingMethodException($"Could not find UnPack method on type {fbType.FullName}");

                    var unpackedInstance = unpackMethod.Invoke(flatInstance, null);
                    var dataListProperty = unpackedInstance.GetType().GetProperty("DataList", BindingFlags.Public | BindingFlags.Instance);
                    if (dataListProperty == null)
                        throw new MissingMemberException($"Could not find 'DataList' property on type {unpackedInstance.GetType().FullName}");

                    return dataListProperty.GetValue(unpackedInstance);
                }
                else if (File.Exists(excelDBDir))
                {
                    TableEncryptionService.UseEncryption = false;
                    var excelList = new List<T>();

                    var fbType = type.Assembly.GetType($"{type.Namespace}.{baseTypeName}");
                    if (fbType == null)
                        throw new InvalidOperationException($"FlatBuffer type '{type.Namespace}.{baseTypeName}' not found for {type.FullName}");

                    using (var dbConnection = OpenExcelDbConnection(excelDBDir))
                    {
                        var command = dbConnection.CreateCommand();
                        command.CommandText = $"SELECT Bytes FROM [{schemaName}]";

                        var skippedRows = 0;
                        var widestRow = 0;
                        var properties = type.GetProperties();
                        var slotMap = ClientExcelSchema.SlotMapFor(type, baseTypeName);
                        int compiledMisfits = 0, candidateMisfits = 0, scoredRows = 0;
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                var rowBytes = (byte[])reader[0];
                                widestRow = Math.Max(widestRow, RowSlotCount(rowBytes));
                                if (slotMap != null && scoredRows < 200)
                                {
                                    var (compiled, candidate) = RealignedRowReader.ScoreMaps(rowBytes, properties, slotMap);
                                    compiledMisfits += compiled;
                                    candidateMisfits += candidate;
                                    scoredRows++;
                                }
                                var byteBuffer = new ByteBuffer(rowBytes);
                                var getRootMethod = fbType.GetMethod($"GetRootAs{baseTypeName}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)])
                                    ?? throw new MissingMethodException($"Could not find GetRootAs{baseTypeName} on type {fbType.FullName}");

                                var flatInstance = getRootMethod.Invoke(null, [byteBuffer]);
                                var unpackMethod = fbType.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public)
                                    ?? throw new MissingMethodException($"Could not find UnPack method on type {fbType.FullName}");

                                var unpackedInstance = (T)unpackMethod.Invoke(flatInstance, null);
                                excelList.Add(unpackedInstance);
                                }
                                catch (Exception rowEx)
                                {
                                    // Skip just the bad row - a row whose bytes don't line up with the current FlatBuffer schema otherwise reaches the catch below and discards the ENTIRE table, silently turning every lookup against it into "not found".
                                    if (skippedRows++ == 0)
                                        Console.WriteLine($"[ExcelTableService] WARNING: {baseTypeName} has rows that do not match the current schema ({rowEx.GetBaseException().Message}); skipping them");
                                }
                            }
                        }

                        // The client's field order only says where our fields sit in the build it came from, and it is a name match, so it also invents drift for reader properties that are not schema fields at all. Realign only where the rows themselves say the compiled offsets are wrong: rows the model cannot read, rows carrying more slots than the model has fields, or rows that fit the client's order better than ours.
                        // GoodsExcel is the case this protects - it name-aligns as drifted, its rows are exactly as wide as the model, and re-reading it changes nothing.
                        if (slotMap != null && (skippedRows > 0 || widestRow > properties.Length || candidateMisfits < compiledMisfits))
                        {
                            var realigned = new List<T>();
                            var unreadable = 0;
                            using (var rereader = command.ExecuteReader())
                            {
                                while (rereader.Read())
                                {
                                    try { realigned.Add((T)RealignedRowReader.Read((byte[])rereader[0], type, slotMap)); }
                                    catch { unreadable++; }
                                }
                            }

                            if (realigned.Count >= excelList.Count)
                            {
                                Console.WriteLine($"[ExcelTableService] {baseTypeName}: layout drift detected; realigned {realigned.Count} row(s) against the installed client's schema" +
                                    (unreadable > 0 ? $" ({unreadable} still unreadable)" : "") + ". Regenerate the model to make this permanent.");
                                return realigned;
                            }
                            Console.WriteLine($"[ExcelTableService] WARNING: {baseTypeName}: realignment read fewer rows ({realigned.Count}) than the compiled model ({excelList.Count}); keeping the compiled model's result");
                        }

                        if (skippedRows > 0)
                            Console.WriteLine($"[ExcelTableService] WARNING: {baseTypeName}: skipped {skippedRows} unreadable row(s), loaded {excelList.Count}");
                    }

                    return excelList;
                }
                else
                {
                    Console.WriteLine($"[ExcelTableService] WARNING: No Excel data found for {baseTypeName}, returning empty list");
                    return new List<T>();
                }
                }
                catch (Exception ex)
                {
                    // A dumped table whose bytes don't match the current Schale FlatBuffer schema (e.g. RaidStageExcel.GroundDevName offset mismatch) would otherwise throw out of the handler and become Error 500 -> client shows "Server failed to process request. Returning to the title screen."
                    // Degrade to an empty table instead so the request still completes.
                    Console.WriteLine($"[ExcelTableService] WARNING: failed to load {type.Name} table ({ex.GetBaseException().Message}); degrading to empty table");
                    return new List<T>();
                }
                }
            });

            return unpacked;
        }

        // vtable layout: [0] its own size in bytes, [2] the table's inline size, then one uint16 per field.
        private static int RowSlotCount(byte[] row)
        {
            if (row.Length < 8) return 0;
            var table = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(row);
            var vtable = table - System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(table));
            return (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable)) - 4) / 2;
        }

        // The ExcelDB key rotates between some game updates; when it does, every DB-backed table silently degrades to empty and the failure only surfaces as broken client screens much later.
        // No-ops when ExcelDB.db is absent (a .bytes-only / custom-Excel setup) or is stored as a plain, unencrypted SQLite file.
        public static void ValidateExcelDbKey()
        {
            var excelDbPath = Path.Combine(DumpedDir, "ExcelDB.db");
            if (!File.Exists(excelDbPath))
            {
                // Downloaded is where the CDN files land, Dumped is what the server reads, and ExcelDB.db is copied between them while the zips are extracted. A download that stopped after the first step leaves it in the wrong one, and with no Excel directory to fall back on every table then loads empty and the server starts up looking healthy.
                var staged = Path.Combine(ResourceDir, "Downloaded", "ExcelDB.db");
                if (File.Exists(staged) && !Directory.Exists(Path.Combine(DumpedDir, "Excel")))
                    throw new InvalidOperationException(
                        $"ExcelDB.db is in {Path.GetDirectoryName(staged)} but not in {DumpedDir}, and there is no Excel " +
                        "directory to load instead, so every table would load empty. Copy it across, or delete " +
                        "Resources/original_version.txt to download the resources again.");

                Console.WriteLine("[ExcelTableService] ExcelDB.db not present; skipping SQLCipher key validation.");
                return;
            }

            var length = new FileInfo(excelDbPath).Length;
            var header = new byte[16];
            using (var stream = File.OpenRead(excelDbPath))
                stream.ReadExactly(header, 0, (int)Math.Min(header.Length, length));

            // A SQLCipher database opens with its random salt, so there is nothing to positively identify it by - but the files that get mistaken for it identify themselves. Without this every one of them reaches the PRAGMA and comes back as error 26, which reads as a key problem and sends people off to re-extract a key that was never wrong.
            string wrongFile = null;
            if (length < 512)
                wrongFile = $"only {length} bytes, too small to be a database";
            else if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                wrongFile = "a zip archive - Excel.zip and HexaMap.zip are the two that get extracted rather than copied";
            else if (header[0] == 0x1F && header[1] == 0x8B && header[2] == 0x08)
                wrongFile = "a gzip stream";
            else if (header.AsSpan().StartsWith("<!DOCTYPE"u8) || header.AsSpan().StartsWith("<!doctype"u8) || header.AsSpan().StartsWith("<html"u8) || header.AsSpan().StartsWith("<?xml"u8))
                wrongFile = "an HTML or XML document, which is what a CDN error page saved to disk looks like";

            if (wrongFile != null)
                throw new InvalidOperationException(
                    $"{excelDbPath} is {wrongFile}, so it is not an ExcelDB and no SQLCipher key will open it. " +
                    "Delete it and Resources/original_version.txt to download the resources again.");

            if (!NeedsSqlCipherKey(excelDbPath))
            {
                Console.WriteLine("[ExcelTableService] ExcelDB.db is an unencrypted SQLite file; no SQLCipher key required.");
                return;
            }

            try
            {
                using var connection = OpenExcelDbConnection(excelDbPath);
                using var command = connection.CreateCommand();
                // wrong key fails here, on the first page decrypt: SQLITE_NOTADB (26).
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
                var tableCount = command.ExecuteScalar();
                Console.WriteLine($"[ExcelTableService] ExcelDB SQLCipher key validated ({tableCount} schema entries).");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 26 /* SQLITE_NOTADB */)
            {
                throw new InvalidOperationException(
                    "ExcelDB SQLCipher key is invalid for the current ExcelDB.db. The key most likely rotated " +
                    "with this client version. Re-extract it from a Queuing_GetCryptoKeys capture (Tools/ExcelDbKeyTool) " +
                    "and set ServerConfiguration.ExcelDbSqlCipherKey (or SHITTIM_EXCELDB_SQLCIPHER_KEY).", ex);
            }
        }

        // internal so the layout-drift audit in the test project can read the shipped ExcelDB rows through the same key handling the server uses, rather than duplicating the SQLCipher pragma logic. See ExcelLayoutDriftTests.
        internal static SqliteConnection OpenExcelDbConnection(string dbPath)
        {
            SqliteProvider.EnsureInitialized();

            var dbConnection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());

            dbConnection.Open();

            if (NeedsSqlCipherKey(dbPath))
            {
                using var keyCommand = dbConnection.CreateCommand();
                keyCommand.CommandText = BuildKeyPragma(GetExcelDbSqlCipherKey());
                keyCommand.ExecuteNonQuery();
            }

            return dbConnection;
        }

        private static bool NeedsSqlCipherKey(string dbPath)
        {
            Span<byte> header = stackalloc byte[16];

            using var stream = File.OpenRead(dbPath);
            if (stream.Read(header) != header.Length)
                return false;

            return !header.SequenceEqual("SQLite format 3\0"u8);
        }

        // The ExcelDB SQLCipher key lives in exactly one place: ServerConfiguration.ExcelDbSqlCipherKey (overridable per-machine via the SHITTIM_EXCELDB_SQLCIPHER_KEY environment variable). The same value is handed to the client in QueuingHandler.GetSqlCipherKeyBytes, so both the server and the client decrypt the CDN's ExcelDB.db with an identical key.
        private static string GetExcelDbSqlCipherKey()
        {
            var key = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY");
            if (string.IsNullOrWhiteSpace(key))
                key = Config.Instance.ServerConfiguration.ExcelDbSqlCipherKey;

            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException(
                    "ExcelDB SQLCipher key is not configured. Set ServerConfiguration.ExcelDbSqlCipherKey " +
                    "in Config.json (or the SHITTIM_EXCELDB_SQLCIPHER_KEY environment variable) to the key " +
                    "for the current client version.");

            return key;
        }

        private static string BuildKeyPragma(string key)
        {
            var trimmed = key.Trim();

            if (trimmed.StartsWith("x'", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("'"))
                return $"PRAGMA key = \"{trimmed.Replace("\"", "\"\"")}\";";

            if (IsHex(trimmed) && trimmed.Length % 2 == 0)
                return $"PRAGMA key = \"x'{trimmed}'\";";

            if (TryBase64Key(trimmed, out var keyBytes))
                return $"PRAGMA key = \"x'{Convert.ToHexString(keyBytes).ToLowerInvariant()}'\";";

            return $"PRAGMA key = '{trimmed.Replace("'", "''")}';";
        }

        private static bool TryBase64Key(string key, out byte[] keyBytes)
        {
            try
            {
                keyBytes = Convert.FromBase64String(key);
                return keyBytes.Length is 16 or 24 or 32;
            }
            catch (FormatException)
            {
                keyBytes = [];
                return false;
            }
        }

        private static bool IsHex(string value)
        {
            return value.All(Uri.IsHexDigit);
        }
    }

    public static class ExcelTableServiceExtensions
    {
        public static void AddExcelTableService(this IServiceCollection services)
        {
            services.AddSingleton<ExcelTableService>();
        }
    }
}
