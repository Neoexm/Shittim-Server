using BlueArchiveAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    [Route("suffix2333/com.nexon.bluearchive/server_config")]
    public class ServerConfigController : ControllerBase
    {
        private readonly ILogger<ServerConfigController> _logger;

        public ServerConfigController(ILogger<ServerConfigController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{version}_Live.json")]
        public ActionResult<string> GetServerUrl(string version)
        {
            _logger.LogInformation($"server url from: {Request.Host}");
            return new JObject
            {
                ["DefaultConnectionGroup"] = "live",
                ["ConnectionGroupsJson"] = JsonConvert.SerializeObject(new ConnectionGroup[2]
                {
                    new ConnectionGroup
                    {
                        Name = "stage-review",
                        ApiUrl = $"http://{Request.Host}/api/",
                        GatewayUrl = "https://nxm-eu-bagl.nexon.com:5100/api/gateway/",
                        DisableWebviewBanner = true,
                    },
                    new ConnectionGroup
                    {
                        Name = "live",
                        OverrideConnectionGroups = new ConnectionGroup[5]
                        {
                            new ConnectionGroup()
                            {
                                Name = "tw",
                                ApiUrl = $"http://{Request.Host}/api/",
                                GatewayUrl = "https://nxm-eu-bagl.nexon.com:5100/api/gateway/",
                            },
                            new ConnectionGroup()
                            {
                                Name = "asia",
                                ApiUrl = $"http://{Request.Host}/api/",
                                GatewayUrl = "https://nxm-eu-bagl.nexon.com:5100/api/gateway/",
                            },
                            new ConnectionGroup()
                            {
                                Name = "na",
                                ApiUrl = $"http://{Request.Host}/api/",
                                GatewayUrl = "https://nxm-eu-bagl.nexon.com:5100/api/gateway/",
                            },
                            new ConnectionGroup()
                            {
                                Name = "global",
                                ApiUrl = $"http://{Request.Host}/api/",
                                GatewayUrl = "https://nxm-eu-bagl.nexon.com:5100/api/gateway/",
                            },
                            new ConnectionGroup()
                            {
                                Name = "kr",
                                ApiUrl = $"http://{Request.Host}/api/",
                                GatewayUrl = "https://nxm-eu-bagl.nexon.com:5100/api/gateway/",
                            },
                        }
                    }
                }),
                ["desc"] = "1.50.202328"
            }.ToString();
        }
    }
}
