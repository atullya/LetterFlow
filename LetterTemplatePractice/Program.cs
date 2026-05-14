using LetterTemplatePractice.Auth;
using LetterTemplatePractice.BackgroundServices;
using LetterTemplatePractice.Data;
using LetterTemplatePractice.Models;
using LetterTemplatePractice.Services;
using Logging;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "LetterFlow API", Version = "v1" }));

var app = builder.Build();

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
app.UseRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
