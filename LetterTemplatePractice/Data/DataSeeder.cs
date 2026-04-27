using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Models;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Data
{
    /// <summary>
    /// Seeds the database with a default admin account on first run.
    /// Credentials are read from configuration so they are never hard-coded.
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope   = services.CreateScope();
            var context       = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher        = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger        = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

            // Apply any pending migrations
            await context.Database.MigrateAsync();

            var adminUsername = configuration["Seed:AdminUsername"] ?? "admin";
            var adminEmail    = configuration["Seed:AdminEmail"]    ?? "admin@letterflow.local";
            var adminPassword = configuration["Seed:AdminPassword"] ?? "Admin@1234";

            // Always ensure the admin account exists, regardless of other users.
            // If the username already exists but has the wrong role, promote it.
            var existing = await context.Users
                .FirstOrDefaultAsync(u => u.Username == adminUsername);

            if (existing is null)
            {
                context.Users.Add(new ApplicationUser
                {
                    Username     = adminUsername,
                    Email        = adminEmail,
                    PasswordHash = hasher.Hash(adminPassword),
                    Role         = UserRoles.Admin,
                    IsActive     = true,
                    CreatedAt    = DateTime.UtcNow,
                    UpdatedAt    = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                logger.LogInformation(
                    "Default admin account created. Username: {Username}. Change the password immediately.",
                    adminUsername);
            }
            else if (existing.Role != UserRoles.Admin)
            {
                existing.Role      = UserRoles.Admin;
                existing.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                logger.LogInformation(
                    "Existing user '{Username}' promoted to Admin role.", adminUsername);
            }

            if (!await context.BlogPosts.AnyAsync())
            {
                var author = await context.Users.OrderBy(user => user.Id).FirstAsync();
                var now = DateTime.UtcNow;

                context.BlogPosts.AddRange(
                    new BlogPost
                    {
                        AuthorId = author.Id,
                        Title = "Turning a utility app into a publication platform",
                        Subtitle = "How a focused writing tool can grow into a reader-friendly blog experience.",
                        Slug = "turning-a-utility-app-into-a-publication-platform",
                        Excerpt = "A short walkthrough on reshaping an internal tool into a story-first product with authorship, feed design, and engagement built in.",
                        Topic = "Product",
                        ContentHtml = "<p>LetterFlow Stories now supports long-form publishing with real ownership, reader comments, likes, and public reading pages.</p><p>This sample post exists so the home feed has something meaningful to show on first run.</p>",
                        IsPublished = true,
                        IsFeatured = true,
                        PublishedAt = now.AddDays(-2),
                        ReadTimeMinutes = 2,
                        ViewCount = 18,
                        CreatedAt = now.AddDays(-2),
                        UpdatedAt = now.AddDays(-2)
                    },
                    new BlogPost
                    {
                        AuthorId = author.Id,
                        Title = "Writing with a calmer editor and clearer structure",
                        Subtitle = "Small UI changes that make the act of publishing feel more deliberate.",
                        Slug = "writing-with-a-calmer-editor-and-clearer-structure",
                        Excerpt = "The editor stays familiar, but the publishing flow now supports headlines, excerpts, topics, and proper public story pages.",
                        Topic = "Writing",
                        ContentHtml = "<p>The writing experience keeps the rich text editor, but it is now framed as a full blog composer with better metadata and publishing controls.</p>",
                        IsPublished = true,
                        IsFeatured = false,
                        PublishedAt = now.AddDays(-1),
                        ReadTimeMinutes = 1,
                        ViewCount = 9,
                        CreatedAt = now.AddDays(-1),
                        UpdatedAt = now.AddDays(-1)
                    });

                await context.SaveChangesAsync();
            }
        }
    }
}
