using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers
{
    [ApiController]
    [Route("api/sitemap")]
    public sealed class SitemapController : ControllerBase
    {
        private readonly BlogService _bs;
        public SitemapController(BlogService bs) => _bs = bs;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var stories = await _bs.PublishedPostsQuery()
                .Select(p => new { p.Slug, p.Title, p.UpdatedAt, p.IsFeatured })
                .ToListAsync();

            return Ok(new
            {
                baseUrl = "https://letterflow-ps3k.onrender.com",
                pages = new[] { "/", "/Blog/Explore", "/Home/Privacy" },
                stories = stories.Select(s => new { path = $"/stories/{s.Slug}", s.Title, s.UpdatedAt, priority = s.IsFeatured ? "0.9" : "0.8" })
            });
        }
    }
}
