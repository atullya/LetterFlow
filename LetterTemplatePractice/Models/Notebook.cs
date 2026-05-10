using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    public class Notebook
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<BlogPost> Blogs { get; set; } = new List<BlogPost>();
    }
}
