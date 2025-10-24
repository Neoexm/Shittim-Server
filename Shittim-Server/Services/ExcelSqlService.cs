using Microsoft.Data.Sqlite;
using System.Data;

namespace BlueArchiveAPI.Services
{
    public interface IExcelSqlService
    {
        Task<DataTable> QueryAsync(string sql);
        Task<bool> TableExistsAsync(string tableName);
        Task<List<string>> GetColumnsAsync(string tableName);
        Task<Dictionary<string, string>> GetConstantsAsync();
    }

    public class ExcelSqlService : IExcelSqlService
    {
        private readonly string _dbPath;

        public ExcelSqlService()
        {
            var baseDir = AppContext.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "Resources", "Dumped", "ExcelDB.db");
        }

        public async Task<DataTable> QueryAsync(string sql)
        {
            var dt = new DataTable();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = await command.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task<bool> TableExistsAsync(string tableName)
        {
            try
            {
                var result = await QueryAsync($"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'");
                return result.Rows.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetColumnsAsync(string tableName)
        {
            try
            {
                var result = await QueryAsync($"PRAGMA table_info({tableName})");
                return result.AsEnumerable().Select(row => row["name"].ToString()!).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<Dictionary<string, string>> GetConstantsAsync()
        {
            var constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Try different const table names that might exist
                var tableCandidates = new[] { "ConstCommonExcel", "ConstCommonDBSchema", "ConstCommon" };
                
                foreach (var tableName in tableCandidates)
                {
                    if (!await TableExistsAsync(tableName))
                        continue;
                    
                    var columns = await GetColumnsAsync(tableName);
                    Console.WriteLine($"[ExcelSQL] Found table {tableName} with columns: {string.Join(", ", columns)}");
                    
                    // Try to find key and value columns (case-insensitive)
                    var keyCol = columns.FirstOrDefault(c => c.Contains("key", StringComparison.OrdinalIgnoreCase) || c.Contains("name", StringComparison.OrdinalIgnoreCase));
                    var valueCol = columns.FirstOrDefault(c => c.Contains("value", StringComparison.OrdinalIgnoreCase) && c.Contains("long", StringComparison.OrdinalIgnoreCase));
                    
                    if (keyCol == null || valueCol == null)
                    {
                        Console.WriteLine($"[ExcelSQL] WARNING: Table {tableName} doesn't have recognizable key/value columns");
                        continue;
                    }
                    
                    var data = await QueryAsync($"SELECT {keyCol}, {valueCol} FROM {tableName}");
                    foreach (DataRow row in data.Rows)
                    {
                        var key = row[keyCol]?.ToString();
                        var value = row[valueCol]?.ToString();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            constants[key] = value;
                        }
                    }
                    
                    Console.WriteLine($"[ExcelSQL] Loaded {constants.Count} constants from {tableName}");
                    return constants;
                }
                
                Console.WriteLine("[ExcelSQL] WARNING: No recognizable const table found in ExcelDB.db");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExcelSQL] ERROR loading constants: {ex.Message}");
            }
            
            return constants;
        }
    }

    public static class ExcelSqlServiceExtensions
    {
        public static void AddExcelSqlService(this IServiceCollection services)
        {
            services.AddSingleton<IExcelSqlService, ExcelSqlService>();
        }
    }
}