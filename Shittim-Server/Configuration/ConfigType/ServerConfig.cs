namespace BlueArchiveAPI.Configuration.ConfigType
{
    public class ServerConfig
    {
        public Version GameVersion { get; set; } = new("1.90.433063");
        public string VersionId => BlueArchiveVersionState.Current.VersionId;
        public string CdnBaseUrl => BlueArchiveVersionState.Current.CdnBaseUrl;
        public string HostAddress { get; set; } = "127.0.0.1";
        public string HostPort { get; set; } = "5000";
        public string GatewayPort { get; set; } = "5100";
        public bool EnableGateway { get; set; } = true;
        public string GatewayRsaPrivateKeyPem { get; set; } = "";
        public string GatewayRsaPrivateKeyPath { get; set; } = "";
        public string GatewayRsaPublicKeyPem { get; set; } = "";
        public string GatewayRsaPublicKeyPath { get; set; } = "";
        public bool AutoPatchClientMetadata { get; set; } = true;
        public string ClientMetadataPath { get; set; } = "";
        public bool AutoPatchClientGameAssemblyIas { get; set; } = false;
        public string ClientGameAssemblyPath { get; set; } = "";
        public bool AutoPatchClientGamescaleIas { get; set; } = true;
        public string ClientGamescaleCorePath { get; set; } = "";
        public bool AutoPatchClientNexonPlatformIas { get; set; } = true;
        public string ClientNexonPlatformModulesPath { get; set; } = "";
        public bool AutoPatchClientInfaceIas { get; set; } = true;
        public string ClientInfacePath { get; set; } = "";
        public bool AutoPatchClientInfaceConfig { get; set; } = true;
        public string ClientInfaceConfigPath { get; set; } = "";
        public string ClientPluginDirectory { get; set; } = "";
        public bool AutoManageGrap64 { get; set; } = true;
        public string ClientGrap64Path { get; set; } = "";
        public bool AutoPatchClientBanners { get; set; } = true;
        public string ClientExcelDbPath { get; set; } = "";
        public string SQLProvider { get; set; } = "SQLite3";
        public string SQLConnectionString { get; set; } = "Data Source=shittim.sqlite3";
        public bool UseEncryption { get; set; } = false;
        public bool BypassAuthentication { get; set; } = false;
        public bool UseCustomExcel { get; set; } = false;
        public bool AutoCheckVersion { get; set; } = true;
        public bool AutoUpdateVersion { get; set; } = true;
        public bool AutoUpdateResources { get; set; } = false;
        public string? OverrideVersionId { get; set; }
        public string? OverrideCdnBaseUrl { get; set; }
        public string ExcelDbSqlCipherKey { get; set; } = "efa143094711b6563ec2132d4d6bbe8533d4e291ed4820bdb515b26bb57bb3f0";
        public string ExcelDbSqlCipherLicense { get; set; } = "OmNpZDowMDFWSjAwMDAwY3pzaVlZQVE6cGxhdGZvcm06MjY6ZXhwaXJlOm5ldmVyOnZlcnNpb246MTpsaWJ2ZXI6NC4xMC4wOmhtYWM6ODQ1Y2JkMzQ0MDc3YjIxNmRlYTgyOWI3OTIyMzRkM2UwYmUyMzNhYw==";
        public string ServerInfoUrl { get; set; } = "https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/433063_Live_77acRXMErRIj8461BJ0KXJP3t.json";
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
        public string ServerInfoUrl { get; set; } = "https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/433063_Live_77acRXMErRIj8461BJ0KXJP3t.json";
    }

    public class PacketLogging
    {
        public bool RequestPacket { get; set; } = true;
        public bool ResponsePacket { get; set; } = false;
        public bool ErrorPacket = false;
    }
}
