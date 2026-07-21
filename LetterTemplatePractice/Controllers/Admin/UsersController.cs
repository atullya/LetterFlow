using System.Security.Claims;
using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Users")]
    public sealed class UsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuthService _auth;

        public UsersController(ApplicationDbContext db, IAuthService auth)
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

        // GET /Admin/Users
        // Note: /Admin/Users/Analytics is owned by UserAnalyticsController.
        [HttpGet("")]
        public async Task<IActionResult> Index(
            string? search = null,
            string? role = null,
            string? status = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term) ||
                    (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(role) &&
                (role == UserRoles.Admin || role == UserRoles.User))
            {
                query = query.Where(u => u.Role == role);
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive);
            else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => !u.IsActive);

            var total = await query.CountAsync(ct);

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserListItemViewModel
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    IsHiddenProfile = u.IsHiddenProfile,
                    AvatarUrl = u.AvatarUrl,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                    PostCount = u.BlogPosts.Count
                })
                .ToListAsync(ct);

            var stats = await _db.Users.AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Active = g.Count(u => u.IsActive),
                    Inactive = g.Count(u => !u.IsActive),
                    Admins = g.Count(u => u.Role == UserRoles.Admin)
                })
                .FirstOrDefaultAsync(ct);

            var vm = new AdminUsersIndexViewModel
            {
                Users = users,
                Total = total,
                Page = page,
                PageSize = pageSize,
                Search = search,
                Role = role,
                Status = status,
                ActiveCount = stats?.Active ?? 0,
                InactiveCount = stats?.Inactive ?? 0,
                AdminCount = stats?.Admins ?? 0
            };

            ViewData["Title"] = "Users";
            ViewData["MetaDescription"] = "Manage LetterFlow Stories users, roles, and accounts.";
            ViewBag.CurrentUserId = CurrentUserId;
            return View(vm);
        }

        // POST /Admin/Users/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminCreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = string.Join(" ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Take(3));
                return RedirectToAction(nameof(Index));
            }

            var (success, error, _) = await _auth.AdminCreateUserAsync(
                model.Username, model.Email, model.Password, model.Role, model.DisplayName);

            TempData["ToastType"] = success ? "success" : "error";
            TempData["ToastMessage"] = success
                ? $"User '{model.Username}' created successfully."
                : (error ?? "Failed to create user.");

            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Users/Edit
        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminEditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = string.Join(" ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Take(3));
                return RedirectToAction(nameof(Index));
            }

            // Prevent self-demotion / self-deactivation without a safety net
            if (model.Id == CurrentUserId &&
                (model.Role != UserRoles.Admin || !model.IsActive))
            {
                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = "You cannot demote or deactivate your own admin account.";
                return RedirectToAction(nameof(Index));
            }

            var (success, error) = await _auth.AdminUpdateUserAsync(
                model.Id, model.Username, model.Email, model.DisplayName,
                model.Role, model.IsActive, model.IsHiddenProfile);

            TempData["ToastType"] = success ? "success" : "error";
            TempData["ToastMessage"] = success
                ? $"User '{model.Username}' updated successfully."
                : (error ?? "Failed to update user.");

            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Users/ResetPassword
        [HttpPost("ResetPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = string.Join(" ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Take(3));
                return RedirectToAction(nameof(Index));
            }

            var (success, error) = await _auth.AdminResetPasswordAsync(model.Id, model.NewPassword);

            TempData["ToastType"] = success ? "success" : "error";
            TempData["ToastMessage"] = success
                ? $"Password reset for '{model.Username}'."
                : (error ?? "Failed to reset password.");

            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Users/Delete
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, error) = await _auth.AdminDeleteUserAsync(id, CurrentUserId);

            TempData["ToastType"] = success ? "success" : "error";
            TempData["ToastMessage"] = success
                ? "User deleted successfully."
                : (error ?? "Failed to delete user.");

            return RedirectToAction(nameof(Index));
        }
    }
}
