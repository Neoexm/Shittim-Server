using BlueArchiveAPI;
using Schale.MX.NetworkProtocol;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Xml;
using Shittim_Server.Core;
using Protocol = Schale.MX.NetworkProtocol.Protocol;

namespace Shittim_Server.Controllers.Dev
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<ApiController> _logger;
        private readonly HandlerManager _handlerManager;
        private const string API_URL = "https://nxm-tw-bagl.nexon.com:5000/api";
        private const string GATEWAY_URL = "https://localhost:5100/api";

        private static readonly HttpClient _client = new HttpClient();

        static ApiController()
        {
            _client.Timeout = TimeSpan.FromSeconds(30);
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.TryAddWithoutValidation("TE", "identity");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "BestHTTP/2 v2.4.0");
        }

        public ApiController(ILogger<ApiController> logger, HandlerManager handlerManager)
        {
            _logger = logger;
            _handlerManager = handlerManager;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogWarning("[API TEST] Endpoint called successfully!");
            return Ok(new { message = "API server is reachable!", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private static Protocol ResolveProtocolOrRaise(string path, string hash)
        {
            var proto1 = Utils.ParseProtocolPath(path);
            var proto2 = Utils.ParseProtocolHash(hash);
            if (proto1 != proto2)
            {
                throw new Exception($"Protocol mismatch: {proto1} != {proto2}");
            }
            return proto1;
        }

        [HttpPost("gateway/{path1}/{path2}")]
        public async Task<ActionResult> GatewayApi(string path1, string path2,
            [FromForm] string protocol, [FromForm] bool encode, [FromForm] string packet)
        {
            _logger.LogInformation($"gateway: {protocol}@{path1}/{path2}");

            var proto = ResolveProtocolOrRaise($"{path1}/{path2}", protocol);

            var requestType = _handlerManager.GetRequestType(proto);
            if (requestType == null)
            {
                return NotFound();
            }

            var decryptedJson = Utils.DecryptRequestPacket(packet);
            var request = (RequestPacket)JsonConvert.DeserializeObject(decryptedJson, requestType)!;

            using var lease = _handlerManager.GetHandlerLease(proto);
            if (!lease.IsValid)
            {
                return NotFound();
            }

            var response = await lease.Handler.Handle(request);
            if (response == null)
            {
                return StatusCode(500, "Handler returned null");
            }

            var encryptedBytes = Utils.EncryptResponsePacket(response, proto);
            return File(encryptedBytes, "application/json; charset=utf-8");
        }
        
        [HttpPost("api/{path1}/{path2}")]
        public async Task<ActionResult> GameApi(string path1, string path2,
            [FromForm] string protocol, [FromForm] bool encode, [FromForm] string packet)
        {
            _logger.LogInformation($"api: {protocol}@{path1}/{path2}");
            
            var proto = ResolveProtocolOrRaise($"{path1}/{path2}", protocol);

            var requestType = _handlerManager.GetRequestType(proto);
            if (requestType == null)
            {
                _logger.LogWarning($"api: {protocol}@{path1}/{path2} - no request type!");
                return NotFound();
            }

            var decryptedJson = Utils.DecryptRequestPacket(packet);
            var request = (RequestPacket)JsonConvert.DeserializeObject(decryptedJson, requestType)!;

            using var lease = _handlerManager.GetHandlerLease(proto);

            if (!lease.IsValid)
            {
                _logger.LogWarning($"api: {protocol}@{path1}/{path2} not implemented!");
                return NotFound();
            }

            var response = await lease.Handler.Handle(request);
            if (response == null)
            {
                return StatusCode(500, "Handler returned null");
            }

            var encryptedBytes = Utils.EncryptResponsePacket(response, proto);
            return File(encryptedBytes, "application/json; charset=utf-8");

        }
    }
}
