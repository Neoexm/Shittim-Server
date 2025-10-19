using BlueArchiveAPI.Services;
using System.Diagnostics;

namespace BlueArchiveAPI.Middleware
{
    public class HarLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HarLoggingService _harService;

        public HarLoggingMiddleware(RequestDelegate next, HarLoggingService harService)
        {
            _next = next;
            _harService = harService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            // Capture request body
            context.Request.EnableBuffering();
            var requestBody = await ReadBodyAsync(context.Request.Body);
            context.Request.Body.Position = 0;

            // Capture response body
            var originalResponseBody = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                // Read response body
                responseBodyStream.Position = 0;
                var responseBody = await ReadBodyAsync(responseBodyStream);
                
                // Copy response back to original stream
                responseBodyStream.Position = 0;
                await responseBodyStream.CopyToAsync(originalResponseBody);
                context.Response.Body = originalResponseBody;

                // Log to HAR
                await _harService.LogRequestResponse(context, stopwatch.ElapsedMilliseconds, requestBody, responseBody);
            }
        }

        private static async Task<byte[]> ReadBodyAsync(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
