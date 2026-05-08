using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LetterTemplatePractice.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/ai")]
    public sealed class AiController : ControllerBase
    {
        private readonly GeminiService _ai;

        public AiController(GeminiService ai) => _ai = ai;

        // POST /api/ai/improve
        [HttpPost("improve")]
        public async Task<IActionResult> Improve([FromBody] AiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _ai.ImproveTextAsync(req.Text);
            return Ok(new { result });
        }

        // POST /api/ai/continue
        [HttpPost("continue")]
        public async Task<IActionResult> Continue([FromBody] AiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _ai.ContinueWritingAsync(req.Text);
            return Ok(new { result });
        }

        // POST /api/ai/summarize
        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize([FromBody] AiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "Text is required." });

            var result = await _ai.SummarizeAsync(req.Text);
            return Ok(new { result });
        }

        // POST /api/ai/suggest-title
        [HttpPost("suggest-title")]
        public async Task<IActionResult> SuggestTitle([FromBody] AiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "Text is required." });

            var raw = await _ai.SuggestTitleAsync(req.Text);

            try
            {
                var titles = JsonSerializer.Deserialize<List<string>>(raw);
                return Ok(new { titles });
            }
            catch
            {
                return Ok(new { titles = new List<string> { raw } });
            }
        }

        [HttpPost("suggest-tags")]
        public async Task<IActionResult> SuggestTags([FromBody] AiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "Text is required." });

            var raw = await _ai.SuggestTagsAsync(req.Text);

            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(raw);
                return Ok(new { tags });
            }
            catch
            {
                return Ok(new { tags = new List<string> { raw } });
            }
        }

        // POST /api/ai/suggest-images
        // Gemini picks keywords → server fetches real photos from Pexels API
        [HttpPost("suggest-images")]
        public async Task<IActionResult> SuggestImages([FromBody] AiRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { error = "Text is required." });

            // Step 1 — Gemini picks 3 visual search keywords
            var raw = await _ai.SuggestImageKeywordsAsync(req.Text);

            List<string> keywords;
            try
            {
                keywords = JsonSerializer.Deserialize<List<string>>(raw)
                           ?? new List<string> { "blog", "writing", "technology" };
            }
            catch
            {
                keywords = new List<string> { "blog", "writing", "technology" };
            }

            // Step 2 — fetch 2 photos per keyword from Pexels
            var pexelsKey = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["Pexels:ApiKey"];

            if (string.IsNullOrWhiteSpace(pexelsKey))
                return StatusCode(503, new { error = "Pexels API key not configured. Add Pexels:ApiKey to appsettings." });

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", pexelsKey);

            var images = new List<object>();

            foreach (var kw in keywords)
            {
                try
                {
                    var url      = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(kw)}&per_page=2&orientation=landscape";
                    var response = await http.GetFromJsonAsync<PexelsResponse>(url);

                    if (response?.Photos != null)
                    {
                        foreach (var photo in response.Photos)
                        {
                            images.Add(new
                            {
                                keyword  = kw,
                                url      = photo.Src.Large,        // full-size for cover
                                thumbUrl = photo.Src.Medium,       // thumbnail for preview
                                photographer = photo.Photographer,
                                pexelsUrl    = photo.Url
                            });
                        }
                    }
                }
                catch { /* skip failed keyword, continue with others */ }
            }

            return Ok(new { keywords, images });
        }

        // ── Pexels response DTOs ──────────────────────────────────────────────
        private sealed class PexelsResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("photos")]
            public List<PexelsPhoto>? Photos { get; set; }
        }

        private sealed class PexelsPhoto
        {
            [System.Text.Json.Serialization.JsonPropertyName("url")]
            public string Url { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("photographer")]
            public string Photographer { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("src")]
            public PexelsSrc Src { get; set; } = new();
        }

        private sealed class PexelsSrc
        {
            [System.Text.Json.Serialization.JsonPropertyName("large")]
            public string Large { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("medium")]
            public string Medium { get; set; } = "";
        }
    }

    public sealed record AiRequest(string Text);
}
