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
        private const string DefaultExcelDbSqlCipherKey = "efa143094711b6563ec2132d4d6bbe8533d4e291ed4820bdb515b26bb57bb3f0";
        private readonly ConcurrentDictionary<Type, object> caches = [];
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

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var byteBuffer = new ByteBuffer((byte[])reader[0]);
                                var getRootMethod = fbType.GetMethod($"GetRootAs{baseTypeName}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)])
                                    ?? throw new MissingMethodException($"Could not find GetRootAs{baseTypeName} on type {fbType.FullName}");

                                var flatInstance = getRootMethod.Invoke(null, [byteBuffer]);
                                var unpackMethod = fbType.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public)
                                    ?? throw new MissingMethodException($"Could not find UnPack method on type {fbType.FullName}");

                                var unpackedInstance = (T)unpackMethod.Invoke(flatInstance, null);
                                excelList.Add(unpackedInstance);
                            }
                        }
                    }

                    return excelList;
                }
                else
                {
                    Console.WriteLine($"[ExcelTableService] WARNING: No Excel data found for {baseTypeName}, returning empty list");
                    return new List<T>();
                }
            });

            return unpacked;
        }

        private static SqliteConnection OpenExcelDbConnection(string dbPath)
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

        private static string GetExcelDbSqlCipherKey()
        {
            var key = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY");
            if (string.IsNullOrWhiteSpace(key))
                key = Config.Instance.ServerConfiguration.ExcelDbSqlCipherKey;

            return string.IsNullOrWhiteSpace(key) ? DefaultExcelDbSqlCipherKey : key;
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
