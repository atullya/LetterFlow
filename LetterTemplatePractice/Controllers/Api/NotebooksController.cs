using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplatePractice.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/notebooks")]
    public sealed class NotebooksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotebooksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST /api/notebooks
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNotebookRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userId = GetRequiredUserId();
            var name = request.Name.Trim();
            var description = request.Description?.Trim();

            var nameExists = await _context.Notebooks
                .AnyAsync(notebook => notebook.UserId == userId && notebook.Name == name);

            if (nameExists)
            {
                ModelState.AddModelError(nameof(request.Name), "You already have a notebook with this name.");
                return ValidationProblem(ModelState);
            }

            var notebook = new Notebook
            {
                UserId = userId,
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notebooks.Add(notebook);
            await _context.SaveChangesAsync();

            return Created($"/api/notebooks/{notebook.Id}", new NotebookResponse(
                notebook.Id,
                notebook.Name,
                notebook.Description,
                0,
                notebook.CreatedAt,
                notebook.UpdatedAt));
        }

        private int GetRequiredUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }

    public sealed class CreateNotebookRequest
    {
        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }
    }

    public sealed record NotebookResponse(
        int Id,
        string Name,
        string? Description,
        int BlogCount,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
