using System.Security.Claims;
using LetterTemplatePractice.Auth;
using Logging;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetterTemplatePractice.Controllers
{
    /// <summary>
    /// Handles login, logout and registration.
    /// All actions are intentionally [AllowAnonymous] — the rest of the app is locked down.
    /// </summary>
    [AllowAnonymous]
    public sealed class AccountController : Controller
    {
        private const string Category = nameof(AccountController);

        private readonly IAuthService _authService;
        private readonly IAppLogger _logger;

        public AccountController(IAuthService authService, IAppLogger logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // ── GET /Account/Login ────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToLocal(returnUrl);

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // ── POST /Account/Login ───────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _authService.LoginAsync(model.Username, model.Password);

            if (!result.Success)
            {
                _logger.LogWarning(Category,
                    $"Failed login for '{model.Username}' from {HttpContext.Connection.RemoteIpAddress}",
                    requestPath: "/Account/Login");

                ModelState.AddModelError(string.Empty, result.Error ?? "Login failed.");
                return View(model);
            }

            var user = result.User!;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           user.Username),
                new(ClaimTypes.Email,          user.Email),
                new(ClaimTypes.Role,           user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProps);

            _logger.LogInformation(Category,
                $"User '{user.Username}' authenticated successfully.",
                requestPath: "/Account/Login",
                userId: user.Id.ToString());

            TempData["SuccessMessage"] = $"Welcome back, {user.Username}! You have signed in successfully.";
            return RedirectToLocal(model.ReturnUrl);
        }

        // ── GET /Account/Register ─────────────────────────────────────────────
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new RegisterViewModel());
        }

        // ── POST /Account/Register ────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _authService.RegisterAsync(
                model.Username, model.Email, model.Password);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Registration failed.");
                return View(model);
            }

            _logger.LogInformation(Category,
                $"New account registered for '{model.Username}'.",
                requestPath: "/Account/Register");

            TempData["SuccessMessage"] = "Account created successfully. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        // ── POST /Account/Logout ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var username = User.Identity?.Name ?? "unknown";
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation(Category,
                $"User '{username}' logged out.",
                requestPath: "/Account/Logout",
                userId: User.FindFirstValue(ClaimTypes.NameIdentifier));

            return RedirectToAction(nameof(Login));
        }

        // ── GET /Account/GoogleLogin ──────────────────────────────────────────
        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(ExternalLoginCallback), new { returnUrl })
            };
            return Challenge(properties, "Google");
        }

        // ── GET /Account/ExternalLoginCallback ────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("External");

            if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
            {
                _logger.LogWarning(Category, "External login callback failed — no principal.",
                    requestPath: "/Account/ExternalLoginCallback");
                TempData["ErrorMessage"] = "Google login failed. Please try again.";
                return RedirectToAction(nameof(Login));
            }

            var googleId = authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email);
            var displayName = authenticateResult.Principal.FindFirstValue(ClaimTypes.Name);
            var avatarUrl = authenticateResult.Principal.FindFirstValue("urn:google:picture");

            await HttpContext.SignOutAsync("External");

            if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning(Category, "Google response missing required claims.",
                    requestPath: "/Account/ExternalLoginCallback");
                TempData["ErrorMessage"] = "Could not retrieve your Google account info.";
                return RedirectToAction(nameof(Login));
            }

            var user = await _authService.FindOrCreateGoogleUserAsync(googleId, email, displayName, avatarUrl);
            if (user is null)
            {
                _logger.LogWarning(Category, $"Google account disabled for email: {email}",
                    requestPath: "/Account/ExternalLoginCallback");
                TempData["ErrorMessage"] = "This account has been disabled.";
                return RedirectToAction(nameof(Login));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           user.Username),
                new(ClaimTypes.Email,          user.Email),
                new(ClaimTypes.Role,           user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

            _logger.LogInformation(Category,
                $"User '{user.Username}' authenticated via Google.",
                requestPath: "/Account/ExternalLoginCallback",
                userId: user.Id.ToString());

            TempData["SuccessMessage"] = $"Welcome, {user.DisplayName ?? user.Username}! You have signed in successfully.";
            return RedirectToLocal(returnUrl);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}
