using Microsoft.AspNetCore.Mvc;

namespace Shittim_Server.Controllers.SDK
{
    // Nexon's stamp (IAP/cash-shop) service, redirected here by mitm. The client calls it on
    // every lobby entry and any failure surfaces as a modal over the loaded lobby.
    // gamescale.core.dll's response handler (sub_1800F0490) accepts only HTTP 200: 400/500 map
    // to 0x2FCDB394, anything else including 404 to 0x2FCDB396, both remapped to 801010009 on
    // screen. The body must parse as JSON (else 0x2FCDB395) but field lookups are lenient, so
    // {} answers anything unmodelled - hence the catch-all instead of per-path routes. Prefixes
    // vary by environment (/stamp/live, /stamp/live01, /stamp/pre, /stamp/{alpha,qa,qa02}/public),
    // so the match is on the suffix.
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
                _logger.LogWarning("[stamp] unmodelled endpoint {Method} /stamp/{Path} - answering 200 {{}}",
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
