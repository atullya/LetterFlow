using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Logging;

namespace LetterTemplatePractice.Services
{
    /// <summary>
    /// Thin wrapper around the Gemini 2.5 Flash REST API (free tier).
    /// Docs: https://ai.google.dev/api/generate-content
    /// </summary>
    public sealed class GeminiService
    {
        private readonly HttpClient    _http;
        private readonly string        _apiKey;
        private readonly IAppLogger    _logger;

        // Model — gemini-2.0-flash is free and fast
        private const string Model = "gemini-2.5-flash";

        public GeminiService(IHttpClientFactory factory, IConfiguration config, IAppLogger logger)
        {
            _http   = factory.CreateClient("gemini");
            _apiKey = config["Gemini:ApiKey"]
                      ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
            _logger = logger;
        }

        // ── Public actions ────────────────────────────────────────────────────

        public Task<string> ImproveTextAsync(string text, CancellationToken ct = default)
            => GenerateAsync(
                "You are a professional writing editor. Rewrite the given text to be clearer, " +
                "more engaging, and better structured. Keep the same meaning and approximate length. " +
                "Return ONLY the improved text — no explanations, no quotes, no preamble.",
                text, ct);

        public Task<string> ContinueWritingAsync(string text, CancellationToken ct = default)
            => GenerateAsync(
                "You are a creative writing assistant. Continue the given blog post naturally. " +
                "Write 2–3 paragraphs that flow from where the text ends. " +
                "Match the author's tone and style. Return ONLY the continuation text.",
                text, ct);

        public Task<string> SummarizeAsync(string text, CancellationToken ct = default)
            => GenerateAsync(
                "Write a concise 1–2 sentence summary of the following blog post content. " +
                "This will be used as the post excerpt shown in feed cards. " +
                "Return ONLY the summary — no labels, no quotes.",
                text, ct);

        public Task<string> SuggestTitleAsync(string text, CancellationToken ct = default)
            => GenerateAsync(
                "Suggest 3 compelling blog post titles for the following content. " +
                "Return ONLY a JSON array of 3 strings, e.g. [\"Title One\",\"Title Two\",\"Title Three\"]. " +
                "No explanation, no markdown, just the JSON array.",
                text[..Math.Min(text.Length, 3000)], ct);

        public Task<string> SuggestTagsAsync(string text, CancellationToken ct = default)
            => GenerateAsync(
                "Extract 3–5 relevant topic tags from the following blog post. " +
                "Return ONLY a JSON array of short tag strings, e.g. [\"AI\",\"Web Dev\",\"Career\"]. " +
                "No explanation, no markdown, just the JSON array.",
                text[..Math.Min(text.Length, 3000)], ct);

        /// <summary>
        /// Returns 3 short, concrete Unsplash search keywords based on the blog content.
        /// e.g. ["mountain hiking", "nature trail", "outdoor adventure"]
        /// </summary>
        public Task<string> SuggestImageKeywordsAsync(string text, CancellationToken ct = default)
            => GenerateAsync(
                "You are a photo editor choosing a cover image for a blog post. " +
                "Based on the blog content, suggest 3 short, concrete, visually descriptive search phrases " +
                "suitable for finding a great cover photo on Unsplash. " +
                "Each phrase should be 1–3 words, specific and visual (e.g. 'mountain sunrise', 'coffee laptop', 'city skyline'). " +
                "Return ONLY a JSON array of 3 strings. No explanation, no markdown, just the JSON array.",
                text[..Math.Min(text.Length, 3000)], ct);


        private async Task<string> GenerateAsync(string systemPrompt, string userContent, CancellationToken ct = default)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={_apiKey}";

            var body = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role  = "user",
                        parts = new[] { new { text = userContent } }
                    }
                },
                generationConfig = new
                {
                    temperature     = 0.7,
                    maxOutputTokens = 1024
                }
            };
    
            try
            {
                var response = await _http.PostAsJsonAsync(url, body, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);
                return json?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim()
                       ?? "No response from AI.";
            }
            catch (Exception ex)
            {
                _logger.LogError("GeminiService", "Gemini API call failed", ex);
                throw;
            }
        }

        // ── Response DTOs ─────────────────────────────────────────────────────

        private sealed class GeminiResponse
        {
            [JsonPropertyName("candidates")]
            public List<Candidate>? Candidates { get; set; }
        }

        private sealed class Candidate
        {
            [JsonPropertyName("content")]
            public Content? Content { get; set; }
        }

        private sealed class Content
        {
            [JsonPropertyName("parts")]
            public List<Part>? Parts { get; set; }
        }

        private sealed class Part
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
