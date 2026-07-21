using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuthService _auth;

        public AdminUsersController(ApplicationDbContext db, IAuthService auth)
        {
            _db = db;
            _auth = auth;
        }

        private int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(raw, out var id) ? id : 0;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(string? search, string? role, string? status, int page = 1, int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var q = _db.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                q = q.Where(u => u.Username.Contains(term) || u.Email.Contains(term)
                    || (u.DisplayName != null && u.DisplayName.Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(role))
                q = q.Where(u => u.Role == role);
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                q = q.Where(u => u.IsActive);
            else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
                q = q.Where(u => !u.IsActive);

            var total = await q.CountAsync();
            var users = await q.OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new
                {
                    u.Id, u.Username, u.Email, u.DisplayName, u.Role,
                    u.IsActive, u.IsHiddenProfile, u.AvatarUrl, u.CreatedAt, u.LastLoginAt,
                    postCount = u.BlogPosts.Count
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, users });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var u = await _db.Users.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id, x.Username, x.Email, x.DisplayName, x.Role,
                    x.IsActive, x.IsHiddenProfile, x.AvatarUrl, x.CreatedAt, x.LastLoginAt,
                    postCount = x.BlogPosts.Count,
                    followerCount = x.Followers.Count,
                    followingCount = x.Following.Count
                })
                .FirstOrDefaultAsync();

            return u == null ? NotFound() : Ok(u);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
        {
            if (req is null) return BadRequest(new { error = "Request body is required." });

            var (success, error, user) = await _auth.AdminCreateUserAsync(
                req.Username, req.Email, req.Password, req.Role ?? UserRoles.User, req.DisplayName);

            if (!success) return BadRequest(new { error });

            return CreatedAtAction(nameof(GetById), new { id = user!.Id }, new
            {
                user.Id, user.Username, user.Email, user.DisplayName, user.Role, user.IsActive
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
        {
            if (req is null) return BadRequest(new { error = "Request body is required." });

            var existing = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (existing is null) return NotFound();

            var username = string.IsNullOrWhiteSpace(req.Username) ? existing.Username : req.Username;
            var email = string.IsNullOrWhiteSpace(req.Email) ? existing.Email : req.Email;
            var displayName = req.DisplayName ?? existing.DisplayName;
            var role = string.IsNullOrWhiteSpace(req.Role) ? existing.Role : req.Role;
            var isActive = req.IsActive ?? existing.IsActive;
            var isHidden = req.IsHiddenProfile ?? existing.IsHiddenProfile;

            if (id == CurrentUserId && (role != UserRoles.Admin || !isActive))
                return BadRequest(new { error = "You cannot demote or deactivate your own admin account." });

            var (success, error) = await _auth.AdminUpdateUserAsync(
                id, username, email, displayName, role, isActive, isHidden);

            if (!success) return BadRequest(new { error });

            return Ok(new { id, username, email, displayName, role, isActive, isHiddenProfile = isHidden });
        }

        [HttpPost("{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { error = "NewPassword is required." });

            var (success, error) = await _auth.AdminResetPasswordAsync(id, req.NewPassword);
            if (!success) return BadRequest(new { error });
            return Ok(new { message = "Password reset successfully." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, error) = await _auth.AdminDeleteUserAsync(id, CurrentUserId);
            if (!success) return BadRequest(new { error });
            return Ok(new { message = "User deleted successfully." });
        }
    }

    public sealed class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Role { get; set; }
    }

    public sealed class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsHiddenProfile { get; set; }
    }

    public sealed class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }
}
