using LetterTemplatePractice.Models;

namespace LetterTemplatePractice.Services
{
    public interface IAiQueue
    {
        /// <summary>
        /// Enqueue a new AI job. Returns the created job (with Id assigned).
        /// </summary>
        Task<AiJob> EnqueueAsync(string type, string input, int? ownerUserId, CancellationToken ct = default);

        /// <summary>Get a job by id, or null if not found.</summary>
        Task<AiJob?> GetAsync(Guid jobId, CancellationToken ct = default);

        /// <summary>Cancel a pending job. Returns true if cancelled.</summary>
        Task<bool> CancelAsync(Guid jobId, CancellationToken ct = default);

        /// <summary>Requeue a failed job for another attempt. Returns true if requeued.</summary>
        Task<bool> RequeueAsync(Guid jobId, CancellationToken ct = default);

        /// <summary>
        /// Dequeue the next available job for processing.
        /// Uses a transaction to avoid race conditions between workers.
        /// Returns null if no jobs are available.
        /// </summary>
        Task<AiJob?> DequeueAsync(string workerId, CancellationToken ct = default);

        /// <summary>Mark a job as succeeded with its result.</summary>
        Task SucceedAsync(AiJob job, string result, CancellationToken ct = default);

        /// <summary>
        /// Mark a job as failed or re-queued for retry.
        /// If attempts remain, sets Status=Pending and schedules NextAttemptAt.
        /// Otherwise sets Status=Failed.
        /// </summary>
        Task HandleFailureAsync(AiJob job, Exception ex, bool isTransient, bool isRateLimit = false, CancellationToken ct = default);

        // ── Admin queries ─────────────────────────────────────────────────

        Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken ct = default);

        Task<List<AiJob>> GetRecentJobsAsync(int limit = 50, CancellationToken ct = default);

        Task<List<AiJob>> GetRecentErrorsAsync(int limit = 50, CancellationToken ct = default);

        /// <summary>Paginated job list with optional status/type/owner filters.</summary>
        Task<Logging.PagedResult<AiJob>> GetPagedJobsAsync(
            int     page     = 1,
            int     pageSize = 25,
            string? status   = null,
            string? type     = null,
            string? owner    = null,
            CancellationToken ct = default);

        /// <summary>Paginated failed-job list with optional date range and keyword.</summary>
        Task<Logging.PagedResult<AiJob>> GetPagedErrorsAsync(
            int               page      = 1,
            int               pageSize  = 25,
            DateTimeOffset?   from      = null,
            DateTimeOffset?   to        = null,
            string?           keyword   = null,
            CancellationToken ct        = default);
    }
}
