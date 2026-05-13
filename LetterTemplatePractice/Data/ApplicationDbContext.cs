using LetterTemplatePractice.Models;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<Notebook> Notebooks { get; set; }
        public DbSet<BlogComment> BlogComments { get; set; }

        public DbSet<BlogLike> BlogLikes { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AiJob> AiJobs { get; set; }
        public DbSet<Report> Reports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(e =>
            {
                e.HasKey(u => u.Id);
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Username).IsRequired().HasMaxLength(50);
                e.Property(u => u.Email).IsRequired().HasMaxLength(100);
                e.Property(u => u.DisplayName).HasMaxLength(100);
                e.Property(u => u.AvatarUrl).HasMaxLength(500);
                e.Property(u => u.PasswordHash).HasMaxLength(200);
                e.Property(u => u.GoogleId).HasMaxLength(100);
                e.HasIndex(u => u.GoogleId).IsUnique().HasFilter("\"GoogleId\" IS NOT NULL");
                e.Property(u => u.Role).IsRequired().HasMaxLength(20).HasDefaultValue("User");
                e.Property(u => u.IsActive).HasDefaultValue(true);
                e.Property(u => u.IsHiddenProfile).HasDefaultValue(false);
            });

            modelBuilder.Entity<BlogPost>(e =>
            {
                e.HasKey(p => p.Id);
                e.Property(p => p.Title).IsRequired().HasMaxLength(160);
                e.Property(p => p.Subtitle).HasMaxLength(240);
                e.Property(p => p.Slug).IsRequired().HasMaxLength(180);
                e.Property(p => p.Excerpt).HasMaxLength(320);
                e.Property(p => p.CoverImageUrl).HasMaxLength(500);
                e.Property(p => p.Topic).HasMaxLength(200);
                e.Property(p => p.ContentHtml).IsRequired();
                e.HasIndex(p => p.Slug).IsUnique();
                e.HasIndex(p => new { p.IsPublished, p.PublishedAt });
                e.Property(p => p.IsHidden).HasDefaultValue(false);
                e.HasOne(p => p.Author)
                    .WithMany(u => u.BlogPosts)
                    .HasForeignKey(p => p.AuthorId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Notebook)
                    .WithMany(n => n.Blogs)
                    .HasForeignKey(p => p.NotebookId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Notebook>(e =>
            {
                e.HasKey(n => n.Id);
                e.Property(n => n.Name).IsRequired().HasMaxLength(120);
                e.Property(n => n.Description).HasMaxLength(300);
                e.HasIndex(n => new { n.UserId, n.Name }).IsUnique();
                e.HasOne(n => n.User)
                    .WithMany(u => u.Notebooks)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BlogComment>(e =>
            {
                e.HasKey(c => c.Id);
                e.Property(c => c.Content).IsRequired().HasMaxLength(2000);
                e.HasIndex(c => c.CreatedAt);
                e.HasOne(c => c.Post)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(c => c.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Author)
                    .WithMany(u => u.BlogComments)
                    .HasForeignKey(c => c.AuthorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BlogLike>(e =>
            {
                e.HasKey(l => l.Id);
                e.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
                e.HasOne(l => l.Post)
                    .WithMany(p => p.Likes)
                    .HasForeignKey(l => l.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.User)
                    .WithMany(u => u.BlogLikes)
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Follow>(e =>
            {
                e.HasKey(f => f.Id);
                e.HasIndex(f => new { f.FollowerId, f.FollowingId }).IsUnique();
                e.HasOne(f => f.Follower)
                    .WithMany(u => u.Following)
                    .HasForeignKey(f => f.FollowerId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(f => f.Following)
                    .WithMany(u => u.Followers)
                    .HasForeignKey(f => f.FollowingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Notification>(e =>
            {
                e.HasKey(n => n.Id);
                e.Property(n => n.Type).IsRequired().HasMaxLength(40);
                e.HasIndex(n => new { n.RecipientId, n.IsRead });
                e.HasIndex(n => n.CreatedAt);
                e.HasOne(n => n.Recipient)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.RecipientId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(n => n.Actor)
                    .WithMany()
                    .HasForeignKey(n => n.ActorId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(n => n.Post)
                    .WithMany()
                    .HasForeignKey(n => n.PostId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AiJob>(e =>
            {
                e.HasKey(j => j.Id);
                e.Property(j => j.Type).IsRequired().HasMaxLength(50);
                e.Property(j => j.Status).IsRequired().HasMaxLength(20);
                e.Property(j => j.WorkerId).HasMaxLength(100);
                e.HasIndex(j => new { j.Status, j.NextAttemptAt, j.CreatedAt });
                e.HasOne(j => j.Owner).WithMany()
                    .HasForeignKey(j => j.OwnerUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Report>(e =>
            {
                e.HasKey(r => r.Id);
                e.Property(r => r.Reason).HasMaxLength(500);
                e.Property(r => r.Outcome).HasMaxLength(20);
                e.Property(r => r.IsResolved).HasDefaultValue(false);

                // Reporter -> Reports (cascade: deleting reporter removes their reports)
                e.HasOne(r => r.Reporter)
                    .WithMany(u => u.ReportsSubmitted)
                    .HasForeignKey(r => r.ReporterId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Report -> Post (cascade: deleting post removes its reports)
                e.HasOne(r => r.Post)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(r => r.TargetPostId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Report -> TargetUser (cascade: deleting user removes reports against them)
                e.HasOne(r => r.TargetUser)
                    .WithMany(u => u.ReportsReceived)
                    .HasForeignKey(r => r.TargetUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ResolvedBy admin (set null if admin is deleted)
                e.HasOne(r => r.ResolvedBy)
                    .WithMany()
                    .HasForeignKey(r => r.ResolvedById)
                    .OnDelete(DeleteBehavior.SetNull);

                // Unique: one report per reporter per post
                e.HasIndex(r => new { r.ReporterId, r.TargetPostId })
                    .IsUnique()
                    .HasFilter("\"TargetPostId\" IS NOT NULL");

                // Unique: one report per reporter per user
                e.HasIndex(r => new { r.ReporterId, r.TargetUserId })
                    .IsUnique()
                    .HasFilter("\"TargetUserId\" IS NOT NULL");
            });
        }
    }
}
