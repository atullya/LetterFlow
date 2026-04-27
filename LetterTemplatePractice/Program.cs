using LetterTemplatePractice.Auth;
using LetterTemplatePractice.Data;
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

// Cookie auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath           = "/Account/Login";
        options.LogoutPath          = "/Account/Logout";
        options.AccessDeniedPath    = "/Account/AccessDenied";
        options.Cookie.Name         = "LetterFlow.Auth";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite     = SameSiteMode.Strict;
        options.ExpireTimeSpan      = TimeSpan.FromHours(8);
        options.SlidingExpiration   = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<BlogService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "LetterFlow API", Version = "v1" }));

var app = builder.Build();

await DataSeeder.SeedAsync(app.Services);

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
