using Microsoft.AspNetCore.Mvc;
using Schale.Data;
using Shittim_Server.Models.SDK;

namespace Shittim_Server.Controllers.SDK
{
    [ApiController]
    [Route("/")]
    public class GTableInfaceController : ControllerBase
    {
        private readonly SchaleDataContext _context;

        public GTableInfaceController(SchaleDataContext context)
        {
            _context = context;
        }

        [HttpGet("gid/2079.json")]
        public IResult Gid()
        {
            var res = new GTableInfaceResponse()
            {
                ToyServiceId = 2079,
                ArenaProductId = 59754,
                PortalGameCode = "1000158",
                KrpcGameCode = 74280,
                NaServiceId = 1050768977,
                ProjectId = "d8e6e343",
                Guid = "guid",
                StrEnvType = "LIVE",
                GameReleaseStatus = "released",
                GameNameKo = "블루 아카이브",
                GameNameEn = "Blue Archive",
                Gid = "2079",
                LastModified = new()
                {
                    ModifyDate = DateTime.Parse("2024-10-10"),
                    AdminNo = 441
                },
                Created = new()
                {
                    CreateDate = DateTime.Parse("2022-10-28"),
                    AdminNo = 2
                }
            };
            return Results.Json(res);
        }
    }
}
