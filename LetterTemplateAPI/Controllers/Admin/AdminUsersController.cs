using LetterTemplatePractice.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminUsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public AdminUsersController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll(string? search, string? role, int page = 1, int pageSize = 20)
        {
            var q = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.Username.Contains(search) || u.Email.Contains(search) || (u.DisplayName != null && u.DisplayName.Contains(search)));
            if (!string.IsNullOrWhiteSpace(role)) q = q.Where(u => u.Role == role);
            var total = await q.CountAsync();
            var users = await q.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new { u.Id, u.Username, u.Email, u.DisplayName, u.Role, u.IsActive, u.IsHiddenProfile, u.CreatedAt, pc = u.BlogPosts.Count }).ToListAsync();
            return Ok(new { total, page, pageSize, users });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var u = await _db.Users.AsNoTracking().Select(x => new { x.Id, x.Username, x.Email, x.DisplayName, x.Role, x.IsActive, x.IsHiddenProfile, x.AvatarUrl, x.CreatedAt, x.LastLoginAt, pc = x.BlogPosts.Count, fc = x.Followers.Count, fgc = x.Following.Count }).FirstOrDefaultAsync(x => x.Id == id);
            return u == null ? NotFound() : Ok(u);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(req.Role)) u.Role = req.Role;
            if (req.IsActive.HasValue) u.IsActive = req.IsActive.Value;
            if (req.IsHiddenProfile.HasValue) u.IsHiddenProfile = req.IsHiddenProfile.Value;
            u.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { u.Id, u.Username, u.Role, u.IsActive, u.IsHiddenProfile });
        }
    }

    public sealed class UpdateUserRequest { public string? Role { get; set; } public bool? IsActive { get; set; } public bool? IsHiddenProfile { get; set; } }
}
