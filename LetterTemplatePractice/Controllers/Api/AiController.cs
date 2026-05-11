using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LetterTemplatePractice.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/ai")]
    public sealed class AiController : ControllerBase
    {
        private readonly GeminiService _gemini;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public AiController(GeminiService gemini, IHttpClientFactory httpFactory, IConfiguration config)
        {
            _gemini     = gemini;
            _httpFactory = httpFactory;
            _config     = config;
        }

        // POST /api/ai/improve
        [HttpPost("improve")]
        public async Task<IActionResult> Improve([FromBody] AiRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _gemini.ImproveTextAsync(req.Text, ct);
            return Ok(new { result });
        }

        // POST /api/ai/continue
        [HttpPost("continue")]
        public async Task<IActionResult> Continue([FromBody] AiRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _gemini.ContinueWritingAsync(req.Text, ct);
            return Ok(new { result });
        }

        // POST /api/ai/summarize
        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize([FromBody] AiRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _gemini.SummarizeAsync(req.Text, ct);
            return Ok(new { result });
        }

        // POST /api/ai/suggest-title
        [HttpPost("suggest-title")]
        public async Task<IActionResult> SuggestTitle([FromBody] AiRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _gemini.SuggestTitleAsync(req.Text, ct);
            return Ok(new { result });
        }

        // POST /api/ai/suggest-tags
        [HttpPost("suggest-tags")]
        public async Task<IActionResult> SuggestTags([FromBody] AiRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _gemini.SuggestTagsAsync(req.Text, ct);
            return Ok(new { result });
        }

        // POST /api/ai/suggest-images
        [HttpPost("suggest-images")]
        public async Task<IActionResult> SuggestImages([FromBody] AiRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.Text))
                return BadRequest(new { error = "Text is required." });

            // Step 1: Gemini generates visual keywords
            var keywordsRaw = await _gemini.SuggestImageKeywordsAsync(req.Text, ct);
            var keywords = JsonSerializer.Deserialize<string[]>(keywordsRaw) ?? [];

            // Step 2: Fetch photos from Pexels per keyword
            var pexelsKey = _config["Pexels:ApiKey"];
            var http = _httpFactory.CreateClient();
            var allPhotos = new List<object>();

            foreach (var keyword in keywords.Take(3))
            {
                var url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(keyword)}&per_page=2&orientation=landscape";
                using var pexelsReq = new HttpRequestMessage(HttpMethod.Get, url);
                pexelsReq.Headers.Add("Authorization", pexelsKey ?? "");

                var resp = await http.SendAsync(pexelsReq, ct);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadFromJsonAsync<PexelsResponse>(ct);
                if (json?.Photos != null)
                {
                    allPhotos.AddRange(json.Photos.Select(p => new
                    {
                        keyword,
                        id    = p.Id,
                        src   = p.Src?.Large2X ?? p.Src?.Large ?? p.Src?.Medium,
                        alt   = p.Alt ?? keyword,
                        photographer = p.Photographer
                    }));
                }
            }

            return Ok(new { result = allPhotos });
        }

        // ── Pexels DTOs ─────────────────────────────────────────────────

        private sealed class PexelsResponse
        {
            [JsonPropertyName("photos")]
            public List<PexelsPhoto>? Photos { get; set; }
        }

        private sealed class PexelsPhoto
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("alt")]
            public string? Alt { get; set; }

            [JsonPropertyName("photographer")]
            public string? Photographer { get; set; }

            [JsonPropertyName("src")]
            public PexelsSrc? Src { get; set; }
        }

        private sealed class PexelsSrc
        {
            [JsonPropertyName("large")]
            public string? Large { get; set; }

            [JsonPropertyName("large2x")]
            public string? Large2X { get; set; }

            [JsonPropertyName("medium")]
            public string? Medium { get; set; }
        }
    }

    // ── Shared request DTO ─────────────────────────────────────────────

    public sealed class AiRequest
    {
        public string? Text { get; set; }
    }
}
