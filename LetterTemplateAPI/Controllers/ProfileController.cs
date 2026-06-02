using System.Security.Claims;
using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public sealed class ProfileController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ProfileController(IAuthService auth, ApplicationDbContext db, IWebHostEnvironment env) { _auth = auth; _db = db; _env = env; }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await _auth.GetUserByIdAsync(GetUserId());
            if (user == null) return NotFound();
            var isSubscribed = await _db.NewsletterSubscriptions.AnyAsync(s => s.UserId == user.Id && s.IsActive);
            return Ok(new { user.Id, user.Username, user.Email, user.DisplayName, user.AvatarUrl, user.Role, user.CreatedAt, isNewsletterSubscribed = isSubscribed });
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { error = "Username and email are required." });
            var (ok, err) = await _auth.UpdateProfileAsync(GetUserId(), req.Username, req.Email, req.DisplayName);
            if (!ok) return Conflict(new { error = err });
            return Ok(new { message = "Profile updated." });
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { error = "Current and new password are required." });
            var (ok, err) = await _auth.ChangePasswordAsync(GetUserId(), req.CurrentPassword, req.NewPassword);
            if (!ok) return BadRequest(new { error = err });
            return Ok(new { message = "Password changed." });
        }

        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { error = "File is required." });
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
                return BadRequest(new { error = "Only JPG, PNG, GIF, or WebP allowed." });
            if (file.Length > 2 * 1024 * 1024) return BadRequest(new { error = "Max 2 MB." });
            var dir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(dir);
            var fn = $"{GetUserId()}_{Guid.NewGuid():N}{ext}";
            var fp = Path.Combine(dir, fn);
            await using var s = System.IO.File.Create(fp);
            await file.CopyToAsync(s);
            var url = $"/uploads/avatars/{fn}";
            await _auth.UpdateAvatarAsync(GetUserId(), url);
            return Ok(new { avatarUrl = url });
        }

        [HttpPost("newsletter/toggle")]
        public async Task<IActionResult> ToggleNewsletter()
        {
            var uid = GetUserId();
            var user = await _auth.GetUserByIdAsync(uid);
            if (user == null) return NotFound();
            var sub = await _db.NewsletterSubscriptions.FirstOrDefaultAsync(s => s.UserId == uid);
            if (sub == null)
            {
                _db.NewsletterSubscriptions.Add(new NewsletterSubscription { UserId = uid, Email = user.Email, IsActive = true, SubscribedAt = DateTime.UtcNow, UnsubscribeToken = Guid.NewGuid().ToString("N") });
                return Ok(new { subscribed = true });
            }
            sub.IsActive = !sub.IsActive;
            if (sub.IsActive) { sub.Email = user.Email; sub.UnsubscribedAt = null; }
            else sub.UnsubscribedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { subscribed = sub.IsActive });
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications(int page = 1, int pageSize = 20)
        {
            var uid = GetUserId();
            var total = await _db.Notifications.CountAsync(n => n.RecipientId == uid);
            var items = await _db.Notifications.Where(n => n.RecipientId == uid).OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(n => new { n.Id, n.Type, n.PostId, n.IsRead, n.CreatedAt, Actor = new { n.Actor!.Id, n.Actor.Username, n.Actor.AvatarUrl } }).ToListAsync();
            return Ok(new { total, page, pageSize, notifications = items });
        }

        [HttpPost("notifications/{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var uid = GetUserId();
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == uid);
            if (n == null) return NotFound();
            n.IsRead = true;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("notifications/read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var uid = GetUserId();
            await _db.Notifications.Where(n => n.RecipientId == uid && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            return Ok();
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    public sealed class UpdateProfileRequest { public string Username { get; set; } = ""; public string Email { get; set; } = ""; public string? DisplayName { get; set; } }
    public sealed class ChangePasswordRequest { public string CurrentPassword { get; set; } = ""; public string NewPassword { get; set; } = ""; }
}
