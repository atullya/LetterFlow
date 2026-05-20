using System.Text.Json;
using System.Text.Json.Serialization;

namespace LetterTemplatePractice.Services
{
    /// <summary>
    /// Fetches today's top stories from the KCha Khabar API and returns the top N.
    /// API endpoint: GET https://kchakhabar.com/api/v1/today.json
    /// </summary>
    public sealed class NewsService
    {
        private const string ApiUrl = "https://kchakhabar.com/api/v1/today.json";

        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<NewsService> _logger;

        public NewsService(IHttpClientFactory httpFactory, ILogger<NewsService> logger)
        {
            _httpFactory = httpFactory;
            _logger      = logger;
        }

        /// <summary>Fetches today's news and returns the top <paramref name="count"/> stories.</summary>
        public async Task<List<NewsStory>> GetTopStoriesAsync(int count = 5, CancellationToken ct = default)
        {
            using var http = _httpFactory.CreateClient("news");

            try
            {
                var response = await http.GetAsync(ApiUrl, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<KchaKhabarResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

                if (json?.Stories == null || json.Stories.Count == 0)
                {
                    _logger.LogWarning("NewsService: API returned no stories.");
                    return [];
                }

                return json.Stories
                    .Where(s => !string.IsNullOrWhiteSpace(s.TopicEn) && !string.IsNullOrWhiteSpace(s.SummaryEn))
                    .Take(count)
                    .Select(s => new NewsStory
                    {
                        Title      = s.TopicEn!,
                        Summary    = s.SummaryEn!,
                        Url        = s.Url ?? string.Empty,
                        ImageUrl   = s.Sources?.FirstOrDefault()?.ImageUrl,
                        Source     = s.Sources?.FirstOrDefault()?.Publisher ?? "KCha Khabar",
                        ReportedAt = s.FirstReported
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NewsService: Failed to fetch stories from KCha Khabar API.");
                return [];
            }
        }

        // ── DTOs ─────────────────────────────────────────────────────────────

        private sealed class KchaKhabarResponse
        {
            [JsonPropertyName("stories")]
            public List<KchaStory>? Stories { get; set; }
        }

        private sealed class KchaStory
        {
            [JsonPropertyName("topic_en")]
            public string? TopicEn { get; set; }

            [JsonPropertyName("topic_ne")]
            public string? TopicNe { get; set; }

            [JsonPropertyName("summary_en")]
            public string? SummaryEn { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("first_reported")]
            public DateTime FirstReported { get; set; }

            [JsonPropertyName("sources")]
            public List<KchaSource>? Sources { get; set; }
        }

        private sealed class KchaSource
        {
            [JsonPropertyName("publisher")]
            public string? Publisher { get; set; }

            [JsonPropertyName("image_url")]
            public string? ImageUrl { get; set; }
        }
    }

    /// <summary>Simplified story DTO passed around the app.</summary>
    public sealed class NewsStory
    {
        public string Title      { get; set; } = string.Empty;
        public string Summary    { get; set; } = string.Empty;
        public string Url        { get; set; } = string.Empty;
        public string? ImageUrl  { get; set; }
        public string Source     { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; }
    }
}
