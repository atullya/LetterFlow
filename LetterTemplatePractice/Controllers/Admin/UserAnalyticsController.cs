using LetterTemplatePractice.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public sealed class UserAnalyticsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public UserAnalyticsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("/Admin/Users/Analytics")]
        public IActionResult Analytics()
        {
            return View();
        }

        [HttpGet("/admin/api/users")]
        public async Task<IActionResult> ApiUsers(
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "posts",
            [FromQuery] string? sortDir = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.Username != null && u.Username.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)));
            }

            var total = await query.CountAsync(ct);

            var users = await query
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    DisplayName = u.DisplayName,
                    u.Email,
                    AvatarUrl = u.AvatarUrl,
                    CreatedAt = u.CreatedAt,
                    PostCount = u.BlogPosts.Count(p => p.IsPublished && !p.IsHidden),
                    TotalViews = u.BlogPosts.Where(p => p.IsPublished && !p.IsHidden).Sum(p => (int?)p.ViewCount) ?? 0,
                    TotalClaps = u.BlogPosts.Where(p => p.IsPublished && !p.IsHidden)
                        .SelectMany(p => p.Likes).Count(),
                    TotalReadTimeMinutes = u.BlogPosts.Where(p => p.IsPublished && !p.IsHidden)
                        .Sum(p => (int?)p.ReadTimeMinutes) ?? 0
                })
                .ToListAsync(ct);

            IEnumerable<dynamic> sorted = sortBy switch
            {
                "name" => sortDir == "asc"
                    ? users.OrderBy(u => (string?)(u.DisplayName ?? u.Username))
                    : users.OrderByDescending(u => (string?)(u.DisplayName ?? u.Username)),
                "joined" => sortDir == "asc"
                    ? users.OrderBy(u => u.CreatedAt)
                    : users.OrderByDescending(u => u.CreatedAt),
                "views" => sortDir == "asc"
                    ? users.OrderBy(u => u.TotalViews)
                    : users.OrderByDescending(u => u.TotalViews),
                "claps" => sortDir == "asc"
                    ? users.OrderBy(u => u.TotalClaps)
                    : users.OrderByDescending(u => u.TotalClaps),
                "readtime" => sortDir == "asc"
                    ? users.OrderBy(u => u.TotalReadTimeMinutes)
                    : users.OrderByDescending(u => u.TotalReadTimeMinutes),
                _ => sortDir == "asc"
                    ? users.OrderBy(u => u.PostCount)
                    : users.OrderByDescending(u => u.PostCount)
            };

            var items = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    id = (int)u.Id,
                    username = (string?)u.Username,
                    displayName = (string?)u.DisplayName,
                    email = (string?)u.Email,
                    avatarUrl = (string?)u.AvatarUrl,
                    dateJoined = ((DateTime)u.CreatedAt).ToString("MMM dd, yyyy"),
                    postCount = (int)u.PostCount,
                    totalViews = (int)u.TotalViews,
                    totalClaps = (int)u.TotalClaps,
                    averageReadTimeMinutes = (int)u.PostCount > 0
                        ? (double)(int)u.TotalReadTimeMinutes / (int)u.PostCount
                        : 0
                })
                .ToList();

            return Ok(new { items, total });
        }

        [HttpGet("/admin/api/users/{userId:int}/analytics")]
        public async Task<IActionResult> ApiUserAnalytics(int userId,
            [FromQuery] string? range = "30d",
            CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null) return NotFound(new { error = "User not found" });

            var days = range switch { "7d" => 7, "90d" => 90, _ => 30 };
            var cutoff = DateTime.UtcNow.AddDays(-days);

            var postIds = await _db.BlogPosts
                .Where(p => p.AuthorId == userId && p.IsPublished && !p.IsHidden)
                .Select(p => p.Id)
                .ToListAsync(ct);

            var totalViews = await _db.BlogPosts
                .Where(p => p.AuthorId == userId && p.IsPublished && !p.IsHidden)
                .SumAsync(p => (int?)p.ViewCount, ct) ?? 0;

            var totalClaps = postIds.Any()
                ? await _db.BlogLikes.Where(l => postIds.Contains(l.PostId)).CountAsync(ct)
                : 0;

            var posts = await _db.BlogPosts
                .Where(p => p.AuthorId == userId && p.IsPublished && !p.IsHidden)
                .Select(p => new { p.Id, p.ReadTimeMinutes })
                .ToListAsync(ct);

            var avgReadTime = posts.Any() ? posts.Average(p => p.ReadTimeMinutes) : 0;

            double avgScrollRate = 0;
            if (postIds.Any())
            {
                var scrollDepths = await _db.PostViews
                    .Where(v => postIds.Contains(v.PostId) && v.ScrollDepthPercent != null)
                    .Select(v => v.ScrollDepthPercent!.Value)
                    .ToListAsync(ct);
                avgScrollRate = scrollDepths.Any() ? scrollDepths.Average() : 0;
            }

            List<dynamic> dailyViewsRaw;
            if (postIds.Any())
            {
                var views = await _db.PostViews
                    .Where(v => postIds.Contains(v.PostId) && v.Timestamp >= cutoff)
                    .GroupBy(v => v.Timestamp.Date)
                    .Select(g => new { date = g.Key, count = g.Count() })
                    .OrderBy(x => x.date)
                    .ToListAsync(ct);
                dailyViewsRaw = views.Select(v => (dynamic)new { date = v.date.ToString("yyyy-MM-dd"), count = v.count }).ToList();
            }
            else
            {
                dailyViewsRaw = new List<dynamic>();
            }

            var publishedPosts = await _db.BlogPosts
                .Where(p => p.AuthorId == userId && p.IsPublished && !p.IsHidden)
                .Select(p => new { p.Id, p.Title })
                .ToListAsync(ct);

            var clapsPerPost = new List<dynamic>();
            foreach (var pp in publishedPosts.OrderByDescending(_ => Guid.NewGuid()))
            {
                var count = await _db.BlogLikes.CountAsync(l => l.PostId == pp.Id, ct);
                clapsPerPost.Add(new { postId = pp.Id, title = pp.Title, claps = count });
            }
            clapsPerPost = clapsPerPost.OrderByDescending(x => x.claps).ToList<dynamic>();

            var readTimeTrend = await _db.BlogPosts
                .Where(p => p.AuthorId == userId && p.IsPublished && !p.IsHidden)
                .OrderBy(p => p.PublishedAt ?? p.CreatedAt)
                .Select(p => new
                {
                    title = (string?)p.Title,
                    readTime = (int)p.ReadTimeMinutes,
                    publishedAt = p.PublishedAt ?? p.CreatedAt
                })
                .ToListAsync(ct);

            var trafficSources = new List<dynamic>();

            var directCount = postIds.Any()
                ? await _db.PostViews.Where(v => postIds.Contains(v.PostId) && v.Timestamp >= cutoff
                    && (v.ReferrerSource == null || v.ReferrerSource == "")).CountAsync(ct)
                : 0;
            trafficSources.Add(new { source = "Direct", count = directCount });

            var searchCount = postIds.Any()
                ? await _db.PostViews.Where(v => postIds.Contains(v.PostId) && v.Timestamp >= cutoff
                    && v.ReferrerSource != null && v.ReferrerSource.ToLower().Contains("search")).CountAsync(ct)
                : 0;
            trafficSources.Add(new { source = "Search", count = searchCount });

            var socialCount = postIds.Any()
                ? await _db.PostViews.Where(v => postIds.Contains(v.PostId) && v.Timestamp >= cutoff
                    && v.ReferrerSource != null && v.ReferrerSource.ToLower().Contains("social")).CountAsync(ct)
                : 0;
            trafficSources.Add(new { source = "Social", count = socialCount });

            var referralCount = postIds.Any()
                ? await _db.PostViews.Where(v => postIds.Contains(v.PostId) && v.Timestamp >= cutoff
                    && v.ReferrerSource != null && v.ReferrerSource.ToLower() != ""
                    && !v.ReferrerSource.ToLower().Contains("search")
                    && !v.ReferrerSource.ToLower().Contains("social")).CountAsync(ct)
                : 0;
            trafficSources.Add(new { source = "Referral", count = referralCount });

            var heatmapData = new List<dynamic>();
            var heatmapStart = DateTime.UtcNow.Date.AddDays(-83);
            for (var d = heatmapStart; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
            {
                var dayViews = postIds.Any()
                    ? await _db.PostViews.Where(v => postIds.Contains(v.PostId)
                        && v.Timestamp >= d && v.Timestamp < d.AddDays(1)).CountAsync(ct)
                    : 0;
                heatmapData.Add(new { date = d.ToString("yyyy-MM-dd"), views = dayViews });
            }

            return Ok(new
            {
                summary = new
                {
                    totalViews,
                    totalClaps,
                    averageReadTimeMinutes = Math.Round(avgReadTime, 1),
                    averageScrollRate = Math.Round(avgScrollRate, 1)
                },
                dailyViews = dailyViewsRaw,
                clapsPerPost,
                readTimeTrend = readTimeTrend.Select(r => new
                {
                    title = r.title,
                    readTime = r.readTime,
                    publishedAt = r.publishedAt.ToString("yyyy-MM-dd")
                }),
                trafficSources,
                heatmapData
            });
        }

        [HttpGet("/admin/api/users/{userId:int}/posts")]
        public async Task<IActionResult> ApiUserPosts(int userId, CancellationToken ct)
        {
            var posts = await _db.BlogPosts
                .Where(p => p.AuthorId == userId && p.IsPublished && !p.IsHidden)
                .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Slug,
                    publishedAt = p.PublishedAt ?? p.CreatedAt,
                    totalViews = p.ViewCount,
                    totalClaps = p.Likes.Count,
                    avgReadTimeMinutes = p.ReadTimeMinutes
                })
                .ToListAsync(ct);

            var result = new List<dynamic>();
            foreach (var p in posts)
            {
                var viewRecords = await _db.PostViews
                    .Where(v => v.PostId == p.Id)
                    .ToListAsync(ct);

                var uniqueVisitors = viewRecords
                    .Where(v => v.UserId != null)
                    .Select(v => v.UserId)
                    .Distinct()
                    .Count();

                var scrollViews = viewRecords
                    .Where(v => v.ScrollDepthPercent != null)
                    .Select(v => v.ScrollDepthPercent!.Value)
                    .ToList();

                var scrollRate = scrollViews.Any() ? scrollViews.Average() : 0;

                result.Add(new
                {
                    id = p.Id,
                    title = p.Title,
                    slug = p.Slug,
                    publishedAt = p.publishedAt.ToString("MMM dd, yyyy"),
                    totalViews = p.totalViews,
                    uniqueVisitors = uniqueVisitors,
                    totalClaps = p.totalClaps,
                    avgReadTimeMinutes = p.avgReadTimeMinutes,
                    scrollCompletionPercent = Math.Round(scrollRate, 1)
                });
            }

            return Ok(result);
        }

        [HttpGet("/admin/api/posts/{postId:int}/heatmap")]
        public async Task<IActionResult> ApiPostHeatmap(int postId, CancellationToken ct)
        {
            var post = await _db.BlogPosts.FindAsync(new object[] { postId }, ct);
            if (post == null) return NotFound(new { error = "Post not found" });

            var views = await _db.PostViews
                .Where(v => v.PostId == postId && v.ScrollDepthPercent != null)
                .Select(v => v.ScrollDepthPercent!.Value)
                .ToListAsync(ct);

            if (!views.Any())
                return Ok(new { title = post.Title, sections = new List<dynamic>() });

            var buckets = new int[10];
            foreach (var depth in views)
            {
                var idx = Math.Min((int)(depth / 10), 9);
                buckets[idx]++;
            }

            var maxBucket = buckets.Max();
            var sections = new List<dynamic>();
            for (var i = 0; i < 10; i++)
            {
                var pct = (double)buckets[i] / views.Count * 100;
                sections.Add(new
                {
                    label = $"{i * 10}–{(i + 1) * 10}%",
                    percentage = Math.Round(pct, 1),
                    intensity = maxBucket > 0 ? (double)buckets[i] / maxBucket : 0
                });
            }

            return Ok(new { title = post.Title, sections });
        }
    }
}
