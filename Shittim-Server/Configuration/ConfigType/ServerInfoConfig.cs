namespace BlueArchiveAPI.Configuration
{
    public class ServerInfoConfig
    {
        public string DefaultConnectionGroup { get; set; }
        public string DefaultConnectionMode { get; set; }
        public string ConnectionGroupsJson { get; set; }
        public string Desc { get; set; }
    }

    public class ConnectionGroup
    {
        public string Name { get; set; }
        public string ApiUrl { get; set; }
        public string GatewayUrl { get; set; }
        public bool DisableWebviewBanner { get; set; }
        public string NXSID { get; set; }
        public List<ConnectionGroup> OverrideConnectionGroups { get; set; }
    }
}
