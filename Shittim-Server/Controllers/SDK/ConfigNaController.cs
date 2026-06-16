using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers.SDK
{
    // Handles config.na.nexon.com/v2/configurations/<key> (redirected to us by mitm).
    // The gamescale/Inface SDK gates its initialization on na_time_sync succeeding; if
    // it never gets a successful response (we previously KILLed config.na), the SDK does
    // not finish init and the Bolt sign-in base URL is never set, so /signInWithTicket.nx
    // is never sent and login stalls on "Now Loading".
    //
    // NOTE: response shapes here are a first, best-effort attempt (the exact Nexon config
    // format was not recoverable offline). They are logged and will be refined from the
    // observed client behaviour during testing.
    [ApiController]
    [Route("/v2/configurations")]
    public class ConfigNaController : ControllerBase
    {
        private readonly ILogger<ConfigNaController> _logger;

        public ConfigNaController(ILogger<ConfigNaController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{key}")]
        [HttpPost("{key}")]
        public IResult GetConfiguration(string key)
        {
            _logger.LogInformation("[config.na] {Method} /v2/configurations/{Key}{Query}",
                Request.Method, key, Request.QueryString.Value);

            var now = DateTimeOffset.UtcNow;
            var epochMs = now.ToUnixTimeMilliseconds();
            var epochSec = now.ToUnixTimeSeconds();
            var iso = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            object result = key switch
            {
                "na_time_sync" => new
                {
                    // server time, offered under several plausible field names
                    datetime = iso,
                    timestamp = epochMs,
                    timestampMs = epochMs,
                    timestampSec = epochSec,
                    serverTime = epochMs,
                    server_time = epochMs,
                    currentTime = epochMs,
                    current_time = epochMs,
                    time = epochMs
                },
                "na_grclist_query" => new
                {
                    list = Array.Empty<object>(),
                    grcList = Array.Empty<object>()
                },
                _ => new { }
            };

            var res = new
            {
                errorCode = 0,
                errorText = "Success",
                errorDetail = "",
                result
            };

            return Results.Json(res);
        }
    }
}
