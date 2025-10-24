using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Plana.FlatData;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace Plana.Crypto
{
    public static class TableService
    {
        /// <summary>
        /// General password gen by file name, encode to base64 for zips password
        /// </summary>
        /// <param name="key"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] CreatePassword(string key, int length = 20)
        {
            byte[] password = GC.AllocateUninitializedArray<byte>((int)Math.Round((decimal)(length / 4 * 3)));

            using var xxhash = XXHash32.Create();
            xxhash.ComputeHash(Encoding.UTF8.GetBytes(key));

            var mt = new MersenneTwister((int)xxhash.HashUInt32);

            int i = 0;
            while (i < password.Length)
            {
                Array.Copy(BitConverter.GetBytes(mt.Next()), 0, password, i, Math.Min(4, password.Length - i));
                i += 4;
            }

            return password;
        }

#if DEBUG
        public static JsonSerializerSettings jsonSettings = new()
        {
            Formatting = Formatting.Indented
        };
        public static void DumpExcels(string bytesDir, string destDir)
        {
            TableEncryptionService.UseEncryption = true;

            foreach (var type in Assembly.GetAssembly(typeof(CharacterExcelTable))!.GetTypes().Where(t => t.IsAssignableTo(typeof(IFlatbufferObject)) && t.Name.EndsWith("ExcelTable")))
            {
                string dataName = type.Name;
                try
                {
                    // Excel.zip
                    var bytesFilePath = Path.Join(bytesDir, $"{type.Name.ToLower()}.bytes");

                    if (File.Exists(bytesFilePath))
                    {
                        var bytes = File.ReadAllBytes(bytesFilePath);
                        TableEncryptionService.XOR(type.Name, bytes);
                        var inst = type.GetMethod($"GetRootAs{type.Name}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)])!.Invoke(null, [new ByteBuffer(bytes)]);

                        var obj = type.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public)!.Invoke(inst, null);
                        File.WriteAllText(Path.Join(destDir, $"{type.Name}.json"), JsonConvert.SerializeObject(obj, jsonSettings));
                        Console.WriteLine($"[ExcelDumper] Dumped {type.Name} successfully");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[ExcelDumper] Error occurred while dumping {dataName} skipping... Error: {e.Message}");
                }
            }
        }
        
        public static List<object> ExcelList(Type type, string dbDir, string schema)
        {
            var excelList = new List<object>();
            using (var dbConnection = new SqliteConnection($"Data Source = {dbDir}"))
            {
                dbConnection.Open();
                var command = dbConnection.CreateCommand();
                command.CommandText = $"SELECT Bytes FROM {schema}";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var excelInst = type.GetMethod($"GetRootAs{type.Name}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)])!
                            .Invoke(null, [new ByteBuffer((byte[])reader[0])]);
                        
                        var obj = type.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public)!.Invoke(excelInst, null);
                        if (obj is null) continue;
                        excelList.Add(obj);
                    }
                }
            }

            return excelList;
        }

        public static void DumpExcelDB(string dbDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            TableEncryptionService.UseEncryption = false;

            using (var dbConnection = new SqliteConnection($"Data Source = {dbDir}"))
            {
                dbConnection.Open();

                string query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

                using (var command = new SqliteCommand(query, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string schemaName = reader.GetString(0);
                        string tableName = schemaName.Replace("DBSchema", "Excel");
                        var type = Assembly.GetAssembly(typeof(CharacterExcelTable))!.GetTypes()
                            .Where(t =>
                                t.IsAssignableTo(typeof(IFlatbufferObject)) &&
                                t.Name.Equals(tableName, StringComparison.CurrentCultureIgnoreCase
                            )).FirstOrDefault();

                        try
                        {
                            if (type is null) continue;
                            var list = ExcelList(type, dbDir, schemaName);
                            File.WriteAllText(Path.Join(destDir, $"{type.Name}.json"), JsonConvert.SerializeObject(list, jsonSettings));

                            Console.WriteLine($"[ExcelDumper] Dumped {type.Name} from {schemaName} successfully");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[ExcelDumper] Error occurred while dumping {schemaName} skipping... Error: {e.Message}");
                        }
                    }
                }
            }
        }
#endif
    }
}
