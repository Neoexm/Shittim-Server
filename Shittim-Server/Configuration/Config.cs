using System.Text.Json;
using System.Text.Json.Serialization;
using BlueArchiveAPI.Configuration.ConfigType;
using Shittim.Utils;
using Serilog;

namespace BlueArchiveAPI.Configuration
{
    public class Config : Singleton<Config>
    {
        private const string LocalhostAddress = "127.0.0.1";
        private const string OfficialConnectionGroupsJson =
            "[\r\n" +
            "\t{\r\n" +
            "\t\t\"Name\": \"review\",\r\n" +
            "\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\"DisableWebviewBanner\" : true,\r\n" +
            "\t\t\"NXSID\": \"stage-review\"\r\n" +
            "\t},\r\n" +
            "\t{\r\n" +
            "\t\t\"Name\": \"stage-beta\",\r\n" +
            "\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\"DisableWebviewBanner\" : true,\r\n" +
            "\t\t\"NXSID\": \"stage-beta\"\t\r\n" +
            "\t},\t\r\n" +
            "\t{\r\n" +
            "\t\t\"Name\": \"live\",\r\n" +
            "\t\t\"OverrideConnectionGroups\": [\r\n" +
            "\t\t\t{\r\n" +
            "\t\t\t\t\"Name\": \"kr\",\r\n" +
            "\t\t\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\t\t\"NXSID\": \"live-kr\"\r\n" +
            "\t\t\t},\r\n" +
            "\t\t\t{\r\n" +
            "\t\t\t\t\"Name\": \"tw\",\r\n" +
            "\t\t\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\t\t\"NXSID\": \"live-tw\"\r\n" +
            "\t\t\t},\r\n" +
            "\t\t\t{\r\n" +
            "\t\t\t\t\"Name\": \"asia\",\r\n" +
            "\t\t\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\t\t\"NXSID\": \"live-asia\"\r\n" +
            "\t\t\t},\r\n" +
            "\t\t\t{\r\n" +
            "\t\t\t\t\"Name\": \"na\",\r\n" +
            "\t\t\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\t\t\"NXSID\": \"live-na\"\r\n" +
            "\t\t\t},\r\n" +
            "\t\t\t{\r\n" +
            "\t\t\t\t\"Name\": \"global\",\r\n" +
            "\t\t\t\t\"ApiUrl\": \"\",\r\n" +
            "\t\t\t\t\"GatewayUrl\": \"\",\r\n" +
            "\t\t\t\t\"NXSID\": \"live-global\"\r\n" +
            "\t\t\t}\r\n" +
            "\t\t]\r\n" +
            "\t}]";

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private static readonly Newtonsoft.Json.JsonSerializerSettings serverInfoJsonSettings = new()
        {
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore
        };
        public static string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        public static string ConfigPath => Path.Combine(ConfigDirectory, "Config.json");

        [JsonIgnore]
        public ServerInfoConfig ServerInfoConfig { get; set; }

        public ServerConfig ServerConfiguration { get; set; } = new();
        public IrcConfig IrcConfiguration { get; set; } = new();
        public DataFetcherInfo DataFetcherInfo { get; set; } = new();

        public static void Load()
        {
            if (!Directory.Exists(ConfigDirectory)) Directory.CreateDirectory(ConfigDirectory);
            if (!File.Exists(ConfigPath)) Save();
            string json = File.ReadAllText(ConfigPath);
            Instance = JsonSerializer.Deserialize<Config>(json) ?? new Config();
            var saveLocalhostAddress = Instance.ServerConfiguration?.HostAddress != LocalhostAddress || Instance.IrcConfiguration?.IrcAddress != LocalhostAddress;
            ApplyLocalhostAddress();
            if (saveLocalhostAddress)
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Instance, jsonOptions));

            Instance.ServerInfoConfig = GetServerInfoConfig();

            Log.Debug("Config loaded");
            Log.Information("Data Version Id is {VersionId}", Instance.ServerConfiguration.VersionId);
            Log.Information("Game Server Version is {GameVersion}", Instance.ServerConfiguration.GameVersion.ToString());
            Log.Information("Packet Encryption is {UseEncryption}", Instance.ServerConfiguration.UseEncryption ? "Enabled" : "Disabled");
            Log.Information("Bypass Authentication is {BypassAuthentication}", Instance.ServerConfiguration.BypassAuthentication ? "Enabled" : "Disabled");
            Log.Information("Custom Excel is {UseCustomExcel}", Instance.ServerConfiguration.UseCustomExcel ? "Enabled" : "Disabled");
        }

        public static void Save()
        {
            ApplyLocalhostAddress();
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Instance, jsonOptions));
            Log.Debug($"Config saved");
        }

        private static void ApplyLocalhostAddress()
        {
            Instance.ServerConfiguration ??= new ServerConfig();
            Instance.IrcConfiguration ??= new IrcConfig();
            Instance.ServerConfiguration.HostAddress = LocalhostAddress;
            Instance.IrcConfiguration.IrcAddress = LocalhostAddress;
        }

        public static ServerInfoConfig GetServerInfoConfig()
        {
            var ServerInfoConfigPath = Path.Combine(ConfigDirectory, "ServerInfoConfig.json");
            if(File.Exists(ServerInfoConfigPath))
            {
                var existingConfig = JsonSerializer.Deserialize<ServerInfoConfig>(File.ReadAllText(ServerInfoConfigPath)) ?? CreateServerInfoConfig();
                if (ShouldRegenerateServerInfoConfig(existingConfig))
                    existingConfig = CreateServerInfoConfig();
                existingConfig = ApplyGatewayMode(existingConfig);
                File.WriteAllText(ServerInfoConfigPath, JsonSerializer.Serialize(existingConfig, jsonOptions));
                return existingConfig;
            }

            var serverInfoConfig = CreateServerInfoConfig();

            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ServerInfoConfigPath, JsonSerializer.Serialize(serverInfoConfig, jsonOptions));

            return serverInfoConfig;       
        }

        private static bool ShouldRegenerateServerInfoConfig(ServerInfoConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ConnectionGroupsJson) || !config.ConnectionGroupsJson.Contains("\"stage-beta\""))
                return true;

            if (config.ConnectionGroupsJson != OfficialConnectionGroupsJson)
                return true;

            try
            {
                var connectionGroups = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ConnectionGroup>>(config.ConnectionGroupsJson);
                return connectionGroups == null || connectionGroups.Any(HasConfiguredConnectionUrl);
            }
            catch
            {
                return true;
            }
        }

        private static bool HasConfiguredConnectionUrl(ConnectionGroup group)
        {
            if (!string.IsNullOrWhiteSpace(group.ApiUrl) || !string.IsNullOrWhiteSpace(group.GatewayUrl))
                return true;

            return group.OverrideConnectionGroups?.Any(HasConfiguredConnectionUrl) == true;
        }

        private static ServerInfoConfig CreateServerInfoConfig()
        {
            return new()
            {
                DefaultConnectionGroup = "live",
                Desc = Instance.ServerConfiguration.GameVersion.ToString(),
                ConnectionGroupsJson = OfficialConnectionGroupsJson,
                DefaultConnectionMode = "no",
            };
        }

        private static ServerInfoConfig ApplyGatewayMode(ServerInfoConfig serverInfoConfig)
        {
            serverInfoConfig.Desc = Instance.ServerConfiguration.GameVersion.ToString();

            if (string.IsNullOrWhiteSpace(serverInfoConfig.ConnectionGroupsJson))
                return CreateServerInfoConfig();

            try
            {
                var connectionGroups = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ConnectionGroup>>(serverInfoConfig.ConnectionGroupsJson);
                if (connectionGroups == null)
                    return CreateServerInfoConfig();

                if (connectionGroups.Any(HasConfiguredConnectionUrl))
                    return CreateServerInfoConfig();

                var apiUrl = GetApiUrl();
                var gatewayUrl = GetGatewayUrl();
                foreach (var group in connectionGroups)
                    ApplyConnectionGroupUrls(group, apiUrl, gatewayUrl);

                serverInfoConfig.ConnectionGroupsJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                    connectionGroups,
                    Newtonsoft.Json.Formatting.Indented,
                    serverInfoJsonSettings);
            }
            catch
            {
                return CreateServerInfoConfig();
            }

            return serverInfoConfig;
        }

        private static void ApplyConnectionGroupUrls(ConnectionGroup group, string apiUrl, string gatewayUrl)
        {
            group.ApiUrl = apiUrl;
            group.GatewayUrl = gatewayUrl;

            if (group.OverrideConnectionGroups == null)
                return;

            foreach (var overrideGroup in group.OverrideConnectionGroups)
                ApplyConnectionGroupUrls(overrideGroup, apiUrl, gatewayUrl);
        }

        private static string GetApiUrl()
        {
            return $"http://{Instance.ServerConfiguration.HostAddress}:{Instance.ServerConfiguration.HostPort}/api/";
        }

        private static string GetGatewayUrl()
        {
            return Instance.ServerConfiguration.EnableGateway
                ? $"http://{Instance.ServerConfiguration.HostAddress}:{Instance.ServerConfiguration.GatewayPort}/api/"
                : "";
        }

    }
}
