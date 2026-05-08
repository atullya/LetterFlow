using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models
{
    /// <summary>
    /// Represents an authenticated system user.
    /// Passwords are stored as BCrypt hashes — never plain text.
    /// </summary>
    public class ApplicationUser
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>BCrypt hash of the user's password. Null for OAuth-only users.</summary>
        public string? PasswordHash { get; set; }

        [StringLength(100)]
        public string? GoogleId { get; set; }

        [StringLength(20)]
        public string Role { get; set; } = UserRoles.User;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        public virtual ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
        public virtual ICollection<BlogComment> BlogComments { get; set; } = new List<BlogComment>();
        public virtual ICollection<BlogLike> BlogLikes { get; set; } = new List<BlogLike>();

        /// <summary>Users who follow this user.</summary>
        public virtual ICollection<Follow> Followers { get; set; } = new List<Follow>();

        /// <summary>Users this user follows.</summary>
        public virtual ICollection<Follow> Following { get; set; } = new List<Follow>();

        /// <summary>Notifications received by this user.</summary>
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    /// <summary>Role constants — avoids magic strings throughout the codebase.</summary>
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }
}
