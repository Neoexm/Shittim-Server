using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlueArchiveAPI.Configuration.ConfigType;
using Shittim.Utils;
using Serilog;

namespace BlueArchiveAPI.Configuration
{
    public class Config : Singleton<Config>
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
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
            Instance = JsonSerializer.Deserialize<Config>(json);
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
            var ip = GetLocalIPv4(NetworkInterfaceType.Wireless80211) == string.Empty ? GetLocalIPv4(NetworkInterfaceType.Ethernet) : GetLocalIPv4(NetworkInterfaceType.Wireless80211);
            Instance.ServerConfiguration.HostAddress = ip;
            Instance.IrcConfiguration.IrcAddress = ip;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Instance, jsonOptions));
            Log.Debug($"Config saved");
        }

        public static ServerInfoConfig GetServerInfoConfig()
        {
            var ServerInfoConfigPath = Path.Combine(ConfigDirectory, "ServerInfoConfig.json");
            if(File.Exists(ServerInfoConfigPath))
            {
                var existingConfig = JsonSerializer.Deserialize<ServerInfoConfig>(File.ReadAllText(ServerInfoConfigPath)) ?? CreateServerInfoConfig();
                existingConfig = ApplyGatewayMode(existingConfig);
                File.WriteAllText(ServerInfoConfigPath, JsonSerializer.Serialize(existingConfig, jsonOptions));
                return existingConfig;
            }

            var serverInfoConfig = CreateServerInfoConfig();

            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ServerInfoConfigPath, JsonSerializer.Serialize(serverInfoConfig, jsonOptions));

            return serverInfoConfig;       
        }

        private static ServerInfoConfig CreateServerInfoConfig()
        {
            var apiUrl = GetApiUrl();
            var gatewayUrl = GetGatewayUrl();

            List<ConnectionGroup> connectionGroups = [
                new()
                {
                    Name = "review",
                    ApiUrl = apiUrl,
                    GatewayUrl = gatewayUrl,
                    DisableWebviewBanner = true,
                    NXSID = "stage-review"
                },
                new()
                {
                    Name = "live",
                    OverrideConnectionGroups = [
                        new()
                        {
                            Name = "kr",
                            ApiUrl = apiUrl,
                            GatewayUrl = gatewayUrl,
                            DisableWebviewBanner = false,
                            NXSID = "live-kr"
                        },
                        new()
                        {
                            Name = "tw",
                            ApiUrl = apiUrl,
                            GatewayUrl = gatewayUrl,
                            DisableWebviewBanner = false,
                            NXSID = "live-tw"
                        },
                        new()
                        {
                            Name = "asia",
                            ApiUrl = apiUrl,
                            GatewayUrl = gatewayUrl,
                            DisableWebviewBanner = false,
                            NXSID = "live-asia"
                        },
                        new()
                        {
                            Name = "na",
                            ApiUrl = apiUrl,
                            GatewayUrl = gatewayUrl,
                            DisableWebviewBanner = false,
                            NXSID = "live-na"
                        },
                        new()
                        {
                            Name = "global",
                            ApiUrl = apiUrl,
                            GatewayUrl = gatewayUrl,
                            DisableWebviewBanner = false,
                            NXSID = "live-global"
                        }
                    ]
                }
            ];

            // var connectionGroupsJson = Newtonsoft.Json.JsonConvert.SerializeObject(connectionGroups, Newtonsoft.Json.Formatting.Indented);
            var connectionGroupsJson = Newtonsoft.Json.JsonConvert.SerializeObject(connectionGroups, Newtonsoft.Json.Formatting.Indented).Replace("  ", "\t");
            return new()
            {
                DefaultConnectionGroup = "live",
                Desc = Instance.ServerConfiguration.GameVersion.ToString(),
                ConnectionGroupsJson = connectionGroupsJson,
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

                var apiUrl = GetApiUrl();
                var gatewayUrl = GetGatewayUrl();

                foreach (var group in connectionGroups)
                    ApplyConnectionGroupUrls(group, apiUrl, gatewayUrl);

                serverInfoConfig.ConnectionGroupsJson = Newtonsoft.Json.JsonConvert
                    .SerializeObject(connectionGroups, Newtonsoft.Json.Formatting.Indented)
                    .Replace("  ", "\t");
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

            foreach (var child in group.OverrideConnectionGroups)
                ApplyConnectionGroupUrls(child, apiUrl, gatewayUrl);
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

        public static string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }
    }
}
