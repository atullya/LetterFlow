namespace LetterTemplatePractice.Models.ViewModels;

public class FollowListViewModel
{
    public ApplicationUser ProfileUser { get; set; } = null!;
    public IReadOnlyList<ApplicationUser> Users { get; set; } = Array.Empty<ApplicationUser>();
    public IReadOnlyList<int> FollowingIds { get; set; } = Array.Empty<int>();
    public string ActiveTab { get; set; } = "followers"; // "followers" or "following"
}
