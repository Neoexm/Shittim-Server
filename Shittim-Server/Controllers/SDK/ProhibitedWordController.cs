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
            return WordList("blacklist");
        }

        [HttpGet("chattingblacklist.csv")]
        [HttpGet("chattingblacklist")]
        public IResult GetChattingBlacklistCsv()
        {
            return WordList("chattingblacklist");
        }

        [HttpGet("whitelist.csv")]
        [HttpGet("whitelist")]
        public IResult GetWhitelistCsv()
        {
            return WordList("whitelist");
        }

        // pkzip-encrypted with base64(TableService.CreatePassword(last url segment)), so the route name is what the file has to be built against. an empty body is not an option: Unity's downloadHandler.data is null rather than a zero-length array, and ProhibitedWordDownLoadService feeds it straight into new MemoryStream()
        private static IResult WordList(string name)
        {
            return Results.File(Path.Combine(AppContext.BaseDirectory, "Data", "ProhibitedWord", name + ".zip"), "application/zip");
        }
    }
}
