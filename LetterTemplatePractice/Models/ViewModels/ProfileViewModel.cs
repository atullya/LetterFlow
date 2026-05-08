namespace LetterTemplatePractice.Models.ViewModels;

public class ProfileViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public IReadOnlyList<BlogPost> Posts { get; set; } = Array.Empty<BlogPost>();
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsOwnProfile { get; set; }
}
