using System.Text.Json;
using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Tests
{
    /// <summary>
    /// A simple HAR replay tool for testing the Blue Archive server.
    /// This reads the captured HAR file and replays requests to validate server responses.
    /// </summary>
    public class HarReplayTool
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public HarReplayTool(string baseUrl = "https://localhost:5000")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            
            // Configure HTTP client to accept self-signed certificates for testing
            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
        }

        /// <summary>
        /// Replays HAR entries that match Blue Archive API patterns
        /// </summary>
        public async Task<List<ReplayResult>> ReplayHarFile(string harFilePath)
        {
            var results = new List<ReplayResult>();
            
            try
            {
                var harContent = await File.ReadAllTextAsync(harFilePath);
                var harDoc = JsonDocument.Parse(harContent);
                
                var entries = harDoc.RootElement
                    .GetProperty("log")
                    .GetProperty("entries")
                    .EnumerateArray();

                foreach (var entry in entries)
                {
                    var request = entry.GetProperty("request");
                    var url = request.GetProperty("url").GetString();
                    
                    // Only replay Blue Archive API calls
                    if (IsBlueArchiveApiCall(url))
                    {
                        var result = await ReplayEntry(entry);
                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new ReplayResult
                {
                    Url = harFilePath,
                    Success = false,
                    ErrorMessage = $"Failed to parse HAR file: {ex.Message}"
                });
            }

            return results;
        }

        private bool IsBlueArchiveApiCall(string? url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            
            return url.Contains("/api/") || 
                   url.Contains("/gateway/") ||
                   url.Contains("nxm-tw-bagl.nexon.com");
        }

        private async Task<ReplayResult> ReplayEntry(JsonElement entry)
        {
            var result = new ReplayResult();
            
            try
            {
                var request = entry.GetProperty("request");
                var method = request.GetProperty("method").GetString();
                var url = request.GetProperty("url").GetString();
                
                result.Url = url;
                result.Method = method;

                // Convert the original URL to point to our local server
                var localUrl = ConvertToLocalUrl(url);
                
                // Create HTTP request
                var httpRequest = new HttpRequestMessage(new HttpMethod(method), localUrl);
                
                // Add headers
                if (request.TryGetProperty("headers", out var headers))
                {
                    foreach (var header in headers.EnumerateArray())
                    {
                        var name = header.GetProperty("name").GetString();
                        var value = header.GetProperty("value").GetString();
                        
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                        {
                            try
                            {
                                httpRequest.Headers.Add(name, value);
                            }
                            catch
                            {
                                // Some headers can't be added directly, ignore them
                            }
                        }
                    }
                }

                // Add body for POST requests
                if (method == "POST" && request.TryGetProperty("postData", out var postData))
                {
                    if (postData.TryGetProperty("text", out var bodyText))
                    {
                        var body = bodyText.GetString();
                        if (!string.IsNullOrEmpty(body))
                        {
                            httpRequest.Content = new StringContent(body);
                        }
                    }
                }

                // Send request
                var response = await _httpClient.SendAsync(httpRequest);
                
                result.StatusCode = (int)response.StatusCode;
                result.Success = response.IsSuccessStatusCode;
                result.ResponseBody = await response.Content.ReadAsStringAsync();

                if (!result.Success)
                {
                    result.ErrorMessage = $"HTTP {result.StatusCode}: {response.ReasonPhrase}";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private string ConvertToLocalUrl(string? originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl)) return _baseUrl;
            
            var uri = new Uri(originalUrl);
            var path = uri.PathAndQuery;
            
            // Convert nexon server paths to local server paths
            if (path.Contains("nxm-tw-bagl.nexon.com"))
            {
                // Extract the API path
                var apiIndex = path.IndexOf("/api/");
                if (apiIndex >= 0)
                {
                    path = path.Substring(apiIndex);
                }
            }
            
            return $"{_baseUrl}{path}";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class ReplayResult
    {
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}