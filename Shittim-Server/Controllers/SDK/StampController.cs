using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers.SDK
{
    // Handles public.api.nexon.com/stamp/* (redirected to us by mitm) — Nexon's Stamp
    // (IAP/cash-shop) service. The client calls into it on every lobby entry, and a failure
    // anywhere in the chain surfaces as a modal popup over the loaded lobby.
    //
    // The single hard requirement, recovered from gamescale.core.dll's shared stamp HTTP
    // response handler (sub_1800F0490): the handler only accepts HTTP 200. It switches on the
    // status code and treats 400/500 as "stamp api failed" (0x2FCDB394) and *every other*
    // status — 404 included — as "stamp unknown error." (0x2FCDB396); both are then remapped
    // by the response parser to 0x2FBE7159, which the UI renders as 801010009. Only 200 sets
    // the response's success flag. The body must additionally be parseable JSON, or the
    // handler reports "stamp response not json" (0x2FCDB395). Field lookups themselves are
    // lenient — the parser searches the object for each key and silently skips absent ones —
    // so an unknown endpoint can safely be answered with an empty object as long as the
    // status is 200.
    //
    // Because of that, this is deliberately a catch-all: any unserved /stamp/** path would
    // otherwise 404 and reproduce the popup. Known endpoints get their real response shape;
    // anything else gets {}. New paths are logged at warning level so they can be given a
    // proper body if the client turns out to need one.
    //
    // Path prefixes vary by environment: /stamp/live, /stamp/live01, /stamp/pre on
    // public.api, and /stamp/{alpha,qa,qa02}/public on sandbox.api — hence matching on the
    // suffix rather than a fixed {env} segment.
    [ApiController]
    public class StampController : ControllerBase
    {
        // Opaque handles the client echoes back on later stamp calls (issue/purchase), which
        // an offline server never completes, so stable placeholders suffice.
        private const string StampId = "shittim-stamp-00000000-0000-0000-0000-000000000001";
        private const string StampToken = "shittim-stamp-token";

        private readonly ILogger<StampController> _logger;

        public StampController(ILogger<StampController> logger)
        {
            _logger = logger;
        }

        [AcceptVerbs("GET", "POST", "PUT", "DELETE", "OPTIONS", Route = "/stamp/{**path}")]
        public IResult Handle(string? path)
        {
            var suffix = "/" + (path ?? string.Empty);
            object body;

            if (Ends(suffix, "/v1/enter"))
            {
                body = new
                {
                    stamp_id = StampId,
                    stamp_token = StampToken,
                    direct_payment_url = ""
                };
            }
            else if (Ends(suffix, "/products"))
            {
                // Cash-shop listing. Nothing is for sale offline; an empty list is a valid
                // "no products", and the client renders the shop from its own excel anyway.
                body = new { product_infos = Array.Empty<object>() };
            }
            else if (Ends(suffix, "/v2/stamp/status") || suffix.Contains("/v2/restore/", StringComparison.OrdinalIgnoreCase))
            {
                // Pending-transaction poll and purchase-restore. Report none so the client
                // never waits on a delivery. Both spellings of the list appear in the SDK.
                body = new
                {
                    transactions = Array.Empty<object>(),
                    billingTransactions = Array.Empty<object>()
                };
            }
            else
            {
                body = new { };
                _logger.LogWarning("[stamp] unmodelled endpoint {Method} /stamp/{Path} — answering 200 {{}}",
                    Request.Method, path);
            }

            _logger.LogInformation("[stamp] {Method} /stamp/{Path}{Query}",
                Request.Method, path, Request.QueryString.Value);

            return Results.Json(body);
        }

        private static bool Ends(string suffix, string tail)
            => suffix.EndsWith(tail, StringComparison.OrdinalIgnoreCase);
    }
}
