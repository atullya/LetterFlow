using System.Security.Claims;
using LetterTemplatePractice.Data;
using Logging;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Models.ViewModels;
using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers
{
    [Authorize]
    public class BlogController : Controller
    {
        private const string Category = nameof(BlogController);

        private readonly ApplicationDbContext _context;
        private readonly BlogService _blogService;
        private readonly IAppLogger _logger;
        

        public BlogController(ApplicationDbContext context, BlogService blogService, IAppLogger logger)
        {
            _context = context;
            _blogService = blogService;
            _logger = logger;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Explore(string? topic = null, string? search = null)
        {
            var query = _blogService.PublishedPostsQuery();
            _logger.LogError("BlogController", "Testing error log in Explore action.");

            if (!string.IsNullOrWhiteSpace(topic))
            {
                query = query.Where(post => post.Topic == topic);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(post =>
                    post.Title.Contains(term) ||
                    (post.Subtitle != null && post.Subtitle.Contains(term)) ||
                    (post.Excerpt != null && post.Excerpt.Contains(term)) ||
                    (post.Topic != null && post.Topic.Contains(term)));
            }

            ViewBag.Topic = topic;
            ViewBag.Search = search;
            ViewBag.Topics = await _context.BlogPosts
                .AsNoTracking()
                .Where(post => post.IsPublished && post.Topic != null && post.Topic != string.Empty)
                .Select(post => post.Topic!)
                .Distinct()
                .OrderBy(topicName => topicName)
                .ToListAsync();

            return View(await query.ToListAsync());
        }

        [AllowAnonymous]
        [HttpGet("stories/{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            var post = await _context.BlogPosts
                .Include(item => item.Author)
                .Include(item => item.Comments)
                    .ThenInclude(comment => comment.Author)
                .Include(item => item.Likes)
                .FirstOrDefaultAsync(item => item.Slug == slug);

            if (post == null)
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var isOwner = userId.HasValue && post.AuthorId == userId.Value;

            if (!post.IsPublished && !isOwner && !User.IsInRole(UserRoles.Admin))
            {
                return NotFound();
            }

            post.ViewCount += 1;
            await _context.SaveChangesAsync();

            var relatedPosts = await _blogService.PublishedPostsQuery()
                .Where(item => item.Id != post.Id && (item.Topic == post.Topic || item.AuthorId == post.AuthorId))
                .Take(3)
                .ToListAsync();

            var model = new BlogDetailsViewModel
            {
                Post = post,
                Comments = post.Comments.OrderByDescending(comment => comment.CreatedAt).ToList(),
                RelatedPosts = relatedPosts,
                IsOwner = isOwner || User.IsInRole(UserRoles.Admin),
                IsLikedByCurrentUser = userId.HasValue && post.Likes.Any(like => like.UserId == userId.Value),
                CanComment = User.Identity?.IsAuthenticated == true
            };

            return View(model);
        }

        public async Task<IActionResult> Mine()
        {
            var userId = GetRequiredUserId();
            var posts = await _context.BlogPosts
                .AsNoTracking()
                .Include(post => post.Comments)
                .Include(post => post.Likes)
                .Where(post => post.AuthorId == userId)
                .OrderByDescending(post => post.UpdatedAt)
                .ToListAsync();

            return View(posts);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View("Editor", new BlogComposerViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogComposerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Editor", model);
            }

            var userId = GetRequiredUserId();
            var isPublished = string.Equals(model.SubmitAction, "publish", StringComparison.OrdinalIgnoreCase) || model.IsPublished;
            var slug = await _blogService.GenerateUniqueSlugAsync(model.Title);

            var post = new BlogPost
            {
                AuthorId = userId,
                Title = model.Title.Trim(),
                Subtitle = model.Subtitle?.Trim(),
                Slug = slug,
                Excerpt = _blogService.BuildExcerpt(model),
                CoverImageUrl = model.CoverImageUrl?.Trim(),
                Topic = model.Topic?.Trim(),
                ContentHtml = model.ContentHtml,
                IsPublished = isPublished,
                IsFeatured = model.IsFeatured && User.IsInRole(UserRoles.Admin),
                PublishedAt = isPublished ? DateTime.UtcNow : null,
                ReadTimeMinutes = _blogService.EstimateReadTimeMinutes(model.ContentHtml),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation(Category, $"Blog post '{post.Title}' created.", "/Blog/Create", userId.ToString());

            return RedirectToAction(nameof(Details), new { slug = post.Slug });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            if (!CanManagePost(post))
            {
                return Forbid();
            }

            return View("Editor", new BlogComposerViewModel
            {
                Id = post.Id,
                Title = post.Title,
                Subtitle = post.Subtitle,
                Excerpt = post.Excerpt,
                CoverImageUrl = post.CoverImageUrl,
                Topic = post.Topic,
                ContentHtml = post.ContentHtml,
                IsPublished = post.IsPublished,
                IsFeatured = post.IsFeatured
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BlogComposerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Id = id;
                return View("Editor", model);
            }

            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            if (!CanManagePost(post))
            {
                return Forbid();
            }

            var shouldPublish = string.Equals(model.SubmitAction, "publish", StringComparison.OrdinalIgnoreCase) || model.IsPublished;

            post.Title = model.Title.Trim();
            post.Subtitle = model.Subtitle?.Trim();
            post.Excerpt = _blogService.BuildExcerpt(model);
            post.CoverImageUrl = model.CoverImageUrl?.Trim();
            post.Topic = model.Topic?.Trim();
            post.ContentHtml = model.ContentHtml;
            post.IsFeatured = model.IsFeatured && User.IsInRole(UserRoles.Admin);
            post.IsPublished = shouldPublish;
            post.PublishedAt = shouldPublish ? post.PublishedAt ?? DateTime.UtcNow : null;
            post.ReadTimeMinutes = _blogService.EstimateReadTimeMinutes(model.ContentHtml);
            post.Slug = await _blogService.GenerateUniqueSlugAsync(post.Title, post.Id);
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(Category, $"Blog post '{post.Title}' updated.", "/Blog/Edit", post.AuthorId.ToString());

            return RedirectToAction(nameof(Details), new { slug = post.Slug });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            if (!CanManagePost(post))
            {
                return Forbid();
            }

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Mine));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int id)
        {
            var userId = GetRequiredUserId();
            var post = await _context.BlogPosts
                .Include(item => item.Likes)
                .FirstOrDefaultAsync(item => item.Id == id && item.IsPublished);

            if (post == null)
            {
                return NotFound();
            }

            var existingLike = post.Likes.FirstOrDefault(like => like.UserId == userId);
            var liked = false;

            if (existingLike == null)
            {
                _context.BlogLikes.Add(new BlogLike
                {
                    PostId = id,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
                liked = true;
            }
            else
            {
                _context.BlogLikes.Remove(existingLike);
            }

            await _context.SaveChangesAsync();

            var count = await _context.BlogLikes.CountAsync(like => like.PostId == id);
            return Json(new { liked, count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string content)
        {
            var userId = GetRequiredUserId();
            var post = await _context.BlogPosts.FirstOrDefaultAsync(item => item.Id == id && item.IsPublished);
            if (post == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction(nameof(Details), new { slug = post.Slug });
            }

            _context.BlogComments.Add(new BlogComment
            {
                PostId = id,
                AuthorId = userId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { slug = post.Slug });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.BlogComments
                .Include(item => item.Post)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (comment == null || comment.Post == null)
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var canDelete = userId == comment.AuthorId || userId == comment.Post.AuthorId || User.IsInRole(UserRoles.Admin);

            if (!canDelete)
            {
                return Forbid();
            }

            _context.BlogComments.Remove(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { slug = comment.Post.Slug });
        }

        private int GetRequiredUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private int? GetCurrentUserId()
        {
            var rawValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(rawValue, out var id) ? id : null;
        }

        private bool CanManagePost(BlogPost post)
        {
            var userId = GetCurrentUserId();
            return userId == post.AuthorId || User.IsInRole(UserRoles.Admin);
        }
    }
}
