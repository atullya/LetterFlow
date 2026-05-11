using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    public class AiJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, StringLength(50)]
        public string Type { get; set; } = string.Empty;

        /// <summary>FK to ApplicationUser.Id — nullable for anonymous/system jobs.</summary>
        public int? OwnerUserId { get; set; }

        [Required]
        public string Input { get; set; } = string.Empty;

        public string? Result { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = AiJobStatus.Pending;

        public int Attempts { get; set; }

        public int MaxAttempts { get; set; } = 5;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public DateTimeOffset? NextAttemptAt { get; set; }

        public string? Error { get; set; }

        [StringLength(100)]
        public string? WorkerId { get; set; }

        // Navigation
        public virtual ApplicationUser? Owner { get; set; }
    }

    public static class AiJobStatus
    {
        public const string Pending    = "Pending";
        public const string InProgress = "InProgress";
        public const string Succeeded  = "Succeeded";
        public const string Failed     = "Failed";
        public const string Cancelled  = "Cancelled";
    }

    public static class AiJobTypes
    {
        public const string Improve       = "Improve";
        public const string Continue      = "Continue";
        public const string Summarize     = "Summarize";
        public const string SuggestTitle  = "SuggestTitle";
        public const string SuggestTags   = "SuggestTags";
        public const string SuggestImages = "SuggestImages";
    }
}
