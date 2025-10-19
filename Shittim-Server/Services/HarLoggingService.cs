using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace BlueArchiveAPI.Services
{
    public class HarLoggingService
    {
        private readonly ConcurrentBag<object> _entries = new();
        private readonly string _logFilePath;
        private readonly ILogger<HarLoggingService> _logger;

        public HarLoggingService(ILogger<HarLoggingService> logger)
        {
            _logger = logger;
            _logFilePath = $"server_log_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.har";
            CreateInitialHarFile();
        }

        private void CreateInitialHarFile()
        {
            try
            {
                var harData = new
                {
                    log = new
                    {
                        version = "1.2",
                        creator = new { name = "BlueArchiveAPI", version = "1.0" },
                        entries = Array.Empty<object>()
                    }
                };

                var json = JsonSerializer.Serialize(harData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_logFilePath, json);
                Console.WriteLine($"✓ HAR file created: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to create HAR file: {ex.Message}");
            }
        }

        public async Task LogRequestResponse(HttpContext context, long durationMs, byte[] requestBody, byte[] responseBody)
        {
            try
            {
                var entry = new
                {
                    startedDateTime = DateTime.UtcNow.ToString("o"),
                    time = durationMs,
                    request = new
                    {
                        method = context.Request.Method,
                        url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}",
                        httpVersion = context.Request.Protocol,
                        headers = context.Request.Headers.Select(h => new { name = h.Key, value = string.Join(", ", h.Value!) }).ToArray(),
                        queryString = context.Request.Query.Select(q => new { name = q.Key, value = string.Join(", ", q.Value!) }).ToArray(),
                        cookies = context.Request.Cookies.Select(c => new { name = c.Key, value = c.Value }).ToArray(),
                        headersSize = -1,
                        bodySize = requestBody.Length,
                        postData = requestBody.Length > 0 ? new
                        {
                            mimeType = context.Request.ContentType ?? "",
                            text = Encoding.UTF8.GetString(requestBody)
                        } : null
                    },
                    response = new
                    {
                        status = context.Response.StatusCode,
                        statusText = GetStatusText(context.Response.StatusCode),
                        httpVersion = "HTTP/1.1",
                        headers = context.Response.Headers.Select(h => new { name = h.Key, value = string.Join(", ", h.Value!) }).ToArray(),
                        content = new
                        {
                            size = responseBody.Length,
                            mimeType = context.Response.ContentType ?? "",
                            text = Encoding.UTF8.GetString(responseBody)
                        },
                        redirectURL = context.Response.Headers.Location.ToString() ?? "",
                        headersSize = -1,
                        bodySize = responseBody.Length
                    },
                    cache = new { },
                    timings = new
                    {
                        send = 0,
                        wait = durationMs,
                        receive = 0
                    },
                    serverIPAddress = context.Connection.LocalIpAddress?.ToString() ?? "",
                    connection = context.Connection.RemotePort.ToString()
                };

                _entries.Add(entry);
                await UpdateHarFile();

                Console.WriteLine($"[HAR] Added entry #{_entries.Count} for {context.Request.Method} {context.Request.Path}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HAR] Logging error");
            }
        }

        private async Task UpdateHarFile()
        {
            try
            {
                var harData = new
                {
                    log = new
                    {
                        version = "1.2",
                        creator = new { name = "BlueArchiveAPI", version = "1.0" },
                        entries = _entries.ToArray()
                    }
                };

                var json = JsonSerializer.Serialize(harData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_logFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HAR] Update error");
            }
        }

        private static string GetStatusText(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                201 => "Created",
                204 => "No Content",
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => ""
            };
        }
    }
}
