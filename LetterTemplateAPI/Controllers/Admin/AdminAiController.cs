using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/ai")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminAiController : ControllerBase
    {
        private readonly IAiQueue _queue;
        private readonly ApplicationDbContext _db;
        public AdminAiController(IAiQueue queue, ApplicationDbContext db) { _queue = queue; _db = db; }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var counts = await _queue.GetStatusCountsAsync(ct);
            var s24 = await _db.AiJobs.CountAsync(j => j.Status == AiJobStatus.Succeeded && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);
            var f24 = await _db.AiJobs.CountAsync(j => j.Status == AiJobStatus.Failed && j.CompletedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);
            return Ok(new { counts, succeeded24h = s24, failed24h = f24 });
        }

        [HttpGet("jobs")]
        public async Task<IActionResult> Jobs(string? status, string? type, CancellationToken ct, int page = 1, int pageSize = 20)
        {
            var q = _db.AiJobs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(j => j.Status == status);
            if (!string.IsNullOrWhiteSpace(type)) q = q.Where(j => j.Type == type);
            var total = await q.CountAsync(ct);
            var jobs = await q.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(j => new { j.Id, j.Type, j.Status, j.Attempts, j.MaxAttempts, j.OwnerUserId, j.CreatedAt, j.StartedAt, j.CompletedAt }).ToListAsync(ct);
            return Ok(new { total, page, pageSize, jobs });
        }
    }
}
