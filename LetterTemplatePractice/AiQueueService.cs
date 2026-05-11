using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetterTemplatePractice.Services
{
    public class AiQueueService : IAiQueue
    {
        private readonly ApplicationDbContext _db;
        private readonly AiQueueOptions _opts;

        public AiQueueService(ApplicationDbContext db, IOptions<AiQueueOptions> opts)
        {
            _db = db;
            _opts = opts.Value;
        }

        public async Task<AiJob> EnqueueAsync(string type, string input, int? ownerUserId, CancellationToken ct = default)
        {
            var job = new AiJob
            {
                Id = Guid.NewGuid(),
                Type = type,
                Input = input,
                OwnerUserId = ownerUserId,
                Status = AiJobStatus.Pending,
                Attempts = 0,
                MaxAttempts = _opts.MaxAttempts,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.AiJobs.Add(job);
            await _db.SaveChangesAsync(ct);
            return job;
        }

        public async Task<AiJob?> GetAsync(Guid jobId, CancellationToken ct = default)
            => await _db.AiJobs.FindAsync([jobId], ct);

        public async Task<bool> CancelAsync(Guid jobId, CancellationToken ct = default)
        {
            var job = await _db.AiJobs.FindAsync([jobId], ct);
            if (job == null || job.Status != AiJobStatus.Pending) return false;

            job.Status = AiJobStatus.Cancelled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RequeueAsync(Guid jobId, CancellationToken ct = default)
        {
            var job = await _db.AiJobs.FindAsync([jobId], ct);
            if (job == null || job.Status != AiJobStatus.Failed) return false;

            job.Status = AiJobStatus.Pending;
            job.Attempts = 0;
            job.Error = null;
            job.NextAttemptAt = null;
            job.WorkerId = null;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<AiJob?> DequeueAsync(string workerId, CancellationToken ct = default)
        {
            // Use a transaction to avoid race conditions between multiple workers
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var now = DateTimeOffset.UtcNow;

            var job = await _db.AiJobs
                .Where(j => j.Status == AiJobStatus.Pending
                            && (j.NextAttemptAt == null || j.NextAttemptAt <= now))
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (job == null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            // Mark as in-progress
            job.Status = AiJobStatus.InProgress;
            job.StartedAt = now;
            job.WorkerId = workerId;
            job.Attempts++;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Detach so subsequent saves in the same context work cleanly
            _db.Entry(job).State = EntityState.Detached;
            return job;
        }

        public async Task SucceedAsync(AiJob job, string result, CancellationToken ct = default)
        {
            job.Status = AiJobStatus.Succeeded;
            job.Result = result;
            job.CompletedAt = DateTimeOffset.UtcNow;
            _db.AiJobs.Update(job);
            await _db.SaveChangesAsync(ct);
        }

        public async Task HandleFailureAsync(AiJob job, Exception ex, bool isTransient, bool isRateLimit = false, CancellationToken ct = default)
        {
            if (isTransient && job.Attempts < job.MaxAttempts)
            {
                // 429 rate-limit: use a much longer base delay
                double baseSeconds = isRateLimit
                    ? _opts.RateLimitBackoffSeconds
                    : _opts.BaseDelaySeconds;

                var delay = TimeSpan.FromSeconds(
                    Math.Min(
                        baseSeconds * Math.Pow(2, job.Attempts - 1),
                        _opts.MaxBackoffSeconds));

                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, _opts.JitterMs));

                job.Status = AiJobStatus.Pending;
                job.NextAttemptAt = DateTimeOffset.UtcNow + delay + jitter;
                job.Error = ex.Message;
            }
            else
            {
                // Permanent failure
                job.Status = AiJobStatus.Failed;
                job.Error = ex.Message;
                job.CompletedAt = DateTimeOffset.UtcNow;
            }

            _db.AiJobs.Update(job);
            await _db.SaveChangesAsync(ct);
        }

        // ── Admin queries ─────────────────────────────────────────────────

        public async Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken ct = default)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

            var counts = await _db.AiJobs
                .GroupBy(j => j.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var result = new Dictionary<string, int>
            {
                [AiJobStatus.Pending] = 0,
                [AiJobStatus.InProgress] = 0,
                [AiJobStatus.Succeeded] = 0,
                [AiJobStatus.Failed] = 0,
                [AiJobStatus.Cancelled] = 0
            };

            foreach (var row in counts)
                result[row.Status] = row.Count;

            return result;
        }

        public async Task<List<AiJob>> GetRecentJobsAsync(int limit = 50, CancellationToken ct = default)
            => await _db.AiJobs
                .Include(j => j.Owner)
                .OrderByDescending(j => j.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);

        public async Task<List<AiJob>> GetRecentErrorsAsync(int limit = 50, CancellationToken ct = default)
            => await _db.AiJobs
                .Where(j => j.Status == AiJobStatus.Failed && j.Error != null)
                .Include(j => j.Owner)
                .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);

        public async Task<Logging.PagedResult<AiJob>> GetPagedJobsAsync(
            int     page     = 1,
            int     pageSize = 25,
            string? status   = null,
            string? type     = null,
            string? owner    = null,
            CancellationToken ct = default)
        {
            pageSize = Math.Clamp(pageSize, 5, 100);
            page     = Math.Max(1, page);

            var query = _db.AiJobs.Include(j => j.Owner).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(j => j.Status == status);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(j => j.Type == type);

            if (!string.IsNullOrWhiteSpace(owner))
                query = query.Where(j => j.Owner != null && j.Owner.Username.Contains(owner));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new Logging.PagedResult<AiJob>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public async Task<Logging.PagedResult<AiJob>> GetPagedErrorsAsync(
            int               page      = 1,
            int               pageSize  = 25,
            DateTimeOffset?   from      = null,
            DateTimeOffset?   to        = null,
            string?           keyword   = null,
            CancellationToken ct        = default)
        {
            pageSize = Math.Clamp(pageSize, 5, 100);
            page     = Math.Max(1, page);

            var query = _db.AiJobs
                .Where(j => j.Status == AiJobStatus.Failed && j.Error != null)
                .Include(j => j.Owner)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(j => j.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(j => j.CreatedAt <= to.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(j => j.Error!.Contains(keyword) || j.Type.Contains(keyword));

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new Logging.PagedResult<AiJob>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }
    }
}
