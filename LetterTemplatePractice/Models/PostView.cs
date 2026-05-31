using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    public class PostView
    {
        public long Id { get; set; }

        public int PostId { get; set; }
        public virtual BlogPost? Post { get; set; }

        public int? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        [StringLength(64)]
        public string? SessionId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public int? ScrollDepthPercent { get; set; }

        public int? TimeOnPageSeconds { get; set; }

        [StringLength(40)]
        public string? ReferrerSource { get; set; }
    }
}
