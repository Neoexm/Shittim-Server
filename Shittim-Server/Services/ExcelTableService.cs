using System.Collections.Concurrent;
using System.Reflection;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.Crypto;

namespace BlueArchiveAPI.Services
{
    public class ExcelTableService
    {
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

                    using (var dbConnection = new SqliteConnection($"Data Source = {excelDBDir}"))
                    {
                        dbConnection.Open();
                        var command = dbConnection.CreateCommand();
                        command.CommandText = $"SELECT Bytes FROM {schemaName}";

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
    }

    public static class ExcelTableServiceExtensions
    {
        public static void AddExcelTableService(this IServiceCollection services)
        {
            services.AddSingleton<ExcelTableService>();
        }
    }
}
