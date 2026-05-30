using LetterTemplatePractice.Data;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.BackgroundServices
{
   
    public sealed class ScheduledPostPublisher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScheduledPostPublisher> _logger;

        public ScheduledPostPublisher(
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduledPostPublisher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScheduledPostPublisher started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await PublishScheduledPostsAsync(stoppingToken);
            }

            _logger.LogInformation("ScheduledPostPublisher stopped.");
        }

        private async Task PublishScheduledPostsAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var now = DateTime.UtcNow;

                var duePosts = await db.BlogPosts
                    .Where(p => !p.IsPublished && p.ScheduledAt != null && p.ScheduledAt <= now)
                    .ToListAsync(ct);

                if (duePosts.Count == 0)
                    return;

                foreach (var post in duePosts)
                {
                    post.IsPublished = true;
                    post.PublishedAt = now;
                    post.ScheduledAt = null;
                    post.UpdatedAt = now;
                }

                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "ScheduledPostPublisher: published {Count} scheduled post(s).",
                    duePosts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledPostPublisher: error while publishing scheduled posts.");
            }
        }
    }
}
