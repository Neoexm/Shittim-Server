using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;
using BlueArchiveAPI.Models;

namespace Shittim_Server.Controllers.Api
{
    [ApiController]
    [Route("/")]
    public class ServerInfoController : ControllerBase
    {
        private readonly ILogger<ServerInfoController> _logger;

        public ServerInfoController(ILogger<ServerInfoController> logger)
        {
            _logger = logger;
        }

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

        [HttpPost("log")]
        public async Task<IResult> GetLog()
        {
            using var reader = new StreamReader(Request.Body);

            var payload = JsonNode.Parse(await reader.ReadToEndAsync());
            if (payload?["Message"] is JsonValue msgNode && msgNode.TryGetValue<string>(out var message))
            {
                _logger.LogError("Game Client Error Detected!");
                _logger.LogError("Time: {Time}", payload["Time"]);
                _logger.LogError("Account: ID {AccountId} | {Account}", payload["AccountId"], payload["Account"]);
                _logger.LogError("Error Type: {Type}", payload["Type"]);
                _logger.LogError("Error Message: {ErrorMessage}", message);
                _logger.LogError("Server: {LastServer}", payload["LastServer"]);
                _logger.LogError("LastLoginName: {LastLoginName}", payload["LastLoginName"]);
                _logger.LogError("Revision: {Revision}", payload["Revision"]);
                _logger.LogError("PublisherAccountId: {PublisherAccountId}", payload["PublisherAccountId"]);
            }

            return Results.Ok();
        }
    }
}
