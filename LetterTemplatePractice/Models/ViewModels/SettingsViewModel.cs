using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models.ViewModels
{
    public class ProfileSettingsViewModel
    {
        [StringLength(100)]
        [Display(Name = "Display Name")]
        public string? DisplayName { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Only letters, numbers and underscores allowed.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        public string? CurrentAvatarUrl { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
