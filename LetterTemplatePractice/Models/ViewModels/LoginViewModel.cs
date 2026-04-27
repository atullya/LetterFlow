using System.ComponentModel.DataAnnotations;

namespace LetterTemplatePractice.Models.ViewModels
{
    public sealed class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        /// <summary>URL to redirect to after successful login.</summary>
        public string? ReturnUrl { get; set; }
    }
}
