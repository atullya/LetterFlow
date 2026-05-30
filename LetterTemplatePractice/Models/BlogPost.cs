using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    public class BlogPost
    {
        public int Id { get; set; }

        public int AuthorId { get; set; }
        public virtual ApplicationUser? Author { get; set; }

        public int? NotebookId { get; set; }
        public virtual Notebook? Notebook { get; set; }

        [Required]
        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(240)]
        public string? Subtitle { get; set; }

        [Required]
        [StringLength(180)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(320)]
        public string? Excerpt { get; set; }

        [StringLength(500)]
        public string? CoverImageUrl { get; set; }

        [StringLength(200)]
        public string? Topic { get; set; }

        [Required]
        public string ContentHtml { get; set; } = string.Empty;

        public bool IsPublished { get; set; }

        public bool IsFeatured { get; set; }

        public DateTime? PublishedAt { get; set; }

        public DateTime? ScheduledAt { get; set; }

        public int ReadTimeMinutes { get; set; } = 1;

        public int ViewCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsHidden { get; set; } = false;

        public virtual ICollection<BlogComment> Comments { get; set; } = new List<BlogComment>();

        public virtual ICollection<BlogLike> Likes { get; set; } = new List<BlogLike>();

        public virtual ICollection<Report> Reports { get; set; } = new List<Report>();
    }
}
