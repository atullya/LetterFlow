using System.Security.Claims;
using LetterTemplatePractice.Auth;
using Logging;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetterTemplatePractice.Controllers
{
    [Authorize]
    public sealed class SettingsController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IAppLogger   _logger;
        private readonly IWebHostEnvironment _env;

        public SettingsController(IAuthService authService, IAppLogger logger, IWebHostEnvironment env)
        {
            _authService = authService;
            _logger      = logger;
            _env         = env;
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

            var vm = new ProfileSettingsViewModel
            {
                Username       = user.Username,
                Email          = user.Email,
                DisplayName    = user.DisplayName,
                CurrentAvatarUrl = user.AvatarUrl
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
