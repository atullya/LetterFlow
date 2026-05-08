using LetterTemplatePractice.Models;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<BlogPost>        BlogPosts { get; set; }
        public DbSet<BlogComment>     BlogComments { get; set; }
        public DbSet<BlogLike>        BlogLikes { get; set; }
        public DbSet<Follow>          Follows { get; set; }
        public DbSet<Notification>    Notifications { get; set; }

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
                e.HasOne(p => p.Author)
                    .WithMany(u => u.BlogPosts)
                    .HasForeignKey(p => p.AuthorId)
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
        }
    }
}
