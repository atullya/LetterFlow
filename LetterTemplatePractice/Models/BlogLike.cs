namespace LetterTemplatePractice.Models
{
    public class BlogLike
    {
        public int Id { get; set; }

        public int PostId { get; set; }
        public virtual BlogPost? Post { get; set; }

        public int UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
