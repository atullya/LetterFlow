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

            if (!string.IsNullOrWhiteSpace(topic))
                query = query.Where(post => post.Topic == topic);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(post =>
                    post.Title.Contains(term) ||
                    (post.Subtitle != null && post.Subtitle.Contains(term)) ||
                    (post.Excerpt  != null && post.Excerpt.Contains(term))  ||
                    (post.Topic    != null && post.Topic.Contains(term)));
            }

            ViewBag.Topic  = topic;
            ViewBag.Search = search;
            ViewBag.Topics = await _context.BlogPosts
                .AsNoTracking()
                .Where(post => post.IsPublished && post.Topic != null && post.Topic != string.Empty)
                .Select(post => post.Topic!)
                .Distinct()
                .OrderBy(topicName => topicName)
                .ToListAsync();

            var posts = await query.ToListAsync();

            // AJAX partial — return only the story list HTML
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_ExploreResults", posts);

            return View(posts);
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

        public async Task<IActionResult> Mine(int? notebookId = null)
        {
            var userId = GetRequiredUserId();
            var posts = await _context.BlogPosts
                .AsNoTracking()
                .Include(post => post.Notebook)
                .Include(post => post.Comments)
                .Include(post => post.Likes)
                .Where(post => post.AuthorId == userId)
                .OrderByDescending(post => post.UpdatedAt)
                .ToListAsync();

            var notebooks = await _context.Notebooks
                .AsNoTracking()
                .Where(notebook => notebook.UserId == userId)
                .OrderBy(notebook => notebook.Name)
                .ToListAsync();

            var selectedNotebook = notebookId.HasValue
                ? notebooks.FirstOrDefault(notebook => notebook.Id == notebookId.Value)
                : null;

            ViewBag.Notebooks = notebooks;
            ViewBag.SelectedNotebook = selectedNotebook;
            ViewBag.SelectedNotebookId = selectedNotebook?.Id;
            ViewBag.AssignablePosts = selectedNotebook == null
                ? new List<BlogPost>()
                : posts.Where(post => post.NotebookId != selectedNotebook.Id)
                    .OrderByDescending(post => post.UpdatedAt)
                    .ToList();

            return View(posts);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateNotebookOptionsAsync();
            return View("Editor", new BlogComposerViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogComposerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateNotebookOptionsAsync();
                return View("Editor", model);
            }

            var userId = GetRequiredUserId();
            var isPublished = string.Equals(model.SubmitAction, "publish", StringComparison.OrdinalIgnoreCase) || model.IsPublished;
            var slug = await _blogService.GenerateUniqueSlugAsync(model.Title);
            var notebookId = await ResolveNotebookIdAsync(model.NotebookId, userId);

            var post = new BlogPost
            {
                AuthorId = userId,
                NotebookId = notebookId,
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

            await PopulateNotebookOptionsAsync(post.AuthorId);

            return View("Editor", new BlogComposerViewModel
            {
                Id = post.Id,
                NotebookId = post.NotebookId,
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
                await PopulateNotebookOptionsAsync();
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

            post.NotebookId = await ResolveNotebookIdAsync(model.NotebookId, post.AuthorId);
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
        public async Task<IActionResult> Delete(int id, int? returnNotebookId = null)
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

            return RedirectToAction(nameof(Mine), returnNotebookId.HasValue ? new { notebookId = returnNotebookId } : null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNotebook(string? name, string? description)
        {
            var userId = GetRequiredUserId();
            name = name?.Trim() ?? string.Empty;
            description = description?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["NotebookError"] = "Notebook name is required.";
                return RedirectToAction(nameof(Mine));
            }

            var exists = await _context.Notebooks
                .AnyAsync(notebook => notebook.UserId == userId && notebook.Name == name);

            if (exists)
            {
                TempData["NotebookError"] = "You already have a notebook with this name.";
                return RedirectToAction(nameof(Mine));
            }

            _context.Notebooks.Add(new Notebook
            {
                UserId = userId,
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["NotebookMessage"] = "Notebook created.";

            return RedirectToAction(nameof(Mine));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotebook(int id, string? name, string? description)
        {
            var userId = GetRequiredUserId();
            var notebook = await _context.Notebooks
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            if (notebook == null)
            {
                return NotFound();
            }

            name = name?.Trim() ?? string.Empty;
            description = description?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["NotebookError"] = "Notebook name is required.";
                return RedirectToAction(nameof(Mine), new { notebookId = id });
            }

            var nameExists = await _context.Notebooks
                .AnyAsync(item => item.UserId == userId && item.Id != id && item.Name == name);

            if (nameExists)
            {
                TempData["NotebookError"] = "You already have a notebook with this name.";
                return RedirectToAction(nameof(Mine), new { notebookId = id });
            }

            notebook.Name = name;
            notebook.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            notebook.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["NotebookMessage"] = "Notebook updated.";

            return RedirectToAction(nameof(Mine), new { notebookId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotebook(int id, bool deleteBlogs = false)
        {
            var userId = GetRequiredUserId();
            var notebook = await _context.Notebooks
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            if (notebook == null)
            {
                return NotFound();
            }

            if (deleteBlogs)
            {
                var posts = await _context.BlogPosts
                    .Where(post => post.AuthorId == userId && post.NotebookId == id)
                    .ToListAsync();

                _context.BlogPosts.RemoveRange(posts);
            }

            _context.Notebooks.Remove(notebook);
            await _context.SaveChangesAsync();

            TempData["NotebookMessage"] = deleteBlogs
                ? "Notebook and its stories were deleted."
                : "Notebook deleted. Its stories were kept.";

            return RedirectToAction(nameof(Mine));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBlogToNotebook(int notebookId, int postId)
        {
            var userId = GetRequiredUserId();
            var notebookExists = await _context.Notebooks
                .AnyAsync(notebook => notebook.Id == notebookId && notebook.UserId == userId);

            if (!notebookExists)
            {
                return NotFound();
            }

            var post = await _context.BlogPosts
                .FirstOrDefaultAsync(item => item.Id == postId && item.AuthorId == userId);

            if (post == null)
            {
                return NotFound();
            }

            post.NotebookId = notebookId;
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["NotebookMessage"] = "Story added to notebook.";

            return RedirectToAction(nameof(Mine), new { notebookId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveBlogFromNotebook(int postId, int notebookId)
        {
            var userId = GetRequiredUserId();
            var post = await _context.BlogPosts
                .FirstOrDefaultAsync(item => item.Id == postId && item.AuthorId == userId && item.NotebookId == notebookId);

            if (post == null)
            {
                return NotFound();
            }

            post.NotebookId = null;
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["NotebookMessage"] = "Story removed from notebook.";

            return RedirectToAction(nameof(Mine), new { notebookId });
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

        // ────────────────────────────── Profile ──────────────────────────────

        [AllowAnonymous]
        [HttpGet("@{username}")]
        public async Task<IActionResult> Profile(string username)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return NotFound();
            }

            var currentUserId = GetCurrentUserId();

            var posts = await _context.BlogPosts
                .AsNoTracking()
                .Include(p => p.Author)
                .Include(p => p.Comments)
                .Include(p => p.Likes)
                .Where(p => p.AuthorId == user.Id && p.IsPublished)
                .OrderByDescending(p => p.PublishedAt)
                .ToListAsync();

            var followerCount = await _context.Follows
                .CountAsync(f => f.FollowingId == user.Id);

            var followingCount = await _context.Follows
                .CountAsync(f => f.FollowerId == user.Id);

            var isFollowing = currentUserId.HasValue
                && await _context.Follows
                    .AnyAsync(f => f.FollowerId == currentUserId.Value && f.FollowingId == user.Id);

            var model = new ProfileViewModel
            {
                User = user,
                Posts = posts,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                IsFollowing = isFollowing,
                IsOwnProfile = currentUserId == user.Id
            };

            return View(model);
        }

        [HttpPost("Blog/ToggleFollow/{userId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(int userId)
        {
            var currentUserId = GetRequiredUserId();

            if (currentUserId == userId)
            {
                return BadRequest(new { error = "You cannot follow yourself." });
            }

            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null)
            {
                return NotFound();
            }

            var existingFollow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            bool following;
            if (existingFollow == null)
            {
                _context.Follows.Add(new Follow
                {
                    FollowerId = currentUserId,
                    FollowingId = userId,
                    CreatedAt = DateTime.UtcNow
                });

                // Create notification for the followed user
                _context.Notifications.Add(new Notification
                {
                    RecipientId = userId,
                    ActorId = currentUserId,
                    Type = "follow",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                following = true;
            }
            else
            {
                _context.Follows.Remove(existingFollow);
                following = false;
            }

            await _context.SaveChangesAsync();

            var followerCount = await _context.Follows.CountAsync(f => f.FollowingId == userId);

            _logger.LogInformation(Category,
                $"User {currentUserId} {(following ? "followed" : "unfollowed")} user {userId}.",
                "/Blog/ToggleFollow", currentUserId.ToString());

            return Json(new { following, followerCount });
        }

        [AllowAnonymous]
        [HttpGet("Blog/Followers/{username}")]
        public async Task<IActionResult> Followers(string username)
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var currentUserId = GetCurrentUserId();

            var followers = await _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowingId == user.Id)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Follower)
                .ToListAsync();

            var followingIds = currentUserId.HasValue
                ? await _context.Follows
                    .Where(f => f.FollowerId == currentUserId.Value)
                    .Select(f => f.FollowingId)
                    .ToListAsync()
                : new List<int>();

            return View("FollowList", new FollowListViewModel
            {
                ProfileUser = user,
                Users = followers,
                FollowingIds = followingIds,
                ActiveTab = "followers"
            });
        }

        [AllowAnonymous]
        [HttpGet("Blog/Following/{username}")]
        public async Task<IActionResult> Following(string username)
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var currentUserId = GetCurrentUserId();

            var following = await _context.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == user.Id)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Following)
                .ToListAsync();

            var followingIds = currentUserId.HasValue
                ? await _context.Follows
                    .Where(f => f.FollowerId == currentUserId.Value)
                    .Select(f => f.FollowingId)
                    .ToListAsync()
                : new List<int>();

            return View("FollowList", new FollowListViewModel
            {
                ProfileUser = user,
                Users = following,
                FollowingIds = followingIds,
                ActiveTab = "following"
            });
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

        private async Task PopulateNotebookOptionsAsync(int? ownerId = null)
        {
            var userId = ownerId ?? GetRequiredUserId();
            ViewBag.NotebookOptions = await _context.Notebooks
                .AsNoTracking()
                .Where(notebook => notebook.UserId == userId)
                .OrderBy(notebook => notebook.Name)
                .ToListAsync();
        }

        private async Task<int?> ResolveNotebookIdAsync(int? notebookId, int userId)
        {
            if (!notebookId.HasValue)
            {
                return null;
            }

            var exists = await _context.Notebooks
                .AnyAsync(notebook => notebook.Id == notebookId.Value && notebook.UserId == userId);

            return exists ? notebookId : null;
        }
    }
}
