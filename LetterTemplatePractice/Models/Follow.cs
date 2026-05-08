using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetterTemplatePractice.Models;

public class Follow
{
    public int Id { get; set; }

    public int FollowerId { get; set; }
    [ForeignKey(nameof(FollowerId))]
    public virtual ApplicationUser Follower { get; set; } = null!;

    public int FollowingId { get; set; }
    [ForeignKey(nameof(FollowingId))]
    public virtual ApplicationUser Following { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
