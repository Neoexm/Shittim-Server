using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BlueArchiveAPI.Models
{
    public class ConnectionGroup
    {
        public string Name;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ApiUrl;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GatewayUrl;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? DisableWebviewBanner;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string NXSID;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ConnectionGroup[] OverrideConnectionGroups;
    }
}
