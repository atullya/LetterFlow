using LetterTemplatePractice.Models;
using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LetterTemplatePractice.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/ai/jobs")]
    public sealed class AiJobsController : ControllerBase
    {
        private readonly IAiQueue _queue;

        public AiJobsController(IAiQueue queue) => _queue = queue;

        private int? CurrentUserId
        {
            get
            {
                var val = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(val, out var id) ? id : null;
            }
        }

        // GET /api/ai/jobs/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetJob(Guid id, CancellationToken ct)
        {
            var job = await _queue.GetAsync(id, ct);
            if (job == null) return NotFound();

            // Only owner or Admin can view
            if (job.OwnerUserId != CurrentUserId && !User.IsInRole("Admin"))
                return Forbid();

            return Ok(new
            {
                id = job.Id,
                type = job.Type,
                status = job.Status,
                attempts = job.Attempts,
                maxAttempts = job.MaxAttempts,
                result = job.Status == AiJobStatus.Succeeded ? job.Result : null,
                error = job.Status == AiJobStatus.Failed ? job.Error : null,
                createdAt = job.CreatedAt,
                startedAt = job.StartedAt,
                completedAt = job.CompletedAt,
                nextAttemptAt = job.NextAttemptAt
            });
        }

        // POST /api/ai/jobs/{id}/cancel
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        {
            var job = await _queue.GetAsync(id, ct);
            if (job == null) return NotFound();

            // Only owner or Admin can cancel
            if (job.OwnerUserId != CurrentUserId && !User.IsInRole("Admin"))
                return Forbid();

            var ok = await _queue.CancelAsync(id, ct);
            if (!ok) return Conflict(new { error = "Job is not in a cancellable state." });

            return Ok(new { message = "Job cancelled." });
        }

        // POST /api/ai/jobs/{id}/requeue  (Admin only)
        [Authorize(Roles = "Admin")]
        [HttpPost("{id:guid}/requeue")]
        public async Task<IActionResult> Requeue(Guid id, CancellationToken ct)
        {
            var ok = await _queue.RequeueAsync(id, ct);
            if (!ok) return Conflict(new { error = "Job is not in a requeueable state." });

            return Ok(new { message = "Job requeued." });
        }
    }
}
