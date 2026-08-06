namespace BlueArchiveAPI.Configuration.ConfigType
{
    public class ServerConfig
    {
        public Version GameVersion { get; set; } = new("1.90.439170");
        // Data-build number reported as Account_Auth.CurrentVersion. The official server answers its latest build regardless of the client's (captured: 446690 for a 1.90.443855 client).
        public long AuthCurrentVersion { get; set; } = 446690;
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
        // The Blue Archive install every client path below is derived from. Blank means find it: Steam's own libraries, then the conventional locations. Set it when there are two installs, or when the game is somewhere Steam does not know about.
        public string ClientInstallDirectory { get; set; } = "";
        public bool AutoPatchClientMetadata { get; set; } = true;
        public string ClientMetadataPath { get; set; } = "";
        public bool AutoPatchClientGameAssemblyIas { get; set; } = false;
        // Lets the client hand out an external ticket with Steam offline or not running. Off by default because it rewrites GameAssembly.dll, which Steam restores on a file verify.
        public bool AutoPatchClientSteamOffline { get; set; } = false;
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
        // Sends the client's steam store lookup to this server instead, so the shop currency check still answers with no route out.
        public bool AutoPatchClientStoreUrl { get; set; } = true;
        // Replaces the title-screen region label, which the client builds from its own metadata rather than from anything on the wire. Blank puts the stock region name back.
        public string RegionDisplayText { get; set; } = "";
        public string SQLProvider { get; set; } = "SQLite3";
        public string SQLConnectionString { get; set; } = "Data Source=shittim.sqlite3";
        public bool UseEncryption { get; set; } = false;
        public bool BypassAuthentication { get; set; } = false;
        // When nonzero, logins are answered with this account regardless of which publisher identity connects. Set from the Control Center accounts page, 0 disables.
        public long SelectedAccountId { get; set; } = 0;
        public bool UseCustomExcel { get; set; } = false;
        // Fills every cafe with Koyuki and swaps the lobby banner list for the single webview banner. Off means stock random visitors and no banners.
        public bool KoyukiIncident { get; set; } = false;
        public bool AutoCheckVersion { get; set; } = true;
        public bool AutoUpdateVersion { get; set; } = true;
        public bool AutoUpdateResources { get; set; } = true;
        public string? OverrideVersionId { get; set; }
        public string? OverrideCdnBaseUrl { get; set; }
        // Shared secret for the /api/admin surface, sent by the client as an X-Admin-Key header and overridable per-machine with SHITTIM_ADMIN_API_KEY.
        // Empty by default, which restricts admin endpoints to loopback (see AdminAuthAttribute) - enough for the Control Center, which connects to 127.0.0.1. Set this only if you need to administer the server remotely.
        public string AdminApiKey { get; set; } = "";
        public string ExcelDbSqlCipherKey { get; set; } = "efa143094711b6563ec2132d4d6bbe8533d4e291ed4820bdb515b26bb57bb3f0";
        public string ExcelDbSqlCipherLicense { get; set; } = "OmNpZDowMDFWSjAwMDAwY3pzaVlZQVE6cGxhdGZvcm06MjY6ZXhwaXJlOm5ldmVyOnZlcnNpb246MTpsaWJ2ZXI6NC4xMC4wOmhtYWM6ODQ1Y2JkMzQ0MDc3YjIxNmRlYTgyOWI3OTIyMzRkM2UwYmUyMzNhYw==";
        public string ServerInfoUrl { get; set; } = "https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/439170_Live_77acRXMErRIj8461BJ0KXJP3t.json";
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
        public string ServerInfoUrl { get; set; } = "https://d2vaidpni345rp.cloudfront.net/com.nexon.bluearchivesteam/server_config/439170_Live_77acRXMErRIj8461BJ0KXJP3t.json";
    }

    public class PacketLogging
    {
        public bool RequestPacket { get; set; } = true;
        public bool ResponsePacket { get; set; } = false;
        public bool ErrorPacket = false;
        // On by default: the client reports protocol faults as opaque popups, and without the response bytes there is nothing to compare. Costs one buffered append per request.
        public bool WireDump { get; set; } = true;
    }
}
