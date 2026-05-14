using System.Text;
using System.Xml;
using LetterTemplatePractice.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers;

public class SitemapController : Controller
{
    private const string BaseUrl = "https://letterflow-ps3k.onrender.com";

    private readonly BlogService _blogService;

    public SitemapController(BlogService blogService)
    {
        _blogService = blogService;
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        var urls = new List<SitemapUrl>
        {
            new("/", DateTime.UtcNow, "daily", "1.0"),
            new("/Blog/Explore", DateTime.UtcNow, "daily", "0.9"),
            new("/Home/Privacy", DateTime.UtcNow, "monthly", "0.5")
        };

        var storyUrls = await _blogService.PublishedPostsQuery()
            .Select(post => new SitemapUrl(
                $"/stories/{post.Slug}",
                post.UpdatedAt,
                "weekly",
                post.IsFeatured ? "0.9" : "0.8"))
            .ToListAsync();

        urls.AddRange(storyUrls);

        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            foreach (var url in urls)
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", $"{BaseUrl}{url.Path}");
                writer.WriteElementString("lastmod", url.LastModified.ToUniversalTime().ToString("yyyy-MM-dd"));
                writer.WriteElementString("changefreq", url.ChangeFrequency);
                writer.WriteElementString("priority", url.Priority);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Content(Encoding.UTF8.GetString(stream.ToArray()), "application/xml", Encoding.UTF8);
    }

    private sealed record SitemapUrl(string Path, DateTime LastModified, string ChangeFrequency, string Priority);
}
