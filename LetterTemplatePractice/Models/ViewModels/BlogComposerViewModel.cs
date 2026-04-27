using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models.ViewModels
{
    public class BlogComposerViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [StringLength(240)]
        public string? Subtitle { get; set; }

        [StringLength(320)]
        public string? Excerpt { get; set; }

        [StringLength(500)]
        [Url]
        public string? CoverImageUrl { get; set; }

        [StringLength(200)]
        public string? Topic { get; set; }

        [Required]
        public string ContentHtml { get; set; } = string.Empty;

        public bool IsPublished { get; set; }

        public bool IsFeatured { get; set; }

        public string SubmitAction { get; set; } = "draft";
    }
}
