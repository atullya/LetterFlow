using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    /// <summary>
    /// Tracks which users have opted in to the daily news digest email.
    /// Kept as a separate table so we can also support non-registered subscribers later.
    /// </summary>
    public class NewsletterSubscription
    {
        public int Id { get; set; }

        /// <summary>Null for guest/external subscribers; set for registered users.</summary>
        public int? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        /// <summary>The email address to send to (copied from user at subscribe time).</summary>
        [Required, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UnsubscribedAt { get; set; }

        /// <summary>Opaque token used in unsubscribe links.</summary>
        [Required, StringLength(64)]
        public string UnsubscribeToken { get; set; } = Guid.NewGuid().ToString("N");
    }
}
