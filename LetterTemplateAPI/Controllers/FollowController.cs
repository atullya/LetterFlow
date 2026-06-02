using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers
{
    [ApiController]
    [Route("api/follow")]
    [Authorize]
    public sealed class FollowController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public FollowController(ApplicationDbContext db) => _db = db;

        [HttpGet("followers")]
        public async Task<IActionResult> Followers(string username, int page = 1, int pageSize = 20)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();
            var total = await _db.Follows.CountAsync(f => f.FollowingId == user.Id);
            var items = await _db.Follows.Where(f => f.FollowingId == user.Id).OrderByDescending(f => f.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(f => new { f.Follower!.Id, f.Follower.Username, f.Follower.DisplayName, f.Follower.AvatarUrl, f.CreatedAt }).ToListAsync();
            return Ok(new { total, page, pageSize, followers = items });
        }

        [HttpGet("following")]
        public async Task<IActionResult> Following(string username, int page = 1, int pageSize = 20)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();
            var total = await _db.Follows.CountAsync(f => f.FollowerId == user.Id);
            var items = await _db.Follows.Where(f => f.FollowerId == user.Id).OrderByDescending(f => f.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(f => new { f.Following!.Id, f.Following.Username, f.Following.DisplayName, f.Following.AvatarUrl, f.CreatedAt }).ToListAsync();
            return Ok(new { total, page, pageSize, following = items });
        }
    }
}
