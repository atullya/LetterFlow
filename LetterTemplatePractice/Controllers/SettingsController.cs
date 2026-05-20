using System.Security.Claims;
using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Logging;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers
{
    [Authorize]
    public sealed class SettingsController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IAppLogger   _logger;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public SettingsController(IAuthService authService, IAppLogger logger, IWebHostEnvironment env, ApplicationDbContext db)
        {
            _authService = authService;
            _logger      = logger;
            _env         = env;
            _db          = db;
        }

        private int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(raw, out var id))
                    return id;

                throw new InvalidOperationException(
                    $"The current user's NameIdentifier claim ('{raw}') is not a valid database user ID. " +
                    "This typically means an external login was not completed. Please sign out and log in again.");
            }
        }

        // GET /Settings
        public async Task<IActionResult> Index()
        {
            var user = await _authService.GetUserByIdAsync(CurrentUserId);
            if (user is null) return NotFound();

            var isSubscribed = await _db.NewsletterSubscriptions
                .AnyAsync(s => s.UserId == CurrentUserId && s.IsActive);

            var vm = new ProfileSettingsViewModel
            {
                Username              = user.Username,
                Email                 = user.Email,
                DisplayName           = user.DisplayName,
                CurrentAvatarUrl      = user.AvatarUrl,
                IsNewsletterSubscribed = isSubscribed
            };
            return View(vm);
        }

        // POST /Settings/Profile
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileSettingsViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var user = await _authService.GetUserByIdAsync(CurrentUserId);
                vm.CurrentAvatarUrl = user?.AvatarUrl;
                return View("Index", vm);
            }

            var (success, error) = await _authService.UpdateProfileAsync(
                CurrentUserId, vm.Username, vm.Email, vm.DisplayName);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error!);
                var user = await _authService.GetUserByIdAsync(CurrentUserId);
                vm.CurrentAvatarUrl = user?.AvatarUrl;
                return View("Index", vm);
            }

            // Re-issue cookie with updated username claim
            await RefreshClaimsAsync(vm.Username, vm.Email);

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/ChangePassword
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["PasswordError"] = "Please fix the errors below.";
                return RedirectToAction(nameof(Index));
            }

            var (success, error) = await _authService.ChangePasswordAsync(
                CurrentUserId, vm.CurrentPassword, vm.NewPassword);

            if (!success)
            {
                TempData["PasswordError"] = error;
                return RedirectToAction(nameof(Index));
            }

            TempData["PasswordSuccess"] = "Password changed successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/UploadAvatar
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            if (avatar is null || avatar.Length == 0)
            {
                TempData["AvatarError"] = "Please select an image file.";
                return RedirectToAction(nameof(Index));
            }

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                TempData["AvatarError"] = "Only JPG, PNG, GIF or WebP images are allowed.";
                return RedirectToAction(nameof(Index));
            }

            if (avatar.Length > 2 * 1024 * 1024)
            {
                TempData["AvatarError"] = "Image must be under 2 MB.";
                return RedirectToAction(nameof(Index));
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{CurrentUserId}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using (var stream = System.IO.File.Create(filePath))
                await avatar.CopyToAsync(stream);

            var avatarUrl = $"/uploads/avatars/{fileName}";
            var (success, error) = await _authService.UpdateAvatarAsync(CurrentUserId, avatarUrl);

            if (!success)
            {
                TempData["AvatarError"] = error;
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Profile photo updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Settings/ToggleNewsletter
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleNewsletter(CancellationToken ct)
        {
            var user = await _authService.GetUserByIdAsync(CurrentUserId);
            if (user is null) return NotFound();

            var existing = await _db.NewsletterSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == CurrentUserId, ct);

            if (existing == null)
            {
                // First time subscribing — create a new row
                _db.NewsletterSubscriptions.Add(new NewsletterSubscription
                {
                    UserId           = CurrentUserId,
                    Email            = user.Email,
                    IsActive         = true,
                    SubscribedAt     = DateTime.UtcNow,
                    UnsubscribeToken = Guid.NewGuid().ToString("N")
                });
                TempData["NewsletterSuccess"] = "You're subscribed to the daily news digest.";
            }
            else if (existing.IsActive)
            {
                // Currently subscribed → unsubscribe
                existing.IsActive       = false;
                existing.UnsubscribedAt = DateTime.UtcNow;
                TempData["NewsletterSuccess"] = "You've been unsubscribed from the daily digest.";
            }
            else
            {
                // Previously unsubscribed → re-subscribe, refresh email in case it changed
                existing.IsActive       = true;
                existing.Email          = user.Email;
                existing.UnsubscribedAt = null;
                TempData["NewsletterSuccess"] = "You're re-subscribed to the daily news digest.";
            }

            await _db.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index), new { tab = "notifications" });
        }

        private async Task RefreshClaimsAsync(string username, string email)
        {
            var user = await _authService.GetUserByIdAsync(CurrentUserId);
            if (user is null) return;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           username),
                new(ClaimTypes.Email,          email),
                new(ClaimTypes.Role,           user.Role)
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });
        }
    }
}
