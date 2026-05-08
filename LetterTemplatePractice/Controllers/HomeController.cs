using System.Diagnostics;
using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Models.ViewModels;
using LetterTemplatePractice.Services;
using Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly BlogService _blogService;
    private readonly IAppLogger _logger;

    public HomeController(ApplicationDbContext context, BlogService blogService,IAppLogger logger)
    {
        _context = context;
        _blogService = blogService;
        _logger = logger;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        _logger.LogError("HomeController", "Index page requested");

        var publishedQuery = _blogService.PublishedPostsQuery();

        HomeIndexViewModel model;

        try
        {
            model = new HomeIndexViewModel
            {
                FeaturedPosts = await publishedQuery
                    .Where(post => post.IsFeatured)
                    .Take(2)
                    .ToListAsync(),
                LatestPosts = await publishedQuery
                    .Take(8)
                    .ToListAsync(),
                PopularPosts = await publishedQuery
                    .OrderByDescending(post => post.ViewCount)
                    .ThenByDescending(post => post.PublishedAt)
                    .Take(4)
                    .ToListAsync(),
                Topics = await _context.BlogPosts
                    .AsNoTracking()
                    .Where(post => post.IsPublished && post.Topic != null && post.Topic != string.Empty)
                    .Select(post => post.Topic!)
                    .Distinct()
                    .OrderBy(topic => topic)
                    .Take(8)
                    .ToListAsync(),
                WriterCount = await _context.Users.CountAsync(),
                PublishedPostCount = await _context.BlogPosts.CountAsync(post => post.IsPublished),
                CommentCount = await _context.BlogComments.CountAsync()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("HomeController", "Failed to load home page data", ex);
            throw;
        }

        if (!model.FeaturedPosts.Any())
        {
            _logger.LogWarning("HomeController", "No featured posts found, falling back to latest posts");
            model.FeaturedPosts = model.LatestPosts.Take(2).ToList();
        }

        _logger.LogInformation("HomeController",
            $"Home page loaded — {model.PublishedPostCount} posts, {model.WriterCount} writers, {model.CommentCount} comments");

        return View(model);
    }


    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // ────────────────────────── Notifications API ──────────────────────────

    [Authorize]
    [HttpGet("Notifications/Unread")]
    public async Task<IActionResult> UnreadNotifications()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out var userId)) return Unauthorized();

        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.IsRead,
                n.CreatedAt,
                n.PostId,
                Actor = new
                {
                    n.Actor.Id,
                    n.Actor.Username,
                    n.Actor.DisplayName,
                    n.Actor.AvatarUrl
                },
                Post = n.Post != null ? new
                {
                    n.Post.Title,
                    n.Post.Slug
                } : null
            })
            .ToListAsync();

        var unreadCount = notifications.Count(n => !n.IsRead);

        return Json(new { notifications, unreadCount });
    }

    [Authorize]
    [HttpPost("Notifications/MarkRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationsRead()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out var userId)) return Unauthorized();

        var unread = await _context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread) n.IsRead = true;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }
}
