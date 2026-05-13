using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Reports")]
    public class ReportsController : Controller
    {
        private const string Category = nameof(ReportsController);

        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _logger;

        public ReportsController(ApplicationDbContext db, IAppLogger logger)
        {
            _db     = db;
            _logger = logger;
        }

        // GET /Admin/Reports
        [HttpGet("")]
        public async Task<IActionResult> Index(
            int     page     = 1,
            int     pageSize = 20,
            int?    userId   = null,
            int?    postId   = null,
            bool    resolved = false)
        {
            var query = _db.Reports
                .Include(r => r.Reporter)
                .Include(r => r.Post)
                .Include(r => r.TargetUser)
                .Where(r => r.IsResolved == resolved);

            if (userId.HasValue)
                query = query.Where(r => r.TargetUserId == userId.Value);

            if (postId.HasValue)
                query = query.Where(r => r.TargetPostId == postId.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page      = page;
            ViewBag.PageSize  = pageSize;
            ViewBag.Total     = total;
            ViewBag.Resolved  = resolved;
            ViewBag.UserId    = userId;
            ViewBag.PostId    = postId;

            return View(items);
        }

        // POST /Admin/Reports/Resolve
        [HttpPost("Resolve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(
            [FromForm] List<int> reportIds,
            [FromForm] string    outcome,   // "dismissed" | "confirmed"
            [FromForm] int?      userId     = null,
            [FromForm] int?      postId     = null)
        {
            if (reportIds == null || !reportIds.Any())
            {
                TempData["ErrorMessage"] = "No reports selected.";
                return RedirectToAction(nameof(Index), new { userId, postId });
            }

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var now     = DateTime.UtcNow;

            var reports = await _db.Reports
                .Include(r => r.Post)
                .Include(r => r.TargetUser)
                .Where(r => reportIds.Contains(r.Id) && !r.IsResolved)
                .ToListAsync();

            foreach (var report in reports)
            {
                report.IsResolved    = true;
                report.Outcome       = outcome;
                report.ResolvedAt    = now;
                report.ResolvedById  = adminId;
            }

            // On "confirmed": keep hidden flag as-is (already set by threshold).
            // On "dismissed": if ALL unresolved reports for that target are now resolved, un-hide.
            if (outcome == "dismissed")
            {
                // Collect affected post IDs and user IDs
                var affectedPostIds = reports
                    .Where(r => r.TargetPostId.HasValue)
                    .Select(r => r.TargetPostId!.Value)
                    .Distinct()
                    .ToList();

                var affectedUserIds = reports
                    .Where(r => r.TargetUserId.HasValue)
                    .Select(r => r.TargetUserId!.Value)
                    .Distinct()
                    .ToList();

                foreach (var pid in affectedPostIds)
                {
                    var stillUnresolved = await _db.Reports
                        .CountAsync(r => r.TargetPostId == pid && !r.IsResolved && !reportIds.Contains(r.Id));

                    if (stillUnresolved == 0)
                    {
                        var post = await _db.BlogPosts.FindAsync(pid);
                        if (post != null)
                        {
                            post.IsHidden  = false;
                            post.UpdatedAt = now;
                        }
                    }
                }

                foreach (var uid in affectedUserIds)
                {
                    var stillUnresolved = await _db.Reports
                        .CountAsync(r => r.TargetUserId == uid && !r.IsResolved && !reportIds.Contains(r.Id));

                    if (stillUnresolved == 0)
                    {
                        var user = await _db.Users.FindAsync(uid);
                        if (user != null)
                        {
                            user.IsHiddenProfile = false;
                            user.UpdatedAt       = now;
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(Category,
                $"Admin {adminId} resolved {reports.Count} report(s) with outcome '{outcome}'.",
                "/Admin/Reports/Resolve", adminId.ToString());

            TempData["SuccessMessage"] = $"{reports.Count} report(s) marked as {outcome}.";
            return RedirectToAction(nameof(Index), new { userId, postId });
        }
    }
}
