using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetterTemplatePractice.Models;

public class Notification
{
    public int Id { get; set; }

    /// <summary>Who sees this notification.</summary>
    public int RecipientId { get; set; }
    [ForeignKey(nameof(RecipientId))]
    public virtual ApplicationUser Recipient { get; set; } = null!;

    /// <summary>Who triggered the notification.</summary>
    public int ActorId { get; set; }
    [ForeignKey(nameof(ActorId))]
    public virtual ApplicationUser Actor { get; set; } = null!;

    /// <summary>Notification type: "follow", "new_post", "like", "comment".</summary>
    [MaxLength(40)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Optional related post.</summary>
    public int? PostId { get; set; }
    [ForeignKey(nameof(PostId))]
    public virtual BlogPost? Post { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
