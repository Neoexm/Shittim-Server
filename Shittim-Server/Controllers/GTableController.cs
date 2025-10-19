using Microsoft.AspNetCore.Mvc;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    [Route("/")]
    public class GTableController : ControllerBase
    {
        [HttpGet("gid/2079.json")]
        public IResult Gid()
        {
            var res = new
            {
                toy_service_id = 2079,
                arena_product_id = 59754,
                game_client_id = (string?)null,
                portal_game_code = "1000158",
                krpc_game_code = 74280,
                jppc_game_code = (int?)null,
                na_service_id = 1050768977,
                na_region_host = (string?)null,
                krpc_service_code = (string?)null,
                eve_gameinfo_id = (string?)null,
                twitch_game_id = (string?)null,
                chzzk_game_id = (string?)null,
                project_id = "d8e6e343",
                guss_service_code = (string?)null,
                guid = "guid",
                world_id = (string?)null,
                gcid = (string?)null,
                krpc_member_access_code = (string?)null,
                jppc_gm = (string?)null,
                google_oauth_billing_client_redirect_uri = (string?)null,
                krpc_product_type = (string?)null,
                jppc_product_type = (string?)null,
                coin_type = (string?)null,
                alltem_code = "bluearchive",
                nisms_code = (string?)null,
                nxshop_code = (string?)null,
                google_oauth_billing_client_id = (string?)null,
                google_oauth_billing_client_secret = (string?)null,
                arena_service_code = (string?)null,
                str_env_type = "LIVE",
                game_release_status = "released",
                nemo_service_id = (int?)null,
                game_name_ko = "블루 아카이브",
                game_name_en = "Blue Archive",
                gid = "2079",
                last_modified = new
                {
                    modify_date = DateTime.Parse("2024-10-10"),
                    admin_no = 441
                },
                krpc_alltem_code = "bluearchive",
                created = new
                {
                    create_date = DateTime.Parse("2022-10-28"),
                    admin_no = 2
                }
            };
            return Results.Json(res);
        }
    }
}
