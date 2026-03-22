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

        private const int DefaultToyServiceId = 2079;
        private const int DefaultArenaProductId = 59754;
        private const string DefaultPortalGameCode = "1000158";
        private const int DefaultKrpcGameCode = 74280;
        private const long DefaultNaServiceId = 1050768977;
        private const string DefaultProjectId = "d8e6e343";

        public GTableInfaceController(SchaleDataContext context)
        {
            _context = context;
        }

        [HttpGet("gid/{gid}.json")]
        public IResult Gid(string gid)
        {
            var toyServiceId = int.TryParse(gid, out var parsedGid)
                ? parsedGid
                : DefaultToyServiceId;

            var res = new GTableInfaceResponse()
            {
                ToyServiceId = toyServiceId,
                ArenaProductId = DefaultArenaProductId,
                PortalGameCode = DefaultPortalGameCode,
                KrpcGameCode = DefaultKrpcGameCode,
                NaServiceId = DefaultNaServiceId,
                ProjectId = DefaultProjectId,
                Guid = "guid",
                StrEnvType = "LIVE",
                GameReleaseStatus = "released",
                GameNameKo = "블루 아카이브",
                GameNameEn = "Blue Archive",
                Gid = gid,
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
