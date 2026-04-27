using System.Diagnostics;
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
}
