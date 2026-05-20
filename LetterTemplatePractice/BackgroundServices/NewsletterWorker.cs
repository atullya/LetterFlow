using LetterTemplatePractice.Data;
using LetterTemplatePractice.Services;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.BackgroundServices
{
    /// <summary>
    /// Hosted service that fires the daily news digest once per day at the configured UTC hour.
    /// Default: 06:00 UTC (configurable via Newsletter:SendHourUtc in appsettings).
    ///
    /// Flow:
    ///   1. Wait until the next scheduled send time.
    ///   2. Fetch top 5 stories from KCha Khabar API.
    ///   3. Load active subscriber emails from the DB.
    ///   4. Build HTML and send via SMTP.
    ///   5. Sleep until the next day.
    /// </summary>
    public sealed class NewsletterWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<NewsletterWorker> _logger;

        public NewsletterWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ILogger<NewsletterWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _config       = config;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NewsletterWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextSend();
                _logger.LogInformation("NewsletterWorker: next digest in {Minutes:F0} minutes.", delay.TotalMinutes);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await SendDigestAsync(stoppingToken);
            }

            _logger.LogInformation("NewsletterWorker stopped.");
        }

        private async Task SendDigestAsync(CancellationToken ct)
        {
            _logger.LogInformation("NewsletterWorker: starting daily digest send.");

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var newsService = scope.ServiceProvider.GetRequiredService<NewsService>();
                var sender      = scope.ServiceProvider.GetRequiredService<NewsletterSender>();
                var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. Fetch top 5 stories
                var stories = await newsService.GetTopStoriesAsync(5, ct);
                if (stories.Count == 0)
                {
                    _logger.LogWarning("NewsletterWorker: no stories fetched — skipping send.");
                    return;
                }

                // 2. Load active subscribers
                var subscribers = await db.NewsletterSubscriptions
                    .Include(s => s.User)
                    .Where(s => s.IsActive)
                    .ToListAsync(ct);

                if (subscribers.Count == 0)
                {
                    _logger.LogInformation("NewsletterWorker: no active subscribers — skipping send.");
                    return;
                }

                _logger.LogInformation(
                    "NewsletterWorker: sending {StoryCount} stories to {SubCount} subscribers.",
                    stories.Count, subscribers.Count);

                // 3. Build base URL for unsubscribe links
                var baseUrl = _config["Newsletter:BaseUrl"]?.TrimEnd('/') ?? "https://letterflowstories.com";

                // 4. Send
                var sent = await sender.SendDigestAsync(subscribers, stories, baseUrl, ct);

                _logger.LogInformation("NewsletterWorker: digest sent to {Sent}/{Total} subscribers.", sent, subscribers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NewsletterWorker: unhandled error during digest send.");
            }
        }

        /// <summary>
        /// Calculates how long to wait until the next scheduled send.
        /// Sends at Newsletter:SendHourUtc (default 6 = 06:00 UTC) every day.
        /// </summary>
        private TimeSpan GetDelayUntilNextSend()
        {
            var sendHour = _config.GetValue<int>("Newsletter:SendHourUtc", 6);
            var now      = DateTime.UtcNow;
            var next     = new DateTime(now.Year, now.Month, now.Day, sendHour, 0, 0, DateTimeKind.Utc);

            if (next <= now)
                next = next.AddDays(1);

            return next - now;
        }
    }
}
