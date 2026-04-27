using System.Text;
using System.Text.RegularExpressions;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Services
{
    public class BlogService
    {
        private readonly ApplicationDbContext _context;

        public BlogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<BlogPost> PublishedPostsQuery()
        {
            return _context.BlogPosts
                .AsNoTracking()
                .Include(post => post.Author)
                .Include(post => post.Comments)
                .Include(post => post.Likes)
                .Where(post => post.IsPublished)
                .OrderByDescending(post => post.PublishedAt ?? post.UpdatedAt);
        }

        public async Task<string> GenerateUniqueSlugAsync(string title, int? currentPostId = null)
        {
            var baseSlug = Slugify(title);
            if (string.IsNullOrWhiteSpace(baseSlug))
            {
                baseSlug = $"story-{DateTime.UtcNow:yyyyMMddHHmmss}";
            }

            var slug = baseSlug;
            var suffix = 2;

            while (await _context.BlogPosts.AnyAsync(post => post.Slug == slug && post.Id != currentPostId))
            {
                slug = $"{baseSlug}-{suffix}";
                suffix++;
            }

            return slug;
        }

        public int EstimateReadTimeMinutes(string html)
        {
            var plainText = Regex.Replace(html ?? string.Empty, "<.*?>", " ");
            var wordCount = Regex.Matches(plainText, @"\b\w+\b").Count;
            return Math.Max(1, (int)Math.Ceiling(wordCount / 220d));
        }

        public string BuildExcerpt(BlogComposerViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Excerpt))
            {
                return model.Excerpt.Trim();
            }

            var plainText = Regex.Replace(model.ContentHtml ?? string.Empty, "<.*?>", " ");
            plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

            if (plainText.Length <= 180)
            {
                return plainText;
            }

            return plainText[..177].TrimEnd() + "...";
        }

        private static string Slugify(string value)
        {
            value = value.ToLowerInvariant().Trim();
            var builder = new StringBuilder();
            var previousWasDash = false;

            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    previousWasDash = false;
                }
                else if (!previousWasDash)
                {
                    builder.Append('-');
                    previousWasDash = true;
                }
            }

            return builder.ToString().Trim('-');
        }
    }
}
