using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminDashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public AdminDashboardController(ApplicationDbContext db) => _db = db;

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var totalPosts = await _db.BlogPosts.CountAsync();
            var publishedPosts = await _db.BlogPosts.CountAsync(p => p.IsPublished);
            var totalUsers = await _db.Users.CountAsync();
            var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
            var totalComments = await _db.BlogComments.CountAsync();
            var totalLikes = await _db.BlogLikes.CountAsync();
            var totalViews = await _db.PostViews.CountAsync();
            var totalReports = await _db.Reports.CountAsync(r => !r.IsResolved);
            var pendingAiJobs = await _db.AiJobs.CountAsync(j => j.Status == AiJobStatus.Pending || j.Status == AiJobStatus.InProgress);

            return Ok(new { totalPosts, publishedPosts, totalUsers, activeUsers, totalComments, totalLikes, totalViews, unresolvedReports = totalReports, pendingAiJobs, recentPosts = await RecentPosts() });
        }

        private async Task<object> RecentPosts()
        {
            return await _db.BlogPosts.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(5)
                .Select(p => new { p.Id, p.Title, p.Slug, p.IsPublished, p.PublishedAt, p.ViewCount, Author = new { p.Author!.Username } })
                .ToListAsync();
        }
    }
}
