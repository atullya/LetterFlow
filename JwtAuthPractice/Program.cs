using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════
// 1. JWT Configuration
// ═══════════════════════════════════════════════════════════════

var jwtKey = "this-is-a-super-secret-key-that-should-be-at-least-32-characters-long-for-security";
var jwtIssuer = "JwtAuthPractice";
var jwtAudience = "JwtAuthPracticeUsers";

// 2. Add Authentication Service
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero  // No tolerance for expired tokens
    };
});

// 3. Add Authorization Service
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();  // ← Must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();

app.Run();

// ═══════════════════════════════════════════════════════════════
// HELPER CLASS: JWT Token Generator
// ═══════════════════════════════════════════════════════════════

public class JwtTokenGenerator
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenGenerator(string key, string issuer, string audience)
    {
        _key = key;
        _issuer = issuer;
        _audience = audience;
    }

    /// <summary>
    /// Generates a JWT token for the given user claims.
    /// </summary>
    public string GenerateToken(string userId, string username, string email, int expirationMinutes = 60)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim("CustomClaim", "CustomValue")  // Example of custom claim
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
