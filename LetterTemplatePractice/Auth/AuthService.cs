using LetterTemplatePractice.Data;
using Logging;
using LetterTemplatePractice.Models;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Auth
{
    /// <summary>
    /// Handles user registration, credential validation, and user lookup.
    /// Registered as Scoped — one instance per HTTP request.
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private const string Category = nameof(AuthService);

        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher      _hasher;
        private readonly IAppLogger           _logger;

        public AuthService(
            ApplicationDbContext context,
            IPasswordHasher      hasher,
            IAppLogger           logger)
        {
            _context = context;
            _hasher  = hasher;
            _logger  = logger;
        }

        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return new LoginResult(false, "Username and password are required.", null);

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user is null || !user.IsActive)
            {
                _logger.LogWarning(Category, $"Failed login attempt for username: {username}");
                return new LoginResult(false, "Invalid username or password.", null);
            }

            if (user.PasswordHash is null || !_hasher.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning(Category, $"Invalid password for username: {username}");
                return new LoginResult(false, "Invalid username or password.", null);
            }

            // Update last login timestamp
            await _context.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, DateTime.UtcNow));

            _logger.LogInformation(Category, $"User '{username}' logged in successfully.");
            return new LoginResult(true, null, user);
        }

        public async Task<RegisterResult> RegisterAsync(
            string username, string email, string password, string role = UserRoles.User)
        {
            if (string.IsNullOrWhiteSpace(username))
                return new RegisterResult(false, "Username is required.");

            if (string.IsNullOrWhiteSpace(email))
                return new RegisterResult(false, "Email is required.");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return new RegisterResult(false, "Password must be at least 8 characters.");

            if (await UsernameExistsAsync(username))
                return new RegisterResult(false, "Username is already taken.");

            if (await EmailExistsAsync(email))
                return new RegisterResult(false, "Email is already registered.");

            var user = new ApplicationUser
            {
                Username     = username.Trim(),
                Email        = email.Trim().ToLowerInvariant(),
                PasswordHash = _hasher.Hash(password),
                Role         = role,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation(Category, $"New user registered: '{username}' with role '{role}'.");
            return new RegisterResult(true, null);
        }

        public Task<ApplicationUser?> GetUserByIdAsync(int id)
            => _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

        public Task<ApplicationUser?> GetUserByUsernameAsync(string username)
            => _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);

        public Task<bool> UsernameExistsAsync(string username)
            => _context.Users.AnyAsync(u => u.Username == username);

        public Task<bool> EmailExistsAsync(string email)
            => _context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());

        public async Task<(bool Success, string? Error)> UpdateProfileAsync(
            int userId, string username, string email, string? displayName)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user is null) return (false, "User not found.");

            if (user.Username != username && await _context.Users.AnyAsync(u => u.Username == username && u.Id != userId))
                return (false, "Username is already taken.");

            if (user.Email != email.ToLowerInvariant() && await _context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant() && u.Id != userId))
                return (false, "Email is already registered.");

            user.Username    = username.Trim();
            user.Email       = email.Trim().ToLowerInvariant();
            user.DisplayName = displayName?.Trim();
            user.UpdatedAt   = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation(Category, $"User '{username}' updated their profile.");
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ChangePasswordAsync(
            int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user is null) return (false, "User not found.");

            if (user.PasswordHash is null || !_hasher.Verify(currentPassword, user.PasswordHash))
                return (false, "Current password is incorrect.");

            user.PasswordHash = _hasher.Hash(newPassword);
            user.UpdatedAt    = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation(Category, $"User '{user.Username}' changed their password.");
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UpdateAvatarAsync(int userId, string avatarUrl)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user is null) return (false, "User not found.");

            user.AvatarUrl = avatarUrl;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<ApplicationUser?> FindOrCreateGoogleUserAsync(
            string googleId, string email, string? displayName, string? avatarUrl)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
            if (user is not null)
            {
                if (!user.IsActive) return null;

                user.LastLoginAt = DateTime.UtcNow;
                if (avatarUrl is not null) user.AvatarUrl = avatarUrl;
                if (displayName is not null && user.DisplayName is null) user.DisplayName = displayName;
                await _context.SaveChangesAsync();
                _logger.LogInformation(Category, $"Google user '{user.Username}' logged in.");
                return user;
            }

            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
            if (user is not null)
            {
                if (!user.IsActive) return null;

                user.GoogleId   = googleId;
                user.LastLoginAt = DateTime.UtcNow;
                if (avatarUrl is not null) user.AvatarUrl = avatarUrl;
                await _context.SaveChangesAsync();
                _logger.LogInformation(Category, $"Linked Google account to existing user '{user.Username}'.");
                return user;
            }

            var baseUsername = email.Split('@')[0];
            var username = baseUsername;
            var suffix = 1;
            while (await _context.Users.AnyAsync(u => u.Username == username))
                username = $"{baseUsername}{suffix++}";

            var newUser = new ApplicationUser
            {
                Username     = username,
                Email        = email.ToLowerInvariant(),
                DisplayName  = displayName ?? username,
                AvatarUrl    = avatarUrl,
                GoogleId     = googleId,
                PasswordHash = null,
                Role         = UserRoles.User,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow,
                LastLoginAt  = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation(Category, $"New Google user registered: '{username}'.");
            return newUser;
        }
    }
}
