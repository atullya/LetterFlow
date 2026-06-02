using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace LetterTemplateAPI.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api/ai")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public sealed class AiController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private readonly LetterTemplatePractice.Services.GeminiService _gemini;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;

        public AiController(LetterTemplatePractice.Services.GeminiService gemini, IHttpClientFactory http, IConfiguration cfg) { _gemini = gemini; _http = http; _cfg = cfg; }

        [Microsoft.AspNetCore.Mvc.HttpPost("improve")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> Improve([FromBody] AiRequest r, CancellationToken ct)
        { if (string.IsNullOrWhiteSpace(r?.Text)) return BadRequest(new { error = "Text is required." }); return Ok(new { result = await _gemini.ImproveTextAsync(r.Text, ct) }); }

        [Microsoft.AspNetCore.Mvc.HttpPost("continue")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> Continue([FromBody] AiRequest r, CancellationToken ct)
        { if (string.IsNullOrWhiteSpace(r?.Text)) return BadRequest(new { error = "Text is required." }); return Ok(new { result = await _gemini.ContinueWritingAsync(r.Text, ct) }); }

        [Microsoft.AspNetCore.Mvc.HttpPost("summarize")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> Summarize([FromBody] AiRequest r, CancellationToken ct)
        { if (string.IsNullOrWhiteSpace(r?.Text)) return BadRequest(new { error = "Text is required." }); return Ok(new { result = await _gemini.SummarizeAsync(r.Text, ct) }); }

        [Microsoft.AspNetCore.Mvc.HttpPost("suggest-title")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> SuggestTitle([FromBody] AiRequest r, CancellationToken ct)
        { if (string.IsNullOrWhiteSpace(r?.Text)) return BadRequest(new { error = "Text is required." }); return Ok(new { result = await _gemini.SuggestTitleAsync(r.Text, ct) }); }

        [Microsoft.AspNetCore.Mvc.HttpPost("suggest-tags")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> SuggestTags([FromBody] AiRequest r, CancellationToken ct)
        { if (string.IsNullOrWhiteSpace(r?.Text)) return BadRequest(new { error = "Text is required." }); return Ok(new { result = await _gemini.SuggestTagsAsync(r.Text, ct) }); }

        [Microsoft.AspNetCore.Mvc.HttpPost("suggest-images")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> SuggestImages([FromBody] AiRequest r, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r?.Text)) return BadRequest(new { error = "Text is required." });
            var kw = System.Text.Json.JsonSerializer.Deserialize<string[]>(await _gemini.SuggestImageKeywordsAsync(r.Text, ct)) ?? [];
            var pk = _cfg["Pexels:ApiKey"]; var hc = _http.CreateClient(); var all = new List<object>();
            foreach (var k in kw.Take(3))
            {
                var url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(k)}&per_page=2&orientation=landscape";
                using var req = new HttpRequestMessage(HttpMethod.Get, url); req.Headers.Add("Authorization", pk ?? "");
                var resp = await hc.SendAsync(req, ct); resp.EnsureSuccessStatusCode();
                var j = await resp.Content.ReadFromJsonAsync<PexelsResponse>(ct);
                if (j?.Photos != null) all.AddRange(j.Photos.Select(p => new { keyword = k, id = p.Id, src = p.Src?.Large2X ?? p.Src?.Large ?? p.Src?.Medium, alt = p.Alt ?? k, photographer = p.Photographer }));
            }
            return Ok(new { result = all });
        }

        sealed class PexelsResponse { [JsonPropertyName("photos")] public List<PexelsPhoto>? Photos { get; set; } }
        sealed class PexelsPhoto { [JsonPropertyName("id")] public long Id { get; set; } [JsonPropertyName("alt")] public string? Alt { get; set; } [JsonPropertyName("photographer")] public string? Photographer { get; set; } [JsonPropertyName("src")] public PexelsSrc? Src { get; set; } }
        sealed class PexelsSrc { [JsonPropertyName("large")] public string? Large { get; set; } [JsonPropertyName("large2x")] public string? Large2X { get; set; } [JsonPropertyName("medium")] public string? Medium { get; set; } }
    }

    public sealed class AiRequest { public string? Text { get; set; } }
}
