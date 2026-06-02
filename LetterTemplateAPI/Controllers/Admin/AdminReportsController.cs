using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/reports")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public AdminReportsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll(bool? resolved, int page = 1, int pageSize = 20)
        {
            var q = _db.Reports.AsQueryable();
            if (resolved.HasValue) q = q.Where(r => r.IsResolved == resolved.Value);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new { r.Id, r.ReporterId, r.TargetPostId, r.TargetUserId, r.Reason, r.IsResolved, r.Outcome, r.CreatedAt }).ToListAsync();
            return Ok(new { total, page, pageSize, reports = items });
        }

        [HttpPut("{id}/resolve")]
        public async Task<IActionResult> Resolve(int id, [FromBody] ResolveRequest req)
        {
            var r = await _db.Reports.FindAsync(id);
            if (r == null) return NotFound();
            r.IsResolved = true;
            r.Outcome = req.Outcome;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Report resolved." });
        }

        [HttpGet("users")]
        public async Task<IActionResult> ReportedUsers()
        {
            var items = await _db.Reports.Where(r => r.TargetUserId != null && !r.IsResolved)
                .GroupBy(r => r.TargetUserId!).Select(g => new { userId = g.Key, unresolvedCount = g.Count() })
                .OrderByDescending(x => x.unresolvedCount).Take(50).ToListAsync();
            return Ok(items);
        }
    }

    public sealed class ResolveRequest { public string? Outcome { get; set; } }
}
