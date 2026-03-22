using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers.SDK
{
    [ApiController]
    [Route("/com.nexon.bluearchive/server_config")]
    public class ProhibitedWordController : ControllerBase
    {
        [HttpGet("blacklist.csv")]
        [HttpGet("blacklist")]
        public IResult GetBlacklistCsv()
        {
            Response.ContentType = "text/csv";
            return Results.Text(string.Empty);
        }

        [HttpGet("chattingblacklist.csv")]
        [HttpGet("chattingblacklist")]
        public IResult GetChattingBlacklistCsv()
        {
            Response.ContentType = "text/csv";
            return Results.Text(string.Empty);
        }

        [HttpGet("whitelist.csv")]
        [HttpGet("whitelist")]
        public IResult GetWhitelistCsv()
        {
            Response.ContentType = "text/csv";
            return Results.Text(string.Empty);
        }
    }
}
