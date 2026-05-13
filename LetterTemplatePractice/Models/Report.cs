using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    /// <summary>
    /// Represents a user-submitted report against a post or another user.
    /// Exactly one of TargetPostId / TargetUserId will be set (enforced in application logic).
    /// </summary>
    public class Report
    {
        public int Id { get; set; }

        // ── Reporter ──────────────────────────────────────────────────────────
        public int ReporterId { get; set; }
        public virtual ApplicationUser? Reporter { get; set; }

        // ── Target (post OR user — one will be null) ──────────────────────────
        public int? TargetPostId { get; set; }
        public virtual BlogPost? Post { get; set; }

        public int? TargetUserId { get; set; }
        public virtual ApplicationUser? TargetUser { get; set; }

        // ── Reason ────────────────────────────────────────────────────────────
        [StringLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Admin resolution ──────────────────────────────────────────────────
        public bool IsResolved { get; set; } = false;

        /// <summary>"dismissed" | "confirmed" | null (unresolved)</summary>
        [StringLength(20)]
        public string? Outcome { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public int? ResolvedById { get; set; }
        public virtual ApplicationUser? ResolvedBy { get; set; }
    }
}
