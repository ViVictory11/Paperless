using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Paperless.OcrWorker.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;
        private const string GeminiModel = "gemini-2.0-flash";

        public GeminiService(HttpClient httpClient, ILogger<GeminiService> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not found");
        }

        public async Task<string> SummarizeAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("GeminiService: Empty input text, skipping summarization.");
                return string.Empty;
            }

            try
            {
                _logger.LogInformation("GeminiService: Preparing summarization request...");

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = $"Please summarize the following text into 4-5 clear bullet points (starting with a dash, each line with a key point and in the same language as the text) \n{text}"
                                }
                            }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);

                var request = new HttpRequestMessage(
                    HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("X-goog-api-key", _apiKey);

                _logger.LogInformation("GeminiService: Sending summarization request to {Model}", GeminiModel);

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("GeminiService: API returned {Code} - {Body}", response.StatusCode, body);
                    return string.Empty;
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("GeminiService: Response received ({Length} chars)", content.Length);

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                string? summary = null;
                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.ValueKind == JsonValueKind.Array &&
                    candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var contentProp) &&
                        contentProp.TryGetProperty("parts", out var parts) &&
                        parts.ValueKind == JsonValueKind.Array &&
                        parts.GetArrayLength() > 0)
                    {
                        summary = parts[0].GetProperty("text").GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(summary))
                {
                    _logger.LogWarning("GeminiService: No summary text found in Gemini response.");
                    return string.Empty;
                }

                _logger.LogInformation("GeminiService: Summary generated successfully ({Len} chars)", summary.Length);
                return summary.Trim();
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("GeminiService: Request timed out while summarizing.");
                return string.Empty;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "GeminiService: Invalid JSON in Gemini response.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeminiService: Unexpected exception during summarization.");
                return string.Empty;
            }
        }
    }
}
