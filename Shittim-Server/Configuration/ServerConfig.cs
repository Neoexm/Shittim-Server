using Newtonsoft.Json;

namespace BlueArchiveAPI.Configuration
{
    public class ServerConfig
    {
        public string VersionId { get; set; } = "r77_ygnxgslqs2ksqq6m";
        public bool UseCustomExcel { get; set; } = false;
        public bool DownloadExcelTables { get; set; } = true;
    }

    public class Config
    {
        private static Config? _instance;
        public static Config Instance => _instance ??= LoadConfig();

        public ServerConfig ServerConfiguration { get; set; } = new();

        private static Config LoadConfig()
        {
            var configPath = "appsettings.json";
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                return defaultConfig;
            }

            var json = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }
    }
}
