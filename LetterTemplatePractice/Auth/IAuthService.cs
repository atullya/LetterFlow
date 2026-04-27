using LetterTemplatePractice.Models;

namespace LetterTemplatePractice.Auth
{
    public sealed record LoginResult(bool Success, string? Error, ApplicationUser? User);
    public sealed record RegisterResult(bool Success, string? Error);

    /// <summary>
    /// Contract for all authentication operations.
    /// </summary>
    public interface IAuthService
    {
        Task<LoginResult>    LoginAsync(string username, string password);
        Task<RegisterResult> RegisterAsync(string username, string email, string password, string role = UserRoles.User);
        Task<ApplicationUser?> GetUserByIdAsync(int id);
        Task<ApplicationUser?> GetUserByUsernameAsync(string username);
        Task<bool>           UsernameExistsAsync(string username);
        Task<bool>           EmailExistsAsync(string email);
        Task<(bool Success, string? Error)> UpdateProfileAsync(int userId, string username, string email, string? displayName);
        Task<(bool Success, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task<(bool Success, string? Error)> UpdateAvatarAsync(int userId, string avatarUrl);
    }
}
