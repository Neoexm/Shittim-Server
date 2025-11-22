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

        [HttpGet("toy-push/live/sdk/push/policy")]
        [HttpPost("toy-push/live/sdk/push/policy")]
        public IResult ToyPushPolicy([FromQuery] int? svcID, [FromQuery] string? npToken, [FromBody] object request)
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
