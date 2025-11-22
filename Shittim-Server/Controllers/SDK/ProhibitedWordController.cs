using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers.SDK
{
    [ApiController]
    [Route("/com.nexon.bluearchive/server_config")]
    public class ProhibitedWordController : ControllerBase
    {
        [HttpGet("blacklist.csv")]
        public IResult GetBlacklistCsv()
        {
            Response.ContentType = "text/csv";
            return Results.Text(string.Empty);
        }

        [HttpGet("chattingblacklist.csv")]
        public IResult GetChattingBlacklistCsv()
        {
            Response.ContentType = "text/csv";
            return Results.Text(string.Empty);
        }

        [HttpGet("whitelist.csv")]
        public IResult GetWhitelistCsv()
        {
            Response.ContentType = "text/csv";
            return Results.Text(string.Empty);
        }
    }
}
