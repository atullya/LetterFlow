namespace LetterTemplatePractice.Models.ViewModels
{
    public class BlogDetailsViewModel
    {
        public BlogPost Post { get; set; } = null!;
        public IReadOnlyList<BlogComment> Comments { get; set; } = [];
        public IReadOnlyList<BlogPost> RelatedPosts { get; set; } = [];
        public bool IsOwner { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
        public bool CanComment { get; set; }
    }
}
