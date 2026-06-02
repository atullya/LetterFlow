using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LetterTemplateAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IConfiguration _config;

        public AuthController(IAuthService auth, IConfiguration config)
        {
            _auth = auth;
            _config = config;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Username and password are required." });

            var result = await _auth.LoginAsync(req.Username, req.Password);
            if (!result.Success)
                return Unauthorized(new { error = result.Error ?? "Invalid credentials." });

            var user = result.User!;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var (accessToken, expiresIn) = GenerateToken(claims);
            var refreshToken = GenerateRefreshToken();

            return Ok(new
            {
                accessToken,
                refreshToken,
                expiresIn,
                user = new { id = user.Id, username = user.Username, email = user.Email, role = user.Role, displayName = user.DisplayName, avatarUrl = user.AvatarUrl }
            });
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Username, email, and password are required." });

            var result = await _auth.RegisterAsync(req.Username, req.Email, req.Password);
            if (!result.Success)
                return Conflict(new { error = result.Error ?? "Registration failed." });

            return Ok(new { message = "Registration successful. Please log in." });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public IActionResult Refresh([FromBody] RefreshRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.AccessToken) || string.IsNullOrWhiteSpace(req.RefreshToken))
                return BadRequest(new { error = "Access token and refresh token are required." });

            var principal = ValidateExpiredToken(req.AccessToken);
            if (principal == null)
                return Unauthorized(new { error = "Invalid access token." });

            if (string.IsNullOrWhiteSpace(req.RefreshToken) || req.RefreshToken.Length < 64)
                return Unauthorized(new { error = "Invalid refresh token." });

            var (newToken, expiresIn) = GenerateToken(principal.Claims);
            var newRefresh = GenerateRefreshToken();

            return Ok(new { accessToken = newToken, refreshToken = newRefresh, expiresIn });
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { message = "Logged out successfully. Discard your access and refresh tokens." });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            var user = await _auth.GetUserByIdAsync(uid.Value);
            if (user == null) return NotFound();
            return Ok(new { id = user.Id, username = user.Username, email = user.Email, role = user.Role, displayName = user.DisplayName, avatarUrl = user.AvatarUrl, createdAt = user.CreatedAt });
        }

        private (string Token, int ExpiresIn) GenerateToken(IEnumerable<Claim> claims)
        {
            var key = GetKey();
            var expireMinutes = int.TryParse(_config["Jwt:AccessTokenExpireMinutes"], out var m) ? m : 60;
            var issuer = _config["Jwt:Issuer"] ?? "LetterFlowAPI";
            var audience = _config["Jwt:Audience"] ?? "LetterFlowAPI";

            var desc = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };
            var handler = new JwtSecurityTokenHandler();
            return (handler.WriteToken(handler.CreateToken(desc)), expireMinutes * 60);
        }

        private string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private ClaimsPrincipal? ValidateExpiredToken(string token)
        {
            try
            {
                var key = GetKey();
                var issuer = _config["Jwt:Issuer"] ?? "LetterFlowAPI";
                var audience = _config["Jwt:Audience"] ?? "LetterFlowAPI";
                var tvp = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = key
                };
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, tvp, out var st);
                if (st is not JwtSecurityToken jwt || !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;
                return principal;
            }
            catch { return null; }
        }

        private SymmetricSecurityKey GetKey()
        {
            var raw = _config["Jwt:Key"] ?? _config.GetConnectionString("DefaultConnection")!;
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(raw));
        }

        private int? GetUserId()
        {
            var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(v, out var id) ? id : null;
        }
    }

    public sealed class LoginRequest { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }
    public sealed class RegisterRequest { public string Username { get; set; } = ""; public string Email { get; set; } = ""; public string Password { get; set; } = ""; }
    public sealed class RefreshRequest { public string AccessToken { get; set; } = ""; public string RefreshToken { get; set; } = ""; }
}
