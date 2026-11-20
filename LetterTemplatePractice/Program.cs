using LetterTemplatePractice.Auth;
using LetterTemplatePractice.BackgroundServices;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Models.Configuration;
using LetterTemplatePractice.Services;
using Logging;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ---- SSO configuration section ----
builder.Services.Configure<SsoSettings>(builder.Configuration.GetSection("Sso"));

var sso = builder.Configuration.GetSection("Sso").Get<SsoSettings>() ?? new SsoSettings();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// Logger — singleton, in-memory buffer + Serilog JSON file
builder.Services.AddSingleton<AppLogger>();
builder.Services.AddSingleton<IAppLogger>(sp => sp.GetRequiredService<AppLogger>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AppLogger>());

const string ExternalScheme = "External";

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath           = "/Account/Login";
        options.LogoutPath          = "/Account/Logout";
        options.AccessDeniedPath    = "/Account/AccessDenied";
        options.Cookie.Name         = "LetterFlow.Auth";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite     = SameSiteMode.Lax;
        options.ExpireTimeSpan      = TimeSpan.FromHours(8);
        options.SlidingExpiration   = true;
    })
    .AddCookie(ExternalScheme, options =>
    {
        options.Cookie.Name         = "LetterFlow.External";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite     = SameSiteMode.Lax;
        options.ExpireTimeSpan      = TimeSpan.FromMinutes(5);
    })
    .AddGoogle(options =>
    {
        options.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.SignInScheme = ExternalScheme;
        options.SaveTokens   = true;
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = sso.Authority;
        options.ClientId = sso.ClientId;
        options.ClientSecret = sso.ClientSecret;
        options.ResponseType = sso.ResponseType;
        options.ResponseMode = sso.ResponseMode;
        options.CallbackPath = sso.CallbackPath;
        options.SignedOutCallbackPath = sso.SignedOutCallbackPath;
        options.RemoteSignOutPath = sso.RemoteSignOutPath;
        options.SaveTokens = sso.SaveTokens;
        options.GetClaimsFromUserInfoEndpoint = sso.GetClaimsFromUserInfoEndpoint;

        foreach (var scope in sso.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            options.Scope.Add(scope);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role",
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = sso.Authority,
            ValidAudience = sso.ClientId
        };

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                ctx.ProtocolMessage.Prompt = "login";
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var authService = ctx.HttpContext.RequestServices
                    .GetRequiredService<LetterTemplatePractice.Auth.IAuthService>();

                var email = ctx.Principal.FindFirstValue(ClaimTypes.Email);
                var displayName = ctx.Principal.FindFirstValue(ClaimTypes.Name);
                var avatarUrl = ctx.Principal.FindFirstValue("picture")
                    ?? ctx.Principal.FindFirstValue("avatar_url");

                var user = await authService.FindOrCreateSsoUserAsync(email, displayName, avatarUrl);
                if (user is null)
                {
                    ctx.HandleResponse();
                    ctx.Response.Redirect("/Account/AccessDenied?error=account_not_found");
                    return;
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Username),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.Role, user.Role)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                ctx.Principal = new ClaimsPrincipal(identity);
                ctx.Success();
            },
            OnAuthenticationFailed = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.Redirect("/Account/AccessDenied?error=" + Uri.EscapeDataString(ctx.Exception.Message));
                return Task.CompletedTask;
            }
        };

        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
        }
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<BlogService>();

// AI — Gemini (free tier)
builder.Services.AddHttpClient("gemini");
builder.Services.AddScoped<GeminiService>();

// AI Queue — background job processing
builder.Services.Configure<AiQueueOptions>(builder.Configuration.GetSection("AiQueue"));
builder.Services.AddScoped<IAiQueue, AiQueueService>();
builder.Services.AddHostedService<GeminiWorker>();

// Newsletter — daily news digest
builder.Services.AddHttpClient("news");
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<NewsletterSender>();
builder.Services.AddHostedService<ScheduledPostPublisher>();
builder.Services.AddHostedService<NewsletterWorker>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "LetterFlow API", Version = "v1" }));

var rlSection = builder.Configuration.GetSection("RateLimiting");

int aiPermit  = rlSection.GetValue("AiPerUser:PermitLimit",  20);
int aiWindow  = rlSection.GetValue("AiPerUser:WindowSeconds", 60);
int glPermit  = rlSection.GetValue("Global:PermitLimit",     200);
int glWindow  = rlSection.GetValue("Global:WindowSeconds",    60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("ai-per-user", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = aiPermit,
                Window               = TimeSpan.FromSeconds(aiWindow),
                SegmentsPerWindow    = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    // Global IP-based sliding window for all traffic
    options.AddPolicy("global", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = glPermit,
                Window               = TimeSpan.FromSeconds(glWindow),
                SegmentsPerWindow    = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"error":"Too many requests. Please slow down and try again shortly."}""", ct);
    };
});

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Auto-apply EF Core migrations on startup (safe to run on every deploy)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

await DataSeeder.SeedAsync(app.Services);

// www -> non-www 301 redirect.
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;

    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
    {
        var newHost = host[4..];
        var newUrl = string.Concat(
            context.Request.Scheme,
            "://",
            newHost,
            context.Request.Path,
            context.Request.QueryString);

        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = newUrl;
        return;
    }

    await next();
});

// On startup: reset stuck InProgress jobs and fail exhausted ones
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var now = DateTimeOffset.UtcNow;

    var stuckJobs = db.AiJobs
        .Where(j => j.Status == AiJobStatus.InProgress)
        .ToList();
    foreach (var job in stuckJobs)
    {
        job.Status        = AiJobStatus.Pending;
        job.WorkerId      = null;
        job.StartedAt     = null;
        job.NextAttemptAt = now.AddSeconds(30 * job.Attempts); // stagger retries
    }

    var exhaustedJobs = db.AiJobs
        .Where(j => j.Status == AiJobStatus.Pending && j.Attempts >= j.MaxAttempts)
        .ToList();
    foreach (var job in exhaustedJobs)
    {
        job.Status      = AiJobStatus.Failed;
        job.CompletedAt = now;
        job.Error       = job.Error ?? "Exceeded max attempts";
    }

    if (stuckJobs.Count > 0 || exhaustedJobs.Count > 0)
        await db.SaveChangesAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LetterFlow API v1"));
}

// Render terminates TLS at the load balancer; skip HTTPS redirect inside the container
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
