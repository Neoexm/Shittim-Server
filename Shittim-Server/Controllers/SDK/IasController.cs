using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers.SDK
{
    [ApiController]
    [Route("/")]
    public class IasController : ControllerBase
    {
        private const string DefaultLocalSessionUserId = "76561198260711461";
        private const string DefaultLocalSessionType = "STEAM";
        private const string DefaultUid = "1247143115";

        private readonly ILogger<IasController> logger;

        public IasController(ILogger<IasController> logger)
        {
            this.logger = logger;
        }

        [HttpPost("v2/login/link")]
        [HttpPost("ias/login/link")]
        [HttpPost("ias/live/public/v2/login/link")]
        [HttpPost("ias/pre/public/v2/login/link")]
        [HttpPost("ias/alpha/public/v2/login/link")]
        [HttpPost("ias/qa/public/v2/login/link")]
        public async Task<IResult> LoginLink()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IAS LoginLink] {Payload}", payload);

            var localSessionType = ReadString(payload, "store_type");
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = ReadString(payload, "link_platform_type");
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = DefaultLocalSessionType;

            localSessionType = localSessionType.ToUpperInvariant();

            var localSessionUserId = ReadString(payload, "linked_platform_user_id");
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = ReadString(payload, "link_platform_user_id");
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = DefaultLocalSessionUserId;

            var response = new
            {
                web_token = CreateWebToken(localSessionType, localSessionUserId),
                local_session_user_id = localSessionUserId,
                local_session_type = localSessionType,
                linked_platform_user_id = localSessionUserId,
                links = BuildLinks(localSessionUserId)
            };

            return Results.Json(response);
        }

        [HttpGet("v1/link/account/platform/primary")]
        [HttpPost("v1/link/account/platform/primary")]
        [HttpGet("ias/live/public/v1/link/account/platform/primary")]
        [HttpPost("ias/live/public/v1/link/account/platform/primary")]
        [HttpGet("ias/pre/public/v1/link/account/platform/primary")]
        [HttpPost("ias/pre/public/v1/link/account/platform/primary")]
        [HttpGet("ias/alpha/public/v1/link/account/platform/primary")]
        [HttpPost("ias/alpha/public/v1/link/account/platform/primary")]
        [HttpGet("ias/qa/public/v1/link/account/platform/primary")]
        [HttpPost("ias/qa/public/v1/link/account/platform/primary")]
        public IResult LinkAccountPrimary()
        {
            logger.LogInformation("[IAS LinkAccountPrimary]");

            return Results.Json(new
            {
                links = BuildLinks(DefaultLocalSessionUserId)
            });
        }

        [HttpGet("v1/verify/game-token")]
        [HttpPost("v1/verify/game-token")]
        [HttpGet("ias/live/public/v1/verify/game-token")]
        [HttpPost("ias/live/public/v1/verify/game-token")]
        [HttpGet("ias/pre/public/v1/verify/game-token")]
        [HttpPost("ias/pre/public/v1/verify/game-token")]
        [HttpGet("ias/alpha/public/v1/verify/game-token")]
        [HttpPost("ias/alpha/public/v1/verify/game-token")]
        [HttpGet("ias/qa/public/v1/verify/game-token")]
        [HttpPost("ias/qa/public/v1/verify/game-token")]
        public IResult VerifyGameToken()
        {
            var gameToken = Request.Headers["x-ias-game-token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(gameToken))
                logger.LogInformation("[IAS VerifyGameToken] {Token}", gameToken);

            return Results.Json(new
            {
                uid = DefaultUid
            });
        }

        [HttpGet("ias/{env}/public/{*path}")]
        [HttpPost("ias/{env}/public/{*path}")]
        public async Task<IResult> PublicIas(string env, string path)
        {
            path ??= "";
            var route = path.Replace('\\', '/').ToLowerInvariant();

            logger.LogInformation("[IAS Public] {Method} {Env} {Path}", Request.Method, env, path);

            if (route.EndsWith("login/link") || route.Contains("/login/link"))
                return await LoginLink();

            if (route.EndsWith("verify/game-token") || route.Contains("/verify/game-token"))
                return VerifyGameToken();

            if (route.EndsWith("link/account/platform/primary") || route.Contains("/link/account/platform/primary"))
                return LinkAccountPrimary();

            return Results.Json(new { });
        }

        private async Task<string> ReadBody()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private static string ReadString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "";

            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty(propertyName, out var value))
                    return "";

                return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static string CreateWebToken(string platformType, string platformUserId)
        {
            return $"shittim:{platformType}:{platformUserId}:{Guid.NewGuid():N}";
        }

        private static object[] BuildLinks(string steamId)
        {
            return
            [
                new
                {
                    platform_type = "STEAM",
                    platform_user_id = steamId,
                    guid = "20790000041274554",
                    is_primary = false,
                    game_data = new
                    {
                        guid = "20790000041274554",
                        name = "",
                        level = 0,
                        date_last_login = "2026-01-10T23:48:25Z",
                        attribute = Array.Empty<object>()
                    }
                },
                new
                {
                    platform_type = "ARENA",
                    platform_user_id = "64437461",
                    guid = "20790000040815695",
                    is_primary = true,
                    primary_platform_at = 1752526340000,
                    game_data = new
                    {
                        guid = "20790000040815695",
                        name = "",
                        level = 0,
                        date_last_login = "2026-06-04T17:31:01Z",
                        attribute = Array.Empty<object>()
                    }
                }
            ];
        }
    }
}
