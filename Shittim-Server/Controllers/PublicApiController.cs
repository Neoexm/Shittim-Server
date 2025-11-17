using Microsoft.AspNetCore.Mvc;
using Schale.MX.NetworkProtocol;
using Newtonsoft.Json;
using System.Text;
using Shittim_Server.Core;

namespace Shittim_Server.Controllers
{
    [ApiController]
    [Route("/")]
    public class PublicApiController : ControllerBase
    {
        private readonly HandlerManager _handlerManager;
        private readonly ILogger<PublicApiController> _logger;

        public PublicApiController(HandlerManager handlerManager, ILogger<PublicApiController> logger)
        {
            _handlerManager = handlerManager;
            _logger = logger;
        }

        [HttpPost("gameclient/log")]
        public IResult GameClientLog()
        {
            return Results.Json(new { code = 0 });
        }

        [HttpPost("gameclient/auth")]
        public async Task<IActionResult> Auth([FromBody] AccountAuthRequest request)
        {
            try
            {
                using var lease = _handlerManager.GetHandlerLease(Protocol.Account_Auth);
                if (!lease.IsValid)
                {
                    _logger.LogError("Account_Auth handler not found");
                    return StatusCode(500, new { error = "Handler not found" });
                }

                var response = await lease.Handler.Handle(request);
                if (response == null)
                {
                    return StatusCode(500, new { error = "Handler returned null" });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Account_Auth");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("gameclient/create")]
        public async Task<IActionResult> Create([FromBody] AccountCreateRequest request)
        {
            try
            {
                using var lease = _handlerManager.GetHandlerLease(Protocol.Account_Create);
                if (!lease.IsValid)
                {
                    _logger.LogError("Account_Create handler not found");
                    return StatusCode(500, new { error = "Handler not found" });
                }

                var response = await lease.Handler.Handle(request);
                if (response == null)
                {
                    return StatusCode(500, new { error = "Handler returned null" });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Account_Create");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
