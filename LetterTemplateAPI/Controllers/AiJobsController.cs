using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers
{
    [ApiController]
    [Route("api/ai/jobs")]
    [Authorize]
    public sealed class AiJobsController : ControllerBase
    {
        private readonly IAiQueue _q;
        public AiJobsController(IAiQueue q) => _q = q;
        private int? Cid { get { var v = User.FindFirstValue(ClaimTypes.NameIdentifier); return int.TryParse(v, out var i) ? i : null; } }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var j = await _q.GetAsync(id, ct);
            if (j == null) return NotFound();
            if (j.OwnerUserId != Cid && !User.IsInRole("Admin")) return Forbid();
            return Ok(new { id = j.Id, type = j.Type, status = j.Status, attempts = j.Attempts, maxAttempts = j.MaxAttempts, result = j.Status == AiJobStatus.Succeeded ? j.Result : null, error = j.Status == AiJobStatus.Failed ? j.Error : null, createdAt = j.CreatedAt, startedAt = j.StartedAt, completedAt = j.CompletedAt, nextAttemptAt = j.NextAttemptAt });
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        {
            var j = await _q.GetAsync(id, ct);
            if (j == null) return NotFound();
            if (j.OwnerUserId != Cid && !User.IsInRole("Admin")) return Forbid();
            return !await _q.CancelAsync(id, ct) ? Conflict(new { error = "Not cancellable." }) : Ok(new { message = "Cancelled." });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id:guid}/requeue")]
        public async Task<IActionResult> Requeue(Guid id, CancellationToken ct)
            => !await _q.RequeueAsync(id, ct) ? Conflict(new { error = "Not requeueable." }) : Ok(new { message = "Requeued." });

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var counts = await _q.GetStatusCountsAsync(ct);
            return Ok(counts);
        }
    }
}
