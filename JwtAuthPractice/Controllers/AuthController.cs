using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JwtAuthPractice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthController()
    {
        // Initialize with hardcoded values (in real app, use dependency injection)
        _tokenGenerator = new JwtTokenGenerator(
            key: "this-is-a-super-secret-key-that-should-be-at-least-32-characters-long-for-security",
            issuer: "JwtAuthPractice",
            audience: "JwtAuthPracticeUsers"
        );
    }

    /// <summary>
    /// LOGIN endpoint - No authentication required
    /// Takes username and password, returns JWT token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // ⚠️  In real app: validate credentials against database
        // For this practice, we'll accept any login
        
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        // HARDCODED validation (for demo only!)
        if (request.Username != "testuser" || request.Password != "password123")
            return Unauthorized(new { error = "Invalid username or password." });

        // Generate JWT token
        var token = _tokenGenerator.GenerateToken(
            userId: "1",
            username: request.Username,
            email: "testuser@example.com",
            expirationMinutes: 60
        );

        return Ok(new
        {
            message = "Login successful",
            token = token,
            expiresIn = 3600  // seconds
        });
    }

    /// <summary>
    /// PROTECTED endpoint - Requires valid JWT token
    /// Returns current user's info from claims
    /// </summary>
    [Authorize]  // ← This decorator enforces JWT validation
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var customClaim = User.FindFirstValue("CustomClaim");

        return Ok(new
        {
            userId = userId,
            username = username,
            email = email,
            customClaim = customClaim
        });
    }

    /// <summary>
    /// Another PROTECTED endpoint - Demonstrates authorization
    /// </summary>
    [Authorize]
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        return Ok(new
        {
            message = $"Hello, {username}! This is your profile.",
            profile = new
            {
                username = username,
                email = User.FindFirstValue(ClaimTypes.Email),
                registeredAt = DateTime.Now.AddMonths(-1)
            }
        });
    }

    /// <summary>
    /// PUBLIC endpoint - No authentication required
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new { message = "This is a public endpoint. No token required." });
    }
}

// ═══════════════════════════════════════════════════════════════
// DTOs (Data Transfer Objects)
// ═══════════════════════════════════════════════════════════════

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
