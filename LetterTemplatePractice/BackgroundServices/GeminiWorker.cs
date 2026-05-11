using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Services;
using Logging;
using Microsoft.Extensions.Options;

namespace LetterTemplatePractice.BackgroundServices
{
    /// <summary>
    /// Background hosted service that dequeues and processes AiJob entries.
    /// Uses SemaphoreSlim for configurable concurrency and implements
    /// exponential backoff with jitter for transient Gemini failures.
    /// </summary>
    public sealed class GeminiWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AiQueueOptions _opts;
        private readonly IAppLogger _logger;
        private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

        public GeminiWorker(IServiceScopeFactory scopeFactory, IOptions<AiQueueOptions> opts, IAppLogger logger)
        {
            _scopeFactory = scopeFactory;
            _opts = opts.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GeminiWorker", $"Worker {_instanceId} starting — concurrency {_opts.WorkerConcurrency}");

            using var semaphore = new SemaphoreSlim(_opts.WorkerConcurrency);

            while (!stoppingToken.IsCancellationRequested)
            {
                await semaphore.WaitAsync(stoppingToken);

                bool hadJob;
                try
                {
                    hadJob = await ProcessNextJobAsync(semaphore, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    semaphore.Release();
                    break;
                }

                // If there was nothing to do, back off before polling again
                if (!hadJob)
                {
                    try { await Task.Delay(_opts.PollIntervalMs, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private async Task<bool> ProcessNextJobAsync(SemaphoreSlim semaphore, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IAiQueue>();
                var gemini = scope.ServiceProvider.GetRequiredService<GeminiService>();

                var job = await queue.DequeueAsync(_instanceId, ct);

                if (job is null)
                    return false; // No jobs available — caller will back off

                _logger.LogInformation("GeminiWorker",
                    $"Worker {_instanceId} picked up job {job.Id} (type={job.Type}, attempt {job.Attempts}/{job.MaxAttempts})");

                try
                {
                    string result;

                    if (job.Type == AiJobTypes.SuggestImages)
                    {
                        // Two-step pipeline: Gemini keywords → Pexels photo fetch
                        result = await ProcessSuggestImagesAsync(gemini, job.Input, ct);
                    }
                    else
                    {
                        result = job.Type switch
                        {
                            AiJobTypes.Improve      => await gemini.ImproveTextAsync(job.Input, ct),
                            AiJobTypes.Continue     => await gemini.ContinueWritingAsync(job.Input, ct),
                            AiJobTypes.Summarize    => await gemini.SummarizeAsync(job.Input, ct),
                            AiJobTypes.SuggestTitle => await gemini.SuggestTitleAsync(job.Input, ct),
                            AiJobTypes.SuggestTags  => await gemini.SuggestTagsAsync(job.Input, ct),
                            _ => throw new InvalidOperationException($"Unknown job type: {job.Type}")
                        };
                    }

                    await queue.SucceedAsync(job, result, ct);

                    _logger.LogInformation("GeminiWorker",
                        $"Worker {_instanceId} completed job {job.Id}");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Shutting down — leave job as InProgress so it gets picked up on restart
                    _logger.LogWarning("GeminiWorker",
                        $"Worker {_instanceId} cancelled during job {job.Id}");
                }
                catch (Exception ex)
                {
                    var isTransient = IsTransient(ex);
                    var isRateLimit = IsRateLimit(ex);

                    _logger.LogWarning("GeminiWorker",
                        $"Worker {_instanceId} job {job.Id} failed (transient={isTransient}, rateLimit={isRateLimit}, attempt {job.Attempts}): {ex.Message}");

                    // Re-fetch the job from DB since it was detached after dequeue
                    using var retryScope = _scopeFactory.CreateScope();
                    var retryQueue = retryScope.ServiceProvider.GetRequiredService<IAiQueue>();
                    var freshJob = await retryQueue.GetAsync(job.Id, ct);
                    if (freshJob != null)
                    {
                        await retryQueue.HandleFailureAsync(freshJob, ex, isTransient, isRateLimit, ct);
                    }

                    // If we hit a rate limit, pause the whole worker before picking up the next job
                    if (isRateLimit)
                    {
                        var pause = TimeSpan.FromSeconds(_opts.RateLimitBackoffSeconds);
                        _logger.LogWarning("GeminiWorker",
                            $"Worker {_instanceId} rate-limited — pausing {pause.TotalSeconds}s before next job");
                        try { await Task.Delay(pause, ct); }
                        catch (OperationCanceledException) { /* shutting down */ }
                    }
                }

                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError("GeminiWorker", $"Worker {_instanceId} unexpected error", ex);
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<string> ProcessSuggestImagesAsync(GeminiService gemini, string input, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var http = httpFactory.CreateClient();

            // Step 1: Gemini generates 3 visual keywords
            var keywordsRaw = await gemini.SuggestImageKeywordsAsync(input, ct);
            var keywords = JsonSerializer.Deserialize<string[]>(keywordsRaw) ?? [];

            // Step 2: Fetch 2 landscape photos per keyword from Pexels
            var pexelsKey = config["Pexels:ApiKey"];
            var allPhotos = new List<object>();

            foreach (var keyword in keywords.Take(3))
            {
                var url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(keyword)}&per_page=2&orientation=landscape";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Authorization", pexelsKey ?? "");

                var resp = await http.SendAsync(req, ct);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadFromJsonAsync<PexelsResponse>(ct);
                if (json?.Photos != null)
                {
                    allPhotos.AddRange(json.Photos.Select(p => new
                    {
                        keyword,
                        id = p.Id,
                        src = p.Src?.Large2X ?? p.Src?.Large ?? p.Src?.Medium,
                        alt = p.Alt ?? keyword,
                        photographer = p.Photographer
                    }));
                }
            }

            return JsonSerializer.Serialize(allPhotos);
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is HttpRequestException)
                return true;

            var msg = ex.Message;
            if (msg.Contains("503") || msg.Contains("429") || msg.Contains("Service Unavailable") || msg.Contains("Too Many Requests"))
                return true;

            return false;
        }

        private static bool IsRateLimit(Exception ex)
        {
            var msg = ex.Message;
            return msg.Contains("429") || msg.Contains("Too Many Requests");
        }

        // ── Pexels DTOs ─────────────────────────────────────────────────────

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
}
