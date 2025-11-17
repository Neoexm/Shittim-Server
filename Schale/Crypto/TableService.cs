using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.FlatData;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace Schale.Crypto
{
    public static class TableService
    {
        public static byte[] CreatePassword(string identifier, int passwordLength = 20)
        {
            byte[] passData = GC.AllocateUninitializedArray<byte>((int)Math.Round((decimal)(passwordLength / 4 * 3)));

            using var hasher = XXHash32.Create();
            hasher.ComputeHash(Encoding.UTF8.GetBytes(identifier));

            var randomGen = new MersenneTwister((int)hasher.HashUInt32);

            int position = 0;
            while (position < passData.Length)
            {
                Array.Copy(BitConverter.GetBytes(randomGen.Next()), 0, passData, position, Math.Min(4, passData.Length - position));
                position += 4;
            }

            return passData;
        }

#if DEBUG
        public static JsonSerializerSettings jsonFormatSettings = new()
        {
            Formatting = Formatting.Indented
        };
        
        public static void DumpExcels(string bytesDirectory, string outputDirectory)
        {
            TableEncryptionService.UseEncryption = true;

            foreach (var excelType in Assembly.GetAssembly(typeof(CharacterExcelTable))!.GetTypes().Where(t => t.IsAssignableTo(typeof(IFlatbufferObject)) && t.Name.EndsWith("ExcelTable")))
            {
                string typeName = excelType.Name;
                try
                {
                    var bytesPath = Path.Join(bytesDirectory, $"{excelType.Name.ToLower()}.bytes");

                    if (File.Exists(bytesPath))
                    {
                        var fileBytes = File.ReadAllBytes(bytesPath);
                        TableEncryptionService.XOR(excelType.Name, fileBytes);
                        var tableInstance = excelType.GetMethod($"GetRootAs{excelType.Name}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)])!.Invoke(null, [new ByteBuffer(fileBytes)]);

                        var unpackedData = excelType.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public)!.Invoke(tableInstance, null);
                        File.WriteAllText(Path.Join(outputDirectory, $"{excelType.Name}.json"), JsonConvert.SerializeObject(unpackedData, jsonFormatSettings));
                        Console.WriteLine($"[ExcelDumper] Successfully dumped {excelType.Name}");
                        continue;
                    }
                }
                catch (Exception error)
                {
                    Console.WriteLine($"[ExcelDumper] Failed to dump {typeName}, skipping. Error: {error.Message}");
                }
            }
        }
        
        public static List<object> ExcelList(Type excelType, string databasePath, string schemaName)
        {
            var resultList = new List<object>();
            using (var connection = new SqliteConnection($"Data Source = {databasePath}"))
            {
                connection.Open();
                var query = connection.CreateCommand();
                query.CommandText = $"SELECT Bytes FROM {schemaName}";
                using (var reader = query.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tableInstance = excelType.GetMethod($"GetRootAs{excelType.Name}", BindingFlags.Static | BindingFlags.Public, [typeof(ByteBuffer)])!
                            .Invoke(null, [new ByteBuffer((byte[])reader[0])]);
                        
                        var unpackedData = excelType.GetMethod("UnPack", BindingFlags.Instance | BindingFlags.Public)!.Invoke(tableInstance, null);
                        if (unpackedData is null) continue;
                        resultList.Add(unpackedData);
                    }
                }
            }

            return resultList;
        }

        public static void DumpExcelDB(string databasePath, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            TableEncryptionService.UseEncryption = false;

            using (var connection = new SqliteConnection($"Data Source = {databasePath}"))
            {
                connection.Open();

                string tableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

                using (var queryCommand = new SqliteCommand(tableQuery, connection))
                using (var tableReader = queryCommand.ExecuteReader())
                {
                    while (tableReader.Read())
                    {
                        string schema = tableReader.GetString(0);
                        string excelName = schema.Replace("DBSchema", "Excel");
                        var excelType = Assembly.GetAssembly(typeof(CharacterExcelTable))!.GetTypes()
                            .Where(t =>
                                t.IsAssignableTo(typeof(IFlatbufferObject)) &&
                                t.Name.Equals(excelName, StringComparison.CurrentCultureIgnoreCase
                            )).FirstOrDefault();

                        try
                        {
                            if (excelType is null) continue;
                            var dataList = ExcelList(excelType, databasePath, schema);
                            File.WriteAllText(Path.Join(outputDirectory, $"{excelType.Name}.json"), JsonConvert.SerializeObject(dataList, jsonFormatSettings));

                            Console.WriteLine($"[ExcelDumper] Successfully dumped {excelType.Name} from {schema}");
                        }
                        catch (Exception error)
                        {
                            Console.WriteLine($"[ExcelDumper] Failed to dump {schema}, skipping. Error: {error.Message}");
                        }
                    }
                }
            }
        }
#endif
    }
}


