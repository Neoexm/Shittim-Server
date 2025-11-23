namespace BlueArchiveAPI.Configuration.ConfigType
{
    public class ServerConfig
    {
        public Version GameVersion { get; set; } = new("1.82.374906");
        public string VersionId { get; set; } = "b97c05ca58a24270";
        public string HostAddress { get; set; } = "127.0.0.1";
        public string HostPort { get; set; } = "5000";
        public string SQLProvider { get; set; } = "SQLite3";
        public string SQLConnectionString { get; set; } = "Data Source=shittim.sqlite3";
        public bool UseEncryption { get; set; } = false;
        public bool BypassAuthentication { get; set; } = false;
        public bool UseCustomExcel { get; set; } = false;
        public bool AutoCheckVersion { get; set; } = false;
        public bool AutoUpdateVersion { get; set; } = false;
        public string ServerInfoUrl { get; set; } = "https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/355901_Live.json";
        public PacketLogging PacketLogging { get; set; } = new();
    }

    public class IrcConfig
    {
        public string IrcAddress { get; set; } = "127.0.0.1";
        public int IrcPort { get; set; } = 6667;
        public string IrcPassword { get; set; } = "mx123";
    }

    public class DataFetcherInfo
    {
        public string ServerInfoUrl { get; set; } = "https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/355901_Live.json";
    }

    public class PacketLogging
    {
        public bool RequestPacket { get; set; } = true;
        public bool ResponsePacket { get; set; } = false;
        public bool ErrorPacket = false;
    }
}
