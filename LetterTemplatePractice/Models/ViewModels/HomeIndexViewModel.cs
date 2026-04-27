namespace LetterTemplatePractice.Models.ViewModels
{
    public class HomeIndexViewModel
    {
        public IReadOnlyList<BlogPost> FeaturedPosts { get; set; } = [];
        public IReadOnlyList<BlogPost> LatestPosts { get; set; } = [];
        public IReadOnlyList<BlogPost> PopularPosts { get; set; } = [];
        public IReadOnlyList<string> Topics { get; set; } = [];
        public int WriterCount { get; set; }
        public int PublishedPostCount { get; set; }
        public int CommentCount { get; set; }
    }
}
