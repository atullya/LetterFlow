using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Logging;
using Microsoft.Extensions.Configuration;

namespace LetterTemplate.RAG.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _embeddingModel;
        private readonly IAppLogger _logger;

        public EmbeddingService(IHttpClientFactory factory, IConfiguration config, IAppLogger logger)
        {
            _http = factory.CreateClient("gemini");
            _apiKey = config["Gemini:ApiKey"]
                      ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
            _embeddingModel = config["Gemini:EmbeddingModel"] ?? "text-embedding-004";
            _logger = logger;
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_embeddingModel}:embedContent?key={_apiKey}";

            var body = new
            {
                model = $"models/{_embeddingModel}",
                content = new { parts = new[] { new { text } } }
            };

            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var response = await _http.PostAsJsonAsync(url, body);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
                    var values = json?.Embedding?.Values;
                    if (values == null || values.Length == 0)
                        throw new InvalidOperationException("Empty embedding returned from Gemini.");

                    return values;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("EmbeddingService", $"Embed attempt {attempt + 1}/3 failed: {ex.Message}");
                    if (attempt == 2)
                    {
                        _logger.LogError("EmbeddingService", "All embed attempts failed.", ex);
                        throw;
                    }

                    var delay = (int)Math.Pow(2, attempt) * 500;
                    await Task.Delay(delay);
                }
            }

            throw new InvalidOperationException("Embedding failed after all retries.");
        }

        public async Task<List<float[]>> EmbedBatchAsync(List<string> texts)
        {
            var semaphore = new SemaphoreSlim(5);
            var tasks = texts.Select(async text =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await EmbedAsync(text);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        private sealed class EmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public EmbeddingData? Embedding { get; set; }
        }

        private sealed class EmbeddingData
        {
            [JsonPropertyName("values")]
            public float[]? Values { get; set; }
        }
    }
}
