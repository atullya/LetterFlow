using LetterTemplatePractice.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/analytics")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminAnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public AdminAnalyticsController(ApplicationDbContext db) => _db = db;

        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
        {
            return Ok(new { totalPosts = await _db.BlogPosts.CountAsync(), publishedPosts = await _db.BlogPosts.CountAsync(p => p.IsPublished), totalUsers = await _db.Users.CountAsync(u => u.IsActive), totalComments = await _db.BlogComments.CountAsync(), totalLikes = await _db.BlogLikes.CountAsync(), totalViews = await _db.PostViews.CountAsync() });
        }

        [HttpGet("top-posts")]
        public async Task<IActionResult> TopPosts(int take = 10)
        {
            var posts = await _db.BlogPosts.AsNoTracking().Where(p => p.IsPublished).OrderByDescending(p => p.ViewCount).Take(take)
                .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount, lc = p.Likes.Count, cc = p.Comments.Count, p.PublishedAt }).ToListAsync();
            return Ok(posts);
        }

        [HttpGet("top-users")]
        public async Task<IActionResult> TopUsers(int take = 10)
        {
            var users = await _db.Users.AsNoTracking().Where(u => u.IsActive)
                .Select(u => new { u.Id, u.Username, u.DisplayName, pc = u.BlogPosts.Count(p => p.IsPublished), fc = u.Followers.Count, tv = u.BlogPosts.Sum(p => p.ViewCount), u.CreatedAt })
                .OrderByDescending(u => u.fc).Take(take).ToListAsync();
            return Ok(users);
        }

        [HttpGet("views-trend")]
        public async Task<IActionResult> ViewsTrend(int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            var views = await _db.PostViews.Where(v => v.Timestamp >= since)
                .GroupBy(v => v.Timestamp.Date).Select(g => new { date = g.Key, count = g.Count() }).OrderBy(g => g.date).ToListAsync();
            return Ok(views);
        }
    }
}
