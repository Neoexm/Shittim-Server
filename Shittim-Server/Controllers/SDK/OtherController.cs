using Microsoft.AspNetCore.Mvc;
using Schale.Data;
using Shittim_Server.Models.SDK;

namespace Shittim_Server.Controllers.SDK
{
    [ApiController]
    [Route("/")]
    public class OtherController : ControllerBase
    {
        private readonly SchaleDataContext _context;

        public OtherController(SchaleDataContext context)
        {
            _context = context;
        }

        [HttpPost("stream-processor-proxy/ap-northeast-1/client.all.secure")]
        public IResult UserCheck()
        {
            return Results.Ok();
        }

        [HttpGet("toy/v2/country")]
        public IResult CountryV2()
        {
            return Results.Json(new CountryV2Response()
            {
                IP = "192.168.1.100",
                CountryCode = "PH"
            });
        }

        [HttpPost("sdk-api/user-meta/last-login")]
        public IResult LastLogin()
        {
            return Results.Json(new {});
        }

        // ToySteam reads is_free and price_overview out of this to work out the store currency before it asks Steam Inventory for item prices, and with nothing answering it the lobby comes up with "Failed to request prices".
        [HttpGet("api/appdetails")]
        public IResult AppDetails([FromQuery] string appids)
        {
            var details = new
            {
                success = true,
                data = new
                {
                    type = "game",
                    name = "Blue Archive",
                    steam_appid = int.Parse(appids),
                    required_age = 0,
                    is_free = true,
                    developers = new[] { "NEXON Games" },
                    publishers = new[] { "NEXON Games" },
                    platforms = new { windows = true, mac = false, linux = false },
                    release_date = new { coming_soon = false, date = "6 Nov, 2024" }
                }
            };

            return Results.Json(new Dictionary<string, object> { [appids] = details });
        }

        [HttpGet("toy-push/live/sdk/push/policy")]
        [HttpPost("toy-push/live/sdk/push/policy")]
        // No [FromBody] parameter: the client queries this with a bodyless GET, and a required body binding made every one of those 400.
        public IResult ToyPushPolicy([FromQuery] int? svcID, [FromQuery] string? npToken)
        {
            if (svcID.HasValue)
            {
                var res = new
                {
                    push = new { },
                    kind = new { },
                    svcID = svcID,
                    npToken = npToken
                };
                return Results.Json(res);
            }

            return Results.Ok();
        }
    }
}
