using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BlueArchiveAPI.Models;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    [Route("/")]
    public class ServerConfigController : ControllerBase
    {
        [HttpGet("com.nexon.bluearchive/server_config/{*filename}")]
        [HttpGet("com.nexon.bluearchivesteam/server_config/{*filename}")]
        public ActionResult GetServerUrl(string filename)
        {
            if (filename.EndsWith(".csv"))
            {
                Response.ContentType = "text/csv";
                return Content(string.Empty);
            }
            
            if (!filename.Contains("_Live") || !filename.EndsWith(".json"))
            {
                return NotFound();
            }
            
            var connectionGroups = new[]
            {
                new ConnectionGroup
                {
                    Name = "stage-review",
                    ApiUrl = "http://localhost:5000/api/",
                    GatewayUrl = "http://localhost:5100/api/",
                    DisableWebviewBanner = true,
                    NXSID = "stage-review"
                },
                new ConnectionGroup
                {
                    Name = "live",
                    OverrideConnectionGroups = new ConnectionGroup[5]
                    {
                        new ConnectionGroup()
                        {
                            Name = "tw",
                            ApiUrl = "http://localhost:5000/api/",
                            GatewayUrl = "http://localhost:5100/api/",
                            DisableWebviewBanner = false,
                            NXSID = "live-tw"
                        },
                        new ConnectionGroup()
                        {
                            Name = "asia",
                            ApiUrl = "http://localhost:5000/api/",
                            GatewayUrl = "http://localhost:5100/api/",
                            DisableWebviewBanner = false,
                            NXSID = "live-asia"
                        },
                        new ConnectionGroup()
                        {
                            Name = "na",
                            ApiUrl = "http://localhost:5000/api/",
                            GatewayUrl = "http://localhost:5100/api/",
                            DisableWebviewBanner = false,
                            NXSID = "live-na"
                        },
                        new ConnectionGroup()
                        {
                            Name = "global",
                            ApiUrl = "http://localhost:5000/api/",
                            GatewayUrl = "http://localhost:5100/api/",
                            DisableWebviewBanner = false,
                            NXSID = "live-global"
                        },
                        new ConnectionGroup()
                        {
                            Name = "kr",
                            ApiUrl = "http://localhost:5000/api/",
                            GatewayUrl = "http://localhost:5100/api/",
                            DisableWebviewBanner = false,
                            NXSID = "live-kr"
                        },
                    }
                }
            };
            
            var connectionGroupsJson = JsonConvert.SerializeObject(connectionGroups, Formatting.None);
            
            var result = new JObject
            {
                ["DefaultConnectionGroup"] = "live",
                ["DefaultConnectionMode"] = "no",
                ["ConnectionGroupsJson"] = connectionGroupsJson,
                ["desc"] = "1.50.202328"
            };
            
            return Content(result.ToString(), "application/json");
        }
    }
}
