using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Services;
using Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public sealed class AiDashboardController : Controller
    {
        private readonly IAiQueue _queue;
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _logger;
        private readonly IConfiguration _config;

        public AiDashboardController(IAiQueue queue, ApplicationDbContext db, IAppLogger logger, IConfiguration config)
        {
            _queue = queue;
            _db = db;
            _logger = logger;
            _config = config;
        }

        // GET /Admin/AiDashboard
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var counts = await _queue.GetStatusCountsAsync(ct);

            var recent24hSucceeded = await _db.AiJobs
                .CountAsync(j => j.Status == AiJobStatus.Succeeded
                                 && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);

            var recent24hFailed = await _db.AiJobs
                .CountAsync(j => j.Status == AiJobStatus.Failed
                                 && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);

            ViewBag.Counts = counts;
            ViewBag.Succeeded24h = recent24hSucceeded;
            ViewBag.Failed24h = recent24hFailed;

            return View();
        }

        // GET /Admin/AiDashboard/Jobs
        public async Task<IActionResult> Jobs(CancellationToken ct)
        {
            var jobs = await _queue.GetRecentJobsAsync(100, ct);
            return View(jobs);
        }

        // GET /Admin/AiDashboard/Errors
        public async Task<IActionResult> Errors(CancellationToken ct)
        {
            var errors = await _queue.GetRecentErrorsAsync(100, ct);

            // Also read last N lines from Serilog file
            var logLines = new List<string>();
            var logPath = _config["Serilog:WriteTo:1:Args:path"];
            if (!string.IsNullOrEmpty(logPath))
            {
                // Serilog uses {Date} in the path; find the latest log file
                var dir = Path.GetDirectoryName(logPath);
                if (dir != null && Directory.Exists(dir))
                {
                    var latestFile = Directory.GetFiles(dir, "*.json")
                        .OrderByDescending(f => f)
                        .FirstOrDefault();
                    if (latestFile != null)
                    {
                        try
                        {
                            var allLines = await System.IO.File.ReadAllLinesAsync(latestFile, ct);
                            logLines = allLines.TakeLast(100).ToList();
                        }
                        catch { /* file may be locked */ }
                    }
                }
            }

            ViewBag.LogLines = logLines;
            return View(errors);
        }

        // ── Dashboard API (JSON for AJAX polling) ──────────────────────────

        // GET /Admin/api/ai/overview
        [HttpGet("/admin/api/ai/overview")]
        public async Task<IActionResult> ApiOverview(CancellationToken ct)
        {
            var counts = await _queue.GetStatusCountsAsync(ct);

            var succeeded24h = await _db.AiJobs
                .CountAsync(j => j.Status == AiJobStatus.Succeeded
                                 && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);

            var failed24h = await _db.AiJobs
                .CountAsync(j => j.Status == AiJobStatus.Failed
                                 && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);

            // Volume over time (last 24h, bucketed by hour)
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var hourly = await _db.AiJobs
                .Where(j => j.CreatedAt >= cutoff)
                .GroupBy(j => new { j.CreatedAt.Year, j.CreatedAt.Month, j.CreatedAt.Day, j.CreatedAt.Hour })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ThenBy(x => x.Hour)
                .ToListAsync(ct);

            // Average attempts and latency for completed jobs in last 24h
            var completedJobs = await _db.AiJobs
                .Where(j => (j.Status == AiJobStatus.Succeeded || j.Status == AiJobStatus.Failed)
                            && j.CompletedAt >= cutoff)
                .Select(j => new { j.Attempts, j.CreatedAt, j.CompletedAt })
                .ToListAsync(ct);

            var avgAttempts = completedJobs.Any() ? completedJobs.Average(j => j.Attempts) : 0;
            var avgLatencyMs = completedJobs.Any()
                ? completedJobs
                    .Where(j => j.CompletedAt.HasValue)
                    .Average(j => (j.CompletedAt!.Value - j.CreatedAt).TotalMilliseconds)
                : 0;

            return Ok(new
            {
                pending = counts.GetValueOrDefault(AiJobStatus.Pending),
                inProgress = counts.GetValueOrDefault(AiJobStatus.InProgress),
                succeeded24h,
                failed24h,
                avgAttempts = Math.Round(avgAttempts, 1),
                avgLatencyMs = Math.Round(avgLatencyMs),
                hourly
            });
        }

        // GET /Admin/api/ai/jobs?limit=50
        [HttpGet("/admin/api/ai/jobs")]
        public async Task<IActionResult> ApiJobs([FromQuery] int limit = 50, CancellationToken ct = default)
        {
            var jobs = await _db.AiJobs
                .Include(j => j.Owner)
                .OrderByDescending(j => j.CreatedAt)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(j => new
                {
                    j.Id,
                    j.Type,
                    Owner = j.Owner != null ? j.Owner.Username : null,
                    j.Status,
                    j.Attempts,
                    j.MaxAttempts,
                    j.CreatedAt,
                    j.CompletedAt,
                    j.Error
                })
                .ToListAsync(ct);

            return Ok(jobs);
        }

        // GET /Admin/api/ai/errors?since=...
        [HttpGet("/admin/api/ai/errors")]
        public async Task<IActionResult> ApiErrors([FromQuery] DateTimeOffset? since, CancellationToken ct)
        {
            var query = _db.AiJobs
                .Where(j => j.Status == AiJobStatus.Failed && j.Error != null);

            if (since.HasValue)
                query = query.Where(j => j.CreatedAt >= since.Value);

            var errors = await query
                .Include(j => j.Owner)
                .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                .Take(100)
                .Select(j => new
                {
                    j.Id,
                    j.Type,
                    Owner = j.Owner != null ? j.Owner.Username : null,
                    j.Error,
                    j.Attempts,
                    j.CreatedAt,
                    j.CompletedAt
                })
                .ToListAsync(ct);

            return Ok(errors);
        }
    }
}
