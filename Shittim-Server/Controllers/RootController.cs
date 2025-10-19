using Microsoft.AspNetCore.Mvc;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    public class RootController : ControllerBase
    {
        [HttpPost("log")]
        public IResult GetLog()
        {
            return Results.Ok();
        }
    }
}