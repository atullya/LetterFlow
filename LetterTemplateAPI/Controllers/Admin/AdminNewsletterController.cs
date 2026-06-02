using LetterTemplatePractice.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/newsletter")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminNewsletterController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public AdminNewsletterController(ApplicationDbContext db) => _db = db;

        [HttpGet("subscriptions")]
        public async Task<IActionResult> Subscriptions(int page = 1, int pageSize = 20)
        {
            var total = await _db.NewsletterSubscriptions.CountAsync();
            var items = await _db.NewsletterSubscriptions.OrderByDescending(s => s.SubscribedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new { s.Id, s.UserId, s.Email, s.IsActive, s.SubscribedAt, s.UnsubscribedAt }).ToListAsync();
            return Ok(new { total, page, pageSize, subscriptions = items });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var total = await _db.NewsletterSubscriptions.CountAsync();
            var active = await _db.NewsletterSubscriptions.CountAsync(s => s.IsActive);
            return Ok(new { total, active, unsubscribed = total - active });
        }
    }
}
