using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers
{
    [ApiController]
    [Route("api/notebooks")]
    [Authorize]
    public sealed class NotebooksController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public NotebooksController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll() { var u = U(); return Ok(await _db.Notebooks.Where(n => n.UserId == u).Select(n => R(n)).ToListAsync()); }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id) { var u = U(); var r = await _db.Notebooks.Where(n => n.Id == id && n.UserId == u).Select(n => R(n)).FirstOrDefaultAsync(); return r == null ? NotFound() : Ok(r); }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NotebookRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
            var u = U(); var nm = req.Name.Trim();
            if (await _db.Notebooks.AnyAsync(n => n.UserId == u && n.Name == nm)) return Conflict(new { error = "Already exists." });
            var nb = new Notebook { UserId = u, Name = nm, Description = D(req.Description), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _db.Notebooks.Add(nb); await _db.SaveChangesAsync();
            return Created($"/api/notebooks/{nb.Id}", new { nb.Id, nb.Name, nb.Description, blogCount = 0, nb.CreatedAt, nb.UpdatedAt });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] NotebookRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Name is required." });
            var u = U(); var nb = await _db.Notebooks.FirstOrDefaultAsync(n => n.Id == id && n.UserId == u);
            if (nb == null) return NotFound();
            var nm = req.Name.Trim();
            if (await _db.Notebooks.AnyAsync(n => n.UserId == u && n.Name == nm && n.Id != id)) return Conflict(new { error = "Already exists." });
            nb.Name = nm; nb.Description = D(req.Description); nb.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(R(nb));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var u = U(); var nb = await _db.Notebooks.FirstOrDefaultAsync(n => n.Id == id && n.UserId == u);
            if (nb == null) return NotFound();
            if (await _db.BlogPosts.AnyAsync(bp => bp.NotebookId == id)) return Conflict(new { error = "Notebook has posts." });
            _db.Notebooks.Remove(nb); await _db.SaveChangesAsync();
            return NoContent();
        }

        private int U() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private static string? D(string? s) => string.IsNullOrWhiteSpace(s?.Trim()) ? null : s.Trim();
        private static object R(Notebook n) => new { n.Id, n.Name, n.Description, blogCount = n.Blogs.Count, n.CreatedAt, n.UpdatedAt };
    }

    public sealed class NotebookRequest { public string Name { get; set; } = ""; public string? Description { get; set; } }
}
