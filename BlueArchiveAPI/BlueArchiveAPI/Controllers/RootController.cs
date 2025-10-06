using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BlueArchiveAPI.Controllers
{
    [ApiController]
    public class RootController : ControllerBase
    {
        private readonly ILogger<RootController> _logger;

        public RootController(ILogger<RootController> logger)
        {
            _logger = logger;
        }

        [HttpPost("/")]
        public ActionResult HandleRootPost()
        {
            _logger.LogInformation("[ROOT] POST / from {Host}", Request.Host);
            
            try
            {
                var contentType = Request.ContentType ?? "";
                if (contentType.Contains("application/json"))
                {
                    using var reader = new StreamReader(Request.Body);
                    var body = reader.ReadToEnd();
                    
                    if (!string.IsNullOrEmpty(body))
                    {
                        try
                        {
                            var json = JsonSerializer.Deserialize<JsonElement>(body);
                            if (json.TryGetProperty("gid", out _) || json.TryGetProperty("platform", out _))
                            {
                                return Content(JsonSerializer.Serialize(new
                                {
                                    errorCode = 0,
                                    result = new { status = "authenticated" },
                                    message = "OK"
                                }), "application/json");
                            }
                        }
                        catch { }
                    }
                }

                return Content(JsonSerializer.Serialize(new
                {
                    errorCode = 0,
                    result = new { status = "ok" },
                    message = "Success"
                }), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ROOT] Error handling POST /");
                return Content(JsonSerializer.Serialize(new
                {
                    errorCode = 0,
                    result = new { },
                    message = "OK"
                }), "application/json");
            }
        }

        [HttpGet("/")]
        public ActionResult HandleRootGet()
        {
            _logger.LogInformation("[ROOT] GET / from {Host}", Request.Host);
            return Content(JsonSerializer.Serialize(new
            {
                status = "ok",
                service = "BlueArchiveAPI",
                version = "1.0"
            }), "application/json");
        }
    }
}