using LetterTemplatePractice.Data;
using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Newsletter")]
    public sealed class NewsletterController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly NewsService _news;
        private readonly NewsletterSender _sender;
        private readonly IConfiguration _config;
        private readonly ILogger<NewsletterController> _logger;

        public NewsletterController(
            ApplicationDbContext db,
            NewsService news,
            NewsletterSender sender,
            IConfiguration config,
            ILogger<NewsletterController> logger)
        {
            _db     = db;
            _news   = news;
            _sender = sender;
            _config = config;
            _logger = logger;
        }

        // POST /Admin/Newsletter/SendNow
        // Triggers an immediate digest send — useful for testing.
        [HttpPost("SendNow")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNow(CancellationToken ct)
        {
            var stories = await _news.GetTopStoriesAsync(5, ct);
            if (stories.Count == 0)
                return RedirectToAction("Index", "AiDashboard",
                    new { toast = "error", msg = "No stories fetched from API." });

            var subscribers = await _db.NewsletterSubscriptions
                .Include(s => s.User)
                .Where(s => s.IsActive)
                .ToListAsync(ct);

            if (subscribers.Count == 0)
                return RedirectToAction("Index", "AiDashboard",
                    new { toast = "warn", msg = "No active subscribers found." });

            var baseUrl = _config["Newsletter:BaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";
            var sent    = await _sender.SendDigestAsync(subscribers, stories, baseUrl, ct);

            _logger.LogInformation("Admin triggered newsletter send: {Sent}/{Total} delivered.", sent, subscribers.Count);

            TempData["ToastType"]    = sent > 0 ? "success" : "error";
            TempData["ToastMessage"] = $"Digest sent to {sent}/{subscribers.Count} subscribers.";
            return RedirectToAction("Index", "AiDashboard");
        }

        // POST /Admin/Newsletter/Subscribe
        // Adds an email to the subscriber list (admin use / testing).
        [HttpPost("Subscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string email, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ToastType"]    = "error";
                TempData["ToastMessage"] = "Email is required.";
                return RedirectToAction("Index", "AiDashboard");
            }

            email = email.Trim().ToLowerInvariant();

            var existing = await _db.NewsletterSubscriptions
                .FirstOrDefaultAsync(s => s.Email == email, ct);

            if (existing != null)
            {
                // Re-activate if previously unsubscribed
                existing.IsActive        = true;
                existing.UnsubscribedAt  = null;
                await _db.SaveChangesAsync(ct);
                TempData["ToastType"]    = "success";
                TempData["ToastMessage"] = $"{email} re-subscribed.";
            }
            else
            {
                _db.NewsletterSubscriptions.Add(new Models.NewsletterSubscription
                {
                    Email       = email,
                    IsActive    = true,
                    SubscribedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
                TempData["ToastType"]    = "success";
                TempData["ToastMessage"] = $"{email} subscribed.";
            }

            return RedirectToAction("Index", "AiDashboard");
        }

        // GET /Newsletter/Unsubscribe?token=...
        // Public unsubscribe link used in email footers.
        [AllowAnonymous]
        [HttpGet("/Newsletter/Unsubscribe")]
        public async Task<IActionResult> Unsubscribe(string token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Invalid unsubscribe link.");

            var sub = await _db.NewsletterSubscriptions
                .FirstOrDefaultAsync(s => s.UnsubscribeToken == token, ct);

            if (sub == null)
                return NotFound("Subscription not found.");

            sub.IsActive       = false;
            sub.UnsubscribedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return View("Unsubscribed");
        }
    }
}
