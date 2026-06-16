using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
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
        private const string DefaultSteamGuid = "20790000041274554";
        private const string DefaultArenaUserId = "64437461";
        private const string DefaultArenaGuid = "20790000040815695";

        private static readonly ConcurrentDictionary<string, IasSession> SessionsByWebToken = new();
        private static readonly ConcurrentDictionary<string, IasSession> SessionsByGid = new();
        private static readonly ConcurrentDictionary<string, IasSession> SessionsByPlatformToken = new();

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

            // ROOT-CAUSE FIX for the intermittent "Abnormal client. Returning to the title
            // screen." hang. A genuine new login carries a fresh link_platform_token. The three
            // static session dictionaries below otherwise accrue one entry per login and are NEVER
            // cleared. The inface (V8/Node) SDK persists its web_token in Cookies across game
            // launches and re-sends it; FindSession then re-matches that STALE never-evicted
            // session (verified: a replayed web_token returns the same stale session) and reuses
            // its dead ticket. That desyncs the inface IAS primary-link auth, whose success
            // callback never fires, so the managed login coroutine waits, times out, and shows the
            // abnormal-client popup. (Confirmed by cdb: the stall is the inface IAS auth thread,
            // before NGS; and a server restart -- which clears these dicts -- was the only
            // recovery.) Resetting on each fresh login makes every login start clean, like a
            // restart, so the stale state can never accumulate. Single-account offline server, so
            // dropping prior sessions here is safe.
            if (!string.IsNullOrWhiteSpace(ReadString(payload, "link_platform_token")))
            {
                if (SessionsByWebToken.Count > 0)
                    logger.LogInformation("[IAS] New login link -> clearing {Count} stale IAS session(s)", SessionsByWebToken.Count);
                ResetSessionState();
            }

            var incomingWebToken = ReadRequestWebToken(payload);
            var existingSession = FindSession(payload, incomingWebToken);
            var identity = ResolveIdentity(payload, incomingWebToken, existingSession?.Identity);
            var webToken = !string.IsNullOrWhiteSpace(incomingWebToken)
                ? incomingWebToken
                : existingSession?.WebToken ?? CreateWebToken();

            var session = RememberSession(payload, webToken, identity, existingSession?.Ticket);

            var response = BuildLoginLinkResponse(session);
            logger.LogInformation("[IAS LoginLink Response] web_token={WebToken} local_session_user_id={LocalSessionUserId} local_session_type={LocalSessionType}",
                session.WebToken,
                session.Identity.LocalSessionUserId,
                session.Identity.LocalSessionType);

            return Results.Json(response);
        }

        [HttpPost("v3/issue/ticket/by-web-token")]
        [HttpPost("ias/live/public/v3/issue/ticket/by-web-token")]
        [HttpPost("ias/pre/public/v3/issue/ticket/by-web-token")]
        [HttpPost("ias/alpha/public/v3/issue/ticket/by-web-token")]
        [HttpPost("ias/qa/public/v3/issue/ticket/by-web-token")]
        public async Task<IResult> IssueTicketByWebToken()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IAS IssueTicketByWebToken] {Payload}", payload);

            var webToken = ReadRequestWebToken(payload);
            var existingSession = FindSession(payload, webToken);
            var identity = ResolveIdentity(payload, webToken, existingSession?.Identity);
            if (string.IsNullOrWhiteSpace(webToken))
                webToken = existingSession?.WebToken ?? CreateWebToken();

            var session = RememberSession(payload, webToken, identity, existingSession?.Ticket);

            return Results.Json(new
            {
                errorCode = "0",
                error_code = "0",
                errorText = "",
                error_text = "",
                errorDetail = "",
                error_detail = "",
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = "0",
                message = "",
                ticket = session.Ticket,
                uid = DefaultUid,
                web_token = session.WebToken,
                local_session_user_id = session.Identity.LocalSessionUserId,
                local_session_type = session.Identity.LocalSessionType,
                linked_platform_user_id = session.Identity.LocalSessionUserId,
                links = BuildLinks(session.Identity)
            });
        }

        [HttpGet("v1/issue/game-token/by-ticket")]
        [HttpPost("v1/issue/game-token/by-ticket")]
        [HttpGet("v2/issue/game-token/by-ticket")]
        [HttpPost("v2/issue/game-token/by-ticket")]
        [HttpGet("v3/issue/game-token/by-ticket")]
        [HttpPost("v3/issue/game-token/by-ticket")]
        [HttpGet("ias/live/public/v1/issue/game-token/by-ticket")]
        [HttpPost("ias/live/public/v1/issue/game-token/by-ticket")]
        [HttpGet("ias/live/public/v2/issue/game-token/by-ticket")]
        [HttpPost("ias/live/public/v2/issue/game-token/by-ticket")]
        [HttpGet("ias/live/public/v3/issue/game-token/by-ticket")]
        [HttpPost("ias/live/public/v3/issue/game-token/by-ticket")]
        [HttpGet("ias/pre/public/v1/issue/game-token/by-ticket")]
        [HttpPost("ias/pre/public/v1/issue/game-token/by-ticket")]
        [HttpGet("ias/pre/public/v2/issue/game-token/by-ticket")]
        [HttpPost("ias/pre/public/v2/issue/game-token/by-ticket")]
        [HttpGet("ias/pre/public/v3/issue/game-token/by-ticket")]
        [HttpPost("ias/pre/public/v3/issue/game-token/by-ticket")]
        [HttpGet("ias/alpha/public/v1/issue/game-token/by-ticket")]
        [HttpPost("ias/alpha/public/v1/issue/game-token/by-ticket")]
        [HttpGet("ias/alpha/public/v2/issue/game-token/by-ticket")]
        [HttpPost("ias/alpha/public/v2/issue/game-token/by-ticket")]
        [HttpGet("ias/alpha/public/v3/issue/game-token/by-ticket")]
        [HttpPost("ias/alpha/public/v3/issue/game-token/by-ticket")]
        [HttpGet("ias/qa/public/v1/issue/game-token/by-ticket")]
        [HttpPost("ias/qa/public/v1/issue/game-token/by-ticket")]
        [HttpGet("ias/qa/public/v2/issue/game-token/by-ticket")]
        [HttpPost("ias/qa/public/v2/issue/game-token/by-ticket")]
        [HttpGet("ias/qa/public/v3/issue/game-token/by-ticket")]
        [HttpPost("ias/qa/public/v3/issue/game-token/by-ticket")]
        public async Task<IResult> IssueGameTokenByTicket()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IAS IssueGameTokenByTicket] {Payload}", payload);

            var ticket = Request.Headers["x-ias-ticket"].FirstOrDefault();
            var session = SessionsByWebToken.Values.FirstOrDefault(x => x.Ticket == ticket)
                ?? FindSession(payload, ReadRequestWebToken(payload))
                ?? RememberSession(payload, CreateWebToken(), ResolveIdentity(payload, ""));
            var gameToken = CreateGameToken(session);

            // Shape consumed by the inface JS SDK: issue() does
            //   const n = await reqIAS(...); return n.error_code===ERROR_SUCCESS ? n.game_token : ...
            // ERROR_SUCCESS===0 (a NUMBER), and reqIAS merges this body over its own
            // {error_code:0} base via Object.assign, so a string "0" here would override
            // the SDK's 0 and fail the strict === check. error_code MUST be numeric 0, and
            // game_token MUST be present (that's the field issue() reads).
            return Results.Json(new
            {
                errorCode = 0,
                error_code = 0,
                errorText = "",
                error_text = "",
                errorDetail = "",
                error_detail = "",
                status = "success",
                statusCode = 200,
                status_code = 200,
                code = "0",
                message = "",
                uid = DefaultUid,
                gid = "2079",
                // Account guid bound to the issued game-token. Must match the unified
                // primary guid derived from the login identity (STEAM), NOT the old
                // hard-coded ARENA guid, so the gateway/account view stays self-consistent
                // with the IAS/IMS link responses (see BuildPrimaryLinkDetails).
                guid = BuildPrimaryLinkDetails(session.Identity).Guid,
                game_token = gameToken,
                gameToken,
                access_token = gameToken,
                accessToken = gameToken,
                access_token_type = "game_token",
                accessTokenType = "game_token"
            });
        }

        [HttpGet("v1/issue/link-ticket")]
        [HttpPost("v1/issue/link-ticket")]
        [HttpGet("v2/issue/link-ticket")]
        [HttpPost("v2/issue/link-ticket")]
        [HttpGet("v1/issue/link-ticket/nintendo")]
        [HttpPost("v1/issue/link-ticket/nintendo")]
        [HttpGet("v2/issue/link-ticket/nintendo")]
        [HttpPost("v2/issue/link-ticket/nintendo")]
        [HttpGet("ias/live/public/v1/issue/link-ticket")]
        [HttpPost("ias/live/public/v1/issue/link-ticket")]
        [HttpGet("ias/live/public/v2/issue/link-ticket")]
        [HttpPost("ias/live/public/v2/issue/link-ticket")]
        [HttpGet("ias/live/public/v1/issue/link-ticket/nintendo")]
        [HttpPost("ias/live/public/v1/issue/link-ticket/nintendo")]
        [HttpGet("ias/live/public/v2/issue/link-ticket/nintendo")]
        [HttpPost("ias/live/public/v2/issue/link-ticket/nintendo")]
        public async Task<IResult> IssueLinkTicket()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IMS IssueLinkTicket] {Payload}", payload);

            var ticket = $"shittim-link-ticket:{Guid.NewGuid():N}";
            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = "0",
                message = "",
                link_ticket = ticket,
                linkTicket = ticket,
                ticket
            });
        }

        [HttpGet("v1/link/guest")]
        [HttpPost("v1/link/guest")]
        [HttpGet("ias/live/public/v1/link/guest")]
        [HttpPost("ias/live/public/v1/link/guest")]
        public async Task<IResult> LinkGuest()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IMS LinkGuest] {Payload}", payload);

            var session = FindSession(payload, ReadRequestWebToken(payload))
                ?? RememberSession(payload, CreateWebToken(), ResolveIdentity(payload, ""));

            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = 0,
                message = "",
                linked_platform_user_id = session.Identity.LocalSessionUserId,
                link_platform_user_id = session.Identity.LocalSessionUserId,
                local_session_user_id = session.Identity.LocalSessionUserId,
                local_session_type = session.Identity.LocalSessionType,
                web_token = session.WebToken
            });
        }

        [HttpGet("v1/link/verify/account")]
        [HttpPost("v1/link/verify/account")]
        [HttpGet("ias/live/public/v1/link/verify/account")]
        [HttpPost("ias/live/public/v1/link/verify/account")]
        public async Task<IResult> LinkVerifyAccount()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IMS LinkVerifyAccount] {Payload}", payload);

            var primary = BuildPrimaryLinkDetails();
            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = 0,
                message = "",
                primary_platform_type = primary.PlatformType,
                primary_platform_user_id = primary.PlatformUserId,
                primary_platform_guid = primary.Guid,
                linked_platform_user_id = primary.PlatformUserId,
                is_valid = true,
                isValid = true
            });
        }

        [HttpGet("user-meta/game/user-info")]
        [HttpPost("user-meta/game/user-info")]
        [HttpGet("ias/live/public/user-meta/game/user-info")]
        [HttpPost("ias/live/public/user-meta/game/user-info")]
        public IResult UserMetaGameUserInfo()
        {
            logger.LogInformation("[IMS UserMetaGameUserInfo]");

            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                code = 0,
                message = "",
                is_valid = true,
                isValid = true,
                userMetaArray = Array.Empty<object>(),
                user_meta_array = Array.Empty<object>()
            });
        }

        [HttpGet("user-meta/last-login")]
        [HttpPost("user-meta/last-login")]
        [HttpGet("user-meta/last-login/account")]
        [HttpPost("user-meta/last-login/account")]
        [HttpGet("user-meta/last-login/guid")]
        [HttpPost("user-meta/last-login/guid")]
        [HttpGet("ias/live/public/user-meta/last-login")]
        [HttpPost("ias/live/public/user-meta/last-login")]
        [HttpGet("ias/live/public/user-meta/last-login/account")]
        [HttpPost("ias/live/public/user-meta/last-login/account")]
        [HttpGet("ias/live/public/user-meta/last-login/guid")]
        [HttpPost("ias/live/public/user-meta/last-login/guid")]
        public IResult UserMetaLastLogin()
        {
            logger.LogInformation("[IMS UserMetaLastLogin]");

            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                code = 0,
                message = "",
                is_valid = true,
                isValid = true,
                userMetaArray = Array.Empty<object>(),
                user_meta_array = Array.Empty<object>()
            });
        }

        [HttpGet("user-meta/contents-ownership/{*path}")]
        [HttpPost("user-meta/contents-ownership/{*path}")]
        [HttpGet("ias/live/public/user-meta/contents-ownership/{*path}")]
        [HttpPost("ias/live/public/user-meta/contents-ownership/{*path}")]
        public IResult UserMetaContentsOwnership(string path = "")
        {
            logger.LogInformation("[IMS UserMetaContentsOwnership] {Path}", path);

            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                code = 0,
                message = "",
                contentsInfo = Array.Empty<object>(),
                contents_info = Array.Empty<object>()
            });
        }

        [HttpGet("contents/consent-popup/{*path}")]
        [HttpPost("contents/consent-popup/{*path}")]
        [HttpGet("ias/live/public/contents/consent-popup/{*path}")]
        [HttpPost("ias/live/public/contents/consent-popup/{*path}")]
        public IResult ContentsConsentPopup(string path = "")
        {
            logger.LogInformation("[IMS ContentsConsentPopup] {Path}", path);

            return Results.Json(new
            {
                status = "success",
                statusCode = 200,
                status_code = 200,
                code = 0,
                message = "",
                agreement_status = "AGREED",
                agreementStatus = "AGREED",
                agreed_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                agreedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            });
        }

        [HttpGet("v1/link/account/state-nonce")]
        [HttpPost("v1/link/account/state-nonce")]
        [HttpGet("v1/link/nonce-state")]
        [HttpPost("v1/link/nonce-state")]
        [HttpGet("v1/link/state-nonce")]
        [HttpPost("v1/link/state-nonce")]
        [HttpGet("ias/live/public/v1/link/account/state-nonce")]
        [HttpPost("ias/live/public/v1/link/account/state-nonce")]
        [HttpGet("ias/live/public/v1/link/nonce-state")]
        [HttpPost("ias/live/public/v1/link/nonce-state")]
        [HttpGet("ias/live/public/v1/link/state-nonce")]
        [HttpPost("ias/live/public/v1/link/state-nonce")]
        [HttpGet("ias/pre/public/v1/link/account/state-nonce")]
        [HttpPost("ias/pre/public/v1/link/account/state-nonce")]
        [HttpGet("ias/pre/public/v1/link/nonce-state")]
        [HttpPost("ias/pre/public/v1/link/nonce-state")]
        [HttpGet("ias/pre/public/v1/link/state-nonce")]
        [HttpPost("ias/pre/public/v1/link/state-nonce")]
        [HttpGet("ias/alpha/public/v1/link/account/state-nonce")]
        [HttpPost("ias/alpha/public/v1/link/account/state-nonce")]
        [HttpGet("ias/alpha/public/v1/link/nonce-state")]
        [HttpPost("ias/alpha/public/v1/link/nonce-state")]
        [HttpGet("ias/alpha/public/v1/link/state-nonce")]
        [HttpPost("ias/alpha/public/v1/link/state-nonce")]
        [HttpGet("ias/qa/public/v1/link/account/state-nonce")]
        [HttpPost("ias/qa/public/v1/link/account/state-nonce")]
        [HttpGet("ias/qa/public/v1/link/nonce-state")]
        [HttpPost("ias/qa/public/v1/link/nonce-state")]
        [HttpGet("ias/qa/public/v1/link/state-nonce")]
        [HttpPost("ias/qa/public/v1/link/state-nonce")]
        public IResult LinkAccountStateNonce()
        {
            logger.LogInformation("[IAS LinkAccountStateNonce]");

            return Results.Json(new
            {
                errorCode = "0",
                error_code = "0",
                errorText = "",
                error_text = "",
                errorDetail = "",
                error_detail = "",
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = "0",
                message = "",
                state = Guid.NewGuid().ToString("N"),
                nonce = Guid.NewGuid().ToString("N")
            });
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
        // IMS routes: gamescale native IMS URL builder uses /ims/public/v1 as base,
        // then appends /v1/link/account/platform/primary as the operation path.
        [HttpGet("ims/public/v1/link/account/platform/primary")]
        [HttpPost("ims/public/v1/link/account/platform/primary")]
        [HttpGet("ims/public/v1/v1/link/account/platform/primary")]
        [HttpPost("ims/public/v1/v1/link/account/platform/primary")]
        [HttpGet("ims/pre/public/v1/link/account/platform/primary")]
        [HttpPost("ims/pre/public/v1/link/account/platform/primary")]
        [HttpGet("ims/pre/public/v1/v1/link/account/platform/primary")]
        [HttpPost("ims/pre/public/v1/v1/link/account/platform/primary")]
        public IResult LinkAccountPrimary()
        {
            var prefix = Request.Path.Value?.StartsWith("/ims", StringComparison.OrdinalIgnoreCase) == true
                ? "IMS"
                : "IAS";
            logger.LogInformation("[{Prefix} LinkAccountPrimary]", prefix);
            var response = BuildPrimaryLinkResponse(CreateDefaultSession());
            logger.LogInformation("[{Prefix} LinkAccountPrimary Response] {Response}", prefix, JsonSerializer.Serialize(response));
            return Results.Json(response);
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
                errorCode = "0",
                error_code = "0",
                errorText = "",
                error_text = "",
                errorDetail = "",
                error_detail = "",
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = "0",
                message = "",
                uid = DefaultUid
            });
        }

        [HttpGet("v1/issue/web-ticket/by-game-token")]
        [HttpPost("v1/issue/web-ticket/by-game-token")]
        [HttpGet("v2/issue/web-ticket/by-game-token")]
        [HttpPost("v2/issue/web-ticket/by-game-token")]
        [HttpGet("v3/issue/web-ticket/by-game-token")]
        [HttpPost("v3/issue/web-ticket/by-game-token")]
        [HttpGet("ias/live/public/v1/issue/web-ticket/by-game-token")]
        [HttpPost("ias/live/public/v1/issue/web-ticket/by-game-token")]
        [HttpGet("ias/live/public/v2/issue/web-ticket/by-game-token")]
        [HttpPost("ias/live/public/v2/issue/web-ticket/by-game-token")]
        [HttpGet("ias/live/public/v3/issue/web-ticket/by-game-token")]
        [HttpPost("ias/live/public/v3/issue/web-ticket/by-game-token")]
        [HttpGet("ias/pre/public/v1/issue/web-ticket/by-game-token")]
        [HttpPost("ias/pre/public/v1/issue/web-ticket/by-game-token")]
        [HttpGet("ias/pre/public/v2/issue/web-ticket/by-game-token")]
        [HttpPost("ias/pre/public/v2/issue/web-ticket/by-game-token")]
        [HttpGet("ias/pre/public/v3/issue/web-ticket/by-game-token")]
        [HttpPost("ias/pre/public/v3/issue/web-ticket/by-game-token")]
        [HttpGet("ias/alpha/public/v1/issue/web-ticket/by-game-token")]
        [HttpPost("ias/alpha/public/v1/issue/web-ticket/by-game-token")]
        [HttpGet("ias/alpha/public/v2/issue/web-ticket/by-game-token")]
        [HttpPost("ias/alpha/public/v2/issue/web-ticket/by-game-token")]
        [HttpGet("ias/alpha/public/v3/issue/web-ticket/by-game-token")]
        [HttpPost("ias/alpha/public/v3/issue/web-ticket/by-game-token")]
        [HttpGet("ias/qa/public/v1/issue/web-ticket/by-game-token")]
        [HttpPost("ias/qa/public/v1/issue/web-ticket/by-game-token")]
        [HttpGet("ias/qa/public/v2/issue/web-ticket/by-game-token")]
        [HttpPost("ias/qa/public/v2/issue/web-ticket/by-game-token")]
        [HttpGet("ias/qa/public/v3/issue/web-ticket/by-game-token")]
        [HttpPost("ias/qa/public/v3/issue/web-ticket/by-game-token")]
        public async Task<IResult> IssueWebTicketByGameToken()
        {
            var payload = await ReadBody();
            if (!string.IsNullOrWhiteSpace(payload))
                logger.LogInformation("[IAS IssueWebTicketByGameToken] {Payload}", payload);

            var gameToken = Request.Headers["x-ias-game-token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(gameToken))
                gameToken = ReadString(payload, "game_token");
            if (string.IsNullOrWhiteSpace(gameToken))
                gameToken = ReadString(payload, "gameToken");

            var session = FindSession(payload, ReadRequestWebToken(payload))
                ?? RememberSession(payload, CreateWebToken(), ResolveIdentity(payload, ""));
            var webTicket = $"shittim-web-ticket:{Guid.NewGuid():N}";

            logger.LogInformation("[IAS IssueWebTicketByGameToken Response] web_ticket={WebTicket} game_token_present={HasGameToken}",
                webTicket,
                !string.IsNullOrWhiteSpace(gameToken));

            return Results.Json(new
            {
                errorCode = "0",
                error_code = "0",
                errorText = "",
                error_text = "",
                errorDetail = "",
                error_detail = "",
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = "0",
                message = "",
                uid = DefaultUid,
                web_ticket = webTicket,
                webTicket,
                ticket = webTicket,
                web_token = session.WebToken,
                local_session_user_id = session.Identity.LocalSessionUserId,
                local_session_type = session.Identity.LocalSessionType,
                linked_platform_user_id = session.Identity.LocalSessionUserId
            });
        }

        [HttpGet("ias/{env}/public/{*path}")]
        [HttpPost("ias/{env}/public/{*path}")]
        [HttpGet("ias/{*path}")]
        [HttpPost("ias/{*path}")]
        [HttpGet("ims/{env}/public/{*path}")]
        [HttpPost("ims/{env}/public/{*path}")]
        [HttpGet("ims/{*path}")]
        [HttpPost("ims/{*path}")]
        public async Task<IResult> PublicIas(string? env, string path = "")
        {
            path ??= "";
            var route = string.IsNullOrWhiteSpace(env) ? path : $"public/{path}";
            route = route.Replace('\\', '/').ToLowerInvariant();

            logger.LogInformation("[IAS Public] {Method} {Env} {Path}", Request.Method, env, path);

            // Detailed request dump: we need to know EXACTLY what the bare "GET /ims/public"
            // call is (get_primary_link vs an IMS service/config GET) to serve the right body.
            // Logs full path+query and all request headers (incl. Authorization / x-ias-* /
            // web_token) so the next natural re-fetch reveals the operation unambiguously.
            var headerDump = string.Join(" | ", Request.Headers
                .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                .Select(h => $"{h.Key}={h.Value}"));
            logger.LogInformation("[IAS Public Detail] {Method} {FullPath}{Query} HEADERS[ {Headers} ]",
                Request.Method,
                Request.Path.Value,
                Request.QueryString.Value,
                headerDump);

            if (route.EndsWith("login/link") || route.Contains("/login/link"))
                return await LoginLink();

            if (route.EndsWith("issue/ticket/by-web-token") || route.Contains("/issue/ticket/by-web-token"))
                return await IssueTicketByWebToken();

            // The inface IAS SDK is JavaScript: reqIAS("/issue/game-token/by-ticket",
            // {locale}, {"x-ias-ticket":e}) builds <ias_url>+endpoint, but on the wire the
            // endpoint+/xxxxxx/v1 segment is dropped and the request collapses to a bare
            // POST /ias/live/public carrying only the x-ias-ticket header + body {"locale":...}.
            // So the path-based check below never matches; dispatch by the header instead.
            // (GET /ims/public also carries x-ias-ticket — that's get_primary_link, handled by
            // the isIms branch; verify/game-token uses x-ias-game-token, excluded here.)
            var iasTicketHeader = Request.Headers["x-ias-ticket"].FirstOrDefault();
            var iasGameTokenHeader = Request.Headers["x-ias-game-token"].FirstOrDefault();
            if (route.EndsWith("issue/game-token/by-ticket") || route.Contains("/issue/game-token/by-ticket")
                || (HttpMethods.IsPost(Request.Method)
                    && !string.IsNullOrWhiteSpace(iasTicketHeader)
                    && string.IsNullOrWhiteSpace(iasGameTokenHeader)))
                return await IssueGameTokenByTicket();

            if (route.EndsWith("issue/link-ticket") || route.Contains("/issue/link-ticket"))
                return await IssueLinkTicket();

            if (route.EndsWith("verify/game-token") || route.Contains("/verify/game-token"))
                return VerifyGameToken();

            if (route.EndsWith("issue/web-ticket/by-game-token") || route.Contains("/issue/web-ticket/by-game-token"))
                return await IssueWebTicketByGameToken();

            if (route.EndsWith("link/guest") || route.Contains("/link/guest"))
                return await LinkGuest();

            if (route.EndsWith("link/verify/account") || route.Contains("/link/verify/account"))
                return await LinkVerifyAccount();

            if (route.EndsWith("link/account/state-nonce") || route.Contains("/link/account/state-nonce")
                || route.EndsWith("link/nonce-state") || route.Contains("/link/nonce-state")
                || route.EndsWith("link/state-nonce") || route.Contains("/link/state-nonce"))
                return LinkAccountStateNonce();

            if (route.EndsWith("link/account/platform/primary") || route.Contains("/link/account/platform/primary"))
                return LinkAccountPrimary();

            if (route.Contains("user-meta/game/user-info"))
                return UserMetaGameUserInfo();

            if (route.Contains("user-meta/last-login"))
                return UserMetaLastLogin();

            if (route.Contains("user-meta/contents-ownership"))
                return UserMetaContentsOwnership(path);

            if (route.Contains("contents/consent-popup"))
                return ContentsConsentPopup(path);

            if (HttpMethods.IsPost(Request.Method))
            {
                var payload = await ReadBody();
                if (IsPrimaryLinkFetchPayload(payload))
                {
                    logger.LogInformation("[IAS PrimaryLinkFetch] {Payload}", payload);
                    var session = FindSession(payload, ReadRequestWebToken(payload))
                        ?? RememberSession(payload, CreateWebToken(), ResolveIdentity(payload, ""));

                    logger.LogInformation(
                        "[IAS PrimaryLinkFetch Response] web_token={WebToken} local_session_user_id={LocalSessionUserId} local_session_type={LocalSessionType}",
                        session.WebToken,
                        session.Identity.LocalSessionUserId,
                        session.Identity.LocalSessionType);

                    // CONFIRMED by decompiling ResolvePrimaryPlatformInternal (0x1893FB3C0): this
                    // FetchPrimaryLink result MUST report the link as PRIMARY (isPrimary=true).
                    //   - HasPrimaryLink==true (overridePrimaryPlatform==false) -> "already-primary,
                    //     proceed" success callback (LABEL_8). This is the returning-user path.
                    //   - isPrimary=false -> get_HasPrimaryLink()==false -> the method iterates links
                    //     and, because our link has a non-empty platformUserId AND non-null gameData
                    //     (v30>0), it pops the interactive NXPAccountLinkPrimaryPickerDialog, which has
                    //     no input in the offline flow and hard-stalls "Now Loading".
                    // So markPrimary=true is correct; the remaining post-get_primary_link stall is in
                    // the success-callback continuation, NOT here. (Earlier markPrimary=false test was
                    // doubly wrong: confounded by the scheme bug AND triggers the picker dialog.)
                    var response = BuildPrimaryLinkResponse(session, markPrimary: true);
                    logger.LogInformation("[IAS PrimaryLinkFetch Response Body] {Response}", JsonSerializer.Serialize(response));
                    return Results.Json(response);
                }
            }

            if (HttpMethods.IsPost(Request.Method))
                return await LoginLink();

            // A bare IMS GET that matched no operation above is the native get_primary_link
            // call. gamescale.core.dll's get_primary_link response handler (sub_180707880)
            // accepts HTTP status in {200,400,500} then deserializes the body via
            // sub_180029270; the deserializer REQUIRES the primary-link string fields
            // (primary_platform_type / primary_platform_user_id / primary_platform_guid,
            // plus trace_id / token / token_type / guid — note the dedicated "<field> is
            // empty" strings clustered with the parser). The thin LoginLink shape lacks all
            // of these, so the deserializer throws and the handler emits
            // if.error.auth.ims.get_primary_link.response.parse_exception(10001), which
            // silently stalls login right before Queuing_GetTicket (observed as the game
            // hanging on "Now Loading..." immediately after a 200 on GET /ims/public).
            // Serve the rich primary-link shape (same body proven good for POST
            // PrimaryLinkFetch) instead.
            var isIms = Request.Path.Value?.StartsWith("/ims", StringComparison.OrdinalIgnoreCase) == true;
            if (isIms)
            {
                var imsSession = CreateDefaultSession();
                // Serve the rich primary-link shape for the native get_primary_link parser.
                // (Toggling isPrimary here was tested and does NOT affect the stall.)
                var imsResponse = BuildPrimaryLinkResponse(imsSession);
                logger.LogInformation("[IMS get_primary_link fallback] {Path} -> primary-link shape", Request.Path.Value);
                return Results.Json(imsResponse);
            }

            return Results.Json(BuildLoginLinkResponse(new IasSession(
                CreateWebToken(),
                CreateTicket(),
                new SessionIdentity(DefaultLocalSessionType, DefaultLocalSessionUserId))));
        }

        [HttpGet("{*path}", Order = 999)]
        [HttpPost("{*path}", Order = 999)]
        public async Task<IResult> PublicIasPaddedRoot(string path = "")
        {
            var route = (path ?? "").Replace('\\', '/').ToLowerInvariant();
            if (!IsKnownIasRoute(route))
                return Results.NotFound();

            return await PublicIas(null, path);
        }

        [HttpGet("v1/{*path}")]
        [HttpPost("v1/{*path}")]
        [HttpGet("v2/{*path}")]
        [HttpPost("v2/{*path}")]
        [HttpGet("v3/{*path}")]
        [HttpPost("v3/{*path}")]
        public async Task<IResult> PublicIasVersionRoot(string path = "")
        {
            var requestPath = Request.Path.Value?.Trim('/') ?? "";
            var version = requestPath.Split('/', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            var fullPath = string.IsNullOrWhiteSpace(path) ? version : $"{version}/{path}";

            return await PublicIas(null, fullPath);
        }

        private static bool IsKnownIasRoute(string route)
        {
            return route.EndsWith("login/link")
                || route.Contains("/login/link")
                || route.EndsWith("issue/ticket/by-web-token")
                || route.Contains("/issue/ticket/by-web-token")
                || route.EndsWith("issue/game-token/by-ticket")
                || route.Contains("/issue/game-token/by-ticket")
                || route.EndsWith("issue/link-ticket")
                || route.Contains("/issue/link-ticket")
                || route.EndsWith("verify/game-token")
                || route.Contains("/verify/game-token")
                || route.EndsWith("issue/web-ticket/by-game-token")
                || route.Contains("/issue/web-ticket/by-game-token")
                || route.EndsWith("link/guest")
                || route.Contains("/link/guest")
                || route.EndsWith("link/verify/account")
                || route.Contains("/link/verify/account")
                || route.EndsWith("link/account/state-nonce")
                || route.Contains("/link/account/state-nonce")
                || route.EndsWith("link/nonce-state")
                || route.Contains("/link/nonce-state")
                || route.EndsWith("link/state-nonce")
                || route.Contains("/link/state-nonce")
                || route.EndsWith("link/account/platform/primary")
                || route.Contains("/link/account/platform/primary")
                || route.Contains("user-meta/game/user-info")
                || route.Contains("user-meta/last-login")
                || route.Contains("user-meta/contents-ownership")
                || route.Contains("contents/consent-popup");
        }

        private async Task<string> ReadBody()
        {
            Request.EnableBuffering();
            if (Request.Body.CanSeek)
                Request.Body.Position = 0;

            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            if (Request.Body.CanSeek)
                Request.Body.Position = 0;

            return body;
        }

        private static string ReadString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "";

            try
            {
                using var document = JsonDocument.Parse(json);
                if (TryReadJsonString(document.RootElement, propertyName, out var jsonValue))
                    return jsonValue;
            }
            catch
            {
            }

            return ReadFormString(json, propertyName);
        }

        private static bool TryReadJsonString(JsonElement element, string propertyName, out string value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString() ?? ""
                            : property.Value.ToString();
                        return true;
                    }

                    if (TryReadJsonString(property.Value, propertyName, out value))
                        return true;
                }
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryReadJsonString(item, propertyName, out value))
                        return true;
                }
            }

            value = "";
            return false;
        }

        private static string ReadFormString(string value, string propertyName)
        {
            foreach (var pair in value.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var name = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
                if (!string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                return parts.Length == 2
                    ? Uri.UnescapeDataString(parts[1].Replace('+', ' '))
                    : "";
            }

            return "";
        }

        private string ResolveWebToken(string payload, SessionIdentity identity)
        {
            var webToken = ReadRequestWebToken(payload);

            return string.IsNullOrWhiteSpace(webToken)
                ? CreateWebToken()
                : webToken;
        }

        private string ReadRequestWebToken(string payload)
        {
            var webToken = Request.Headers["x-ias-web-token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(webToken))
                webToken = Request.Query["web_token"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(webToken))
                webToken = ReadString(payload, "web_token");
            if (string.IsNullOrWhiteSpace(webToken))
                webToken = ReadString(payload, "webToken");
            if (string.IsNullOrWhiteSpace(webToken))
                webToken = ReadString(payload, "InWebToken");

            return webToken ?? "";
        }

        private IasSession FindSession(string payload, string webToken)
        {
            if (!string.IsNullOrWhiteSpace(webToken) && SessionsByWebToken.TryGetValue(webToken, out var session))
                return session;

            var gid = ReadString(payload, "gid");
            if (!string.IsNullOrWhiteSpace(gid) && SessionsByGid.TryGetValue(gid, out session))
                return session;

            var platformToken = ReadString(payload, "link_platform_token");
            if (!string.IsNullOrWhiteSpace(platformToken) && SessionsByPlatformToken.TryGetValue(platformToken, out session))
                return session;

            return null;
        }

        private SessionIdentity ResolveIdentity(string payload, string webToken, SessionIdentity fallback = null)
        {
            var localSessionType = ReadString(payload, "store_type");
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = ReadString(payload, "link_platform_type");
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = ReadString(payload, "local_session_type");
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = TryReadWebTokenPart(webToken, 1);
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = fallback?.LocalSessionType;
            if (string.IsNullOrWhiteSpace(localSessionType))
                localSessionType = DefaultLocalSessionType;

            localSessionType = localSessionType.ToUpperInvariant();

            var localSessionUserId = ReadString(payload, "linked_platform_user_id");
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = ReadString(payload, "link_platform_user_id");
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = ReadString(payload, "local_session_user_id");
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = ReadString(payload, "platform_user_id");
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = TryReadWebTokenPart(webToken, 2);
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = fallback?.LocalSessionUserId;
            if (string.IsNullOrWhiteSpace(localSessionUserId))
                localSessionUserId = DefaultLocalSessionUserId;

            return new SessionIdentity(localSessionType, localSessionUserId);
        }

        private static string TryReadWebTokenPart(string webToken, int index)
        {
            if (string.IsNullOrWhiteSpace(webToken) || !webToken.StartsWith("shittim:", StringComparison.Ordinal))
                return "";

            var parts = webToken.Split(':');
            return parts.Length > index ? parts[index] : "";
        }

        private static IasSession RememberSession(string payload, string webToken, SessionIdentity identity, string ticket = null)
        {
            var session = new IasSession(
                webToken,
                string.IsNullOrWhiteSpace(ticket) ? CreateTicket() : ticket,
                identity);

            if (!string.IsNullOrWhiteSpace(session.WebToken))
                SessionsByWebToken[session.WebToken] = session;

            var gid = ReadString(payload, "gid");
            if (!string.IsNullOrWhiteSpace(gid))
                SessionsByGid[gid] = session;

            var platformToken = ReadString(payload, "link_platform_token");
            if (!string.IsNullOrWhiteSpace(platformToken))
                SessionsByPlatformToken[platformToken] = session;

            return session;
        }

        // Clears all accumulated IAS session state so a new login starts from a clean slate
        // (equivalent to a server restart). Called at the start of each genuine LoginLink to
        // prevent the stale-session reuse that hangs the inface IAS auth -> "Abnormal client".
        private static void ResetSessionState()
        {
            SessionsByWebToken.Clear();
            SessionsByGid.Clear();
            SessionsByPlatformToken.Clear();
        }

        private static object BuildLoginLinkResponse(IasSession session)
        {
            return new
            {
                errorCode = "0",
                error_code = "0",
                errorText = "",
                error_text = "",
                errorDetail = "",
                error_detail = "",
                status = "success",
                statusCode = 200,
                status_code = 200,
                responseHandleCode = 0,
                response_handle_code = 0,
                code = "0",
                message = "",
                web_token = session.WebToken,
                ticket = session.Ticket,
                uid = DefaultUid,
                local_session_user_id = session.Identity.LocalSessionUserId,
                local_session_type = session.Identity.LocalSessionType,
                linked_platform_user_id = session.Identity.LocalSessionUserId,
                links = BuildLinks(session.Identity)
            };
        }

        // markPrimary controls whether the emitted link(s) carry isPrimary=true.
        // The native GameAssembly FetchPrimaryLinkResult.get_HasPrimaryLink
        // (0x189460AE0) iterates links and returns true iff some link has the
        // isPrimary bool ([link+0x10]) != 0. NXPToyAuthenticationManager.
        // ResolvePrimaryPlatformInternal (0x1893FB3C0) branches on that result
        // (with overridePrimaryPlatform=false): HasPrimaryLink==true takes the
        // "already linked, proceed" path which issues get_primary_link and then
        // STALLS before Queuing_GetTicket; HasPrimaryLink==false takes the
        // SetPrimaryLink path (POST /v1/link/account/platform/primary) whose
        // success spawns the worker that advances managed to Queuing_GetTicket.
        // So the POST PrimaryLinkFetch response MUST report the login-platform
        // link as NON-primary (markPrimary=false). The get_primary_link and
        // UpdatePrimaryLink responses keep markPrimary=true (the primary is, by
        // then, established / being established).
        private static object BuildPrimaryLinkResponse(IasSession session, bool markPrimary = true)
        {
            var primary = BuildPrimaryLinkDetails(session.Identity);

            return new
            {
                code = 0,
                statusCode = 200,
                status_code = 200,
                message = "",
                name = "",
                // The native gamescale.core.dll C-API GameAuthAccountLinkUpdatePrimaryLink
                // (0x1800cda90) parses this body and REQUIRES the following top-level fields
                // to each be a JSON STRING (nlohmann value_t::string == 3), checked in order:
                //   trace_id, token_type, token, primary_platform_type,
                //   primary_platform_user_id, guid.
                // If any is absent or non-string it logs "<field> is empty", routes
                // if.error.auth.account_link.fetch_primary.invalid_arg, and returns WITHOUT
                // calling sub_1800979A0 (the SetPrimaryLink continuation). That missing
                // continuation is exactly why the managed layer never advances to
                // Queuing_GetTicket. trace_id/token/token_type were previously omitted.
                // NOTE: token is a FLAT top-level STRING here, NOT a nested object.
                trace_id = $"shittim-trace:{Guid.NewGuid():N}",
                token = session.WebToken,
                token_type = "Bearer",
                primary_platform_type = primary.PlatformType,
                primary_platform_user_id = primary.PlatformUserId,
                primary_platform_guid = primary.Guid,
                guid = primary.Guid,
                // After UpdatePrimaryLink (0x1800cda90) succeeds, gamescale.core.dll spawns a
                // worker thread running IFGameAuth::LoginAccountLink's lambda
                // (sub_18005E950 @ 0x18005E950). That lambda parses this same response body and
                // unconditionally extracts "ticket" via nlohmann get<std::string>()
                // (sub_180043C20, called at 0x18005fe63). If "ticket" is absent the inserted
                // null fails the value_t::string (==3) check and throws an UNHANDLED
                // type_error 302 "type must be string, but is null", which fast-fails the
                // process (C0000409, FAST_FAIL_FATAL_APP_EXIT) with no WER dump — the "clean
                // exit right after PrimaryLinkFetch" symptom. The lambda also reads web_token
                // (its success gate), local_session_user_id, and local_session_type as strings,
                // so emit all four here. These are extra keys the UpdatePrimaryLink parser
                // ignores (it only validates its 6 fields), so adding them is safe.
                ticket = session.Ticket,
                web_token = session.WebToken,
                local_session_user_id = session.Identity.LocalSessionUserId,
                local_session_type = session.Identity.LocalSessionType,
                links = BuildPrimaryLinks(session.Identity, markPrimary)
            };
        }

        private static string CreateWebToken()
        {
            return $"st_{Guid.NewGuid():N}{Guid.NewGuid():N}{Guid.NewGuid():N}";
        }

        private static string CreateTicket()
        {
            return $"shittim-ticket:{Guid.NewGuid():N}";
        }

        private static string CreateGameToken(IasSession session)
        {
            return $"ias:gt:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{DefaultUid}@{Guid.NewGuid()}@{session.Identity.LocalSessionType}:ANA";
        }

        private static IasSession CreateDefaultSession()
        {
            return new IasSession(
                CreateWebToken(),
                CreateTicket(),
                new SessionIdentity(DefaultLocalSessionType, DefaultLocalSessionUserId));
        }

        private static object[] BuildLinks(SessionIdentity identity)
        {
            var platformType = string.IsNullOrWhiteSpace(identity.LocalSessionType)
                ? DefaultLocalSessionType
                : identity.LocalSessionType.ToUpperInvariant();
            var platformUserId = string.IsNullOrWhiteSpace(identity.LocalSessionUserId)
                ? DefaultLocalSessionUserId
                : identity.LocalSessionUserId;
            var guid = platformType == DefaultLocalSessionType && platformUserId == DefaultLocalSessionUserId
                ? DefaultSteamGuid
                : BuildAccountGuid(platformUserId);

            // Single self-consistent PRIMARY link for the login platform. Previously this
            // emitted the login platform as a NON-primary link plus a primary ARENA link,
            // which contradicted the STEAM login (and the get_primary_link response, see
            // BuildPrimaryLinkDetails) and drove the client into the primary-mismatch hang.
            return
            [
                BuildLink(true, platformType, platformUserId, guid, "1752526340000", "2026-06-04T17:31:01Z")
            ];
        }

        private static object[] BuildPrimaryLinks(SessionIdentity identity, bool markPrimary = true)
        {
            var platformType = string.IsNullOrWhiteSpace(identity.LocalSessionType)
                ? DefaultLocalSessionType
                : identity.LocalSessionType.ToUpperInvariant();
            var platformUserId = string.IsNullOrWhiteSpace(identity.LocalSessionUserId)
                ? DefaultLocalSessionUserId
                : identity.LocalSessionUserId;
            var guid = platformType == DefaultLocalSessionType && platformUserId == DefaultLocalSessionUserId
                ? DefaultSteamGuid
                : BuildAccountGuid(platformUserId);
            var primary = BuildPrimaryLinkDetails(identity);

            if (!markPrimary)
            {
                // FetchPrimaryLink path: report the login-platform link but with
                // isPrimary=FALSE so the client's get_HasPrimaryLink returns false
                // and ResolvePrimaryPlatformInternal takes the SetPrimaryLink branch
                // (POST /v1/link/account/platform/primary) rather than the
                // "already-primary, proceed" branch that stalls before
                // Queuing_GetTicket. The link is still present (non-empty list) so
                // the SetPrimaryLink branch has the login platform as its candidate.
                // This is NOT the old ARENA-as-primary mismatch (no other platform is
                // marked primary) — it is the legitimate "primary not yet set" state.
                return
                [
                    BuildPrimaryLink(false, platformType, platformUserId, guid, "2026-06-04T17:31:01Z")
                ];
            }

            if (primary.PlatformType == platformType && primary.PlatformUserId == platformUserId && primary.Guid == guid)
            {
                return
                [
                    BuildPrimaryLink(true, primary.PlatformType, primary.PlatformUserId, primary.Guid, "2026-06-04T17:31:01Z")
                ];
            }

            return
            [
                BuildPrimaryLink(false, platformType, platformUserId, guid, "2026-01-10T23:48:25Z"),
                BuildPrimaryLink(true, primary.PlatformType, primary.PlatformUserId, primary.Guid, "2026-06-04T17:31:01Z")
            ];
        }

        private static object BuildLink(bool isPrimary, string platformType, string platformUserId, string guid, string primaryPlatformAt, string lastLogin)
        {
            var gameData = BuildGameData(guid, lastLogin);

            return new
            {
                isPrimary,
                platformType,
                platformUserId,
                guid,
                platform_type = platformType,
                platform_user_id = platformUserId,
                is_primary = isPrimary,
                primaryPlatformAt,
                primary_platform_at = primaryPlatformAt,
                game_data = gameData,
                gameData
            };
        }

        private static object BuildGameData(string guid, string lastLogin)
        {
            return new
            {
                guid,
                name = "",
                level = 0,
                date_last_login = lastLogin,
                dateLastLogin = lastLogin,
                last_login_date = lastLogin,
                lastLoginDate = lastLogin,
                attribute = Array.Empty<object>(),
                attributes = Array.Empty<object>()
            };
        }

        private static object BuildPrimaryLink(bool isPrimary, string platformType, string platformUserId, string guid, string lastLogin)
        {
            var gameData = BuildPrimaryGameData(guid, lastLogin);

            return new
            {
                isPrimary,
                platformType,
                platformUserId,
                is_primary = isPrimary,
                platform_type = platformType,
                platform_user_id = platformUserId,
                guid,
                game_data = gameData,
                gameData
            };
        }

        private static PrimaryLinkDetails BuildPrimaryLinkDetails()
        {
            return BuildPrimaryLinkDetails(new SessionIdentity(DefaultLocalSessionType, DefaultLocalSessionUserId));
        }

        private static PrimaryLinkDetails BuildPrimaryLinkDetails(SessionIdentity identity)
        {
            // For the self-contained offline server, the account's PRIMARY platform is
            // ALWAYS the platform the player actually logged in with (e.g. STEAM). The
            // previous hard-coded ARENA primary created a login-vs-primary mismatch:
            // get_primary_link reported primary=ARENA while the login was STEAM, so the
            // native get_primary_link continuation / managed InfaceSDK GameAuth flow took
            // the "switch to the other platform's primary account" branch, for which we
            // never supplied valid ARENA credentials. That continuation faults and the
            // client hangs on "Now Loading" right after GET /ims/public, issuing no
            // further request (no SetPrimaryLink POST, no Queuing_GetTicket). Mirroring the
            // login platform keeps the entire IAS/IMS view self-consistent.
            var platformType = string.IsNullOrWhiteSpace(identity.LocalSessionType)
                ? DefaultLocalSessionType
                : identity.LocalSessionType.ToUpperInvariant();
            var platformUserId = string.IsNullOrWhiteSpace(identity.LocalSessionUserId)
                ? DefaultLocalSessionUserId
                : identity.LocalSessionUserId;
            var guid = platformType == DefaultLocalSessionType && platformUserId == DefaultLocalSessionUserId
                ? DefaultSteamGuid
                : BuildAccountGuid(platformUserId);
            return new PrimaryLinkDetails(platformType, platformUserId, guid);
        }

        private static object BuildPrimaryPlatformResult(PrimaryLinkDetails primary)
        {
            return new
            {
                primary_platform_type = primary.PlatformType,
                primary_platform_user_id = primary.PlatformUserId,
                primary_platform_guid = primary.Guid,
                guid = primary.Guid,
                primaryPlatformType = primary.PlatformType,
                primaryPlatformUserId = primary.PlatformUserId,
                primaryPlatformGuid = primary.Guid
            };
        }

        private static object BuildPrimaryGameData(string guid, string lastLogin)
        {
            return new
            {
                guid,
                name = "",
                level = 0,
                date_last_login = lastLogin,
                dateLastLogin = lastLogin,
                last_login_date = lastLogin,
                lastLoginDate = lastLogin,
                attribute = Array.Empty<object>(),
                attributes = Array.Empty<object>()
            };
        }

        private static string BuildAccountGuid(string platformUserId)
        {
            var digits = new string((platformUserId ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length >= 13)
                return $"2079{digits[^13..]}";

            if (digits.Length > 0)
                return $"2079{digits.PadLeft(13, '0')}";

            return "20790000000000001";
        }

        private static bool IsPrimaryLinkFetchPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            var gid = ReadString(payload, "gid");
            if (string.IsNullOrWhiteSpace(gid))
                return false;

            return string.IsNullOrWhiteSpace(ReadString(payload, "link_platform_token"))
                && string.IsNullOrWhiteSpace(ReadString(payload, "link_platform_type"))
                && string.IsNullOrWhiteSpace(ReadString(payload, "web_token"))
                && string.IsNullOrWhiteSpace(ReadString(payload, "webToken"))
                && string.IsNullOrWhiteSpace(ReadString(payload, "InWebToken"));
        }

        private sealed record SessionIdentity(string LocalSessionType, string LocalSessionUserId);
        private sealed record IasSession(string WebToken, string Ticket, SessionIdentity Identity);
        private sealed record PrimaryLinkDetails(string PlatformType, string PlatformUserId, string Guid);
    }
}
