using Microsoft.AspNetCore.Mvc;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    [Route("/")]
    public class PublicApiController : ControllerBase
    {
        [HttpPost("gameclient/log")]
        public IResult GameClientLog()
        {
            return Results.Json(new { code = 0 });
        }
    }
}
