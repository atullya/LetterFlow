using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    public class BlogComment
    {
        public int Id { get; set; }

        public int PostId { get; set; }
        public virtual BlogPost? Post { get; set; }

        public int AuthorId { get; set; }
        public virtual ApplicationUser? Author { get; set; }

        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
