using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Logging
{
    public sealed class RequestLoggingMiddleware
    {
        private const string Category = "HTTP";

        private readonly RequestDelegate _next;
        private readonly IAppLogger      _logger;

        public RequestLoggingMiddleware(RequestDelegate next, IAppLogger logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw     = Stopwatch.StartNew();
            var path   = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                await _next(context);
                sw.Stop();

                var status = context.Response.StatusCode;
                var level  = status >= 500 ? AppLogLevel.Error
                           : status >= 400 ? AppLogLevel.Warning
                           : AppLogLevel.Information;

                var message = $"{method} {path} → {status} ({sw.ElapsedMilliseconds} ms)";

                switch (level)
                {
                    case AppLogLevel.Error:
                        _logger.LogError(Category, message, requestPath: path, userId: userId);
                        break;
                    case AppLogLevel.Warning:
                        _logger.LogWarning(Category, message, requestPath: path, userId: userId);
                        break;
                    default:
                        _logger.LogInformation(Category, message, requestPath: path, userId: userId);
                        break;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    Category,
                    $"{method} {path} → UNHANDLED EXCEPTION ({sw.ElapsedMilliseconds} ms): {ex.Message}",
                    ex,
                    requestPath: path,
                    userId: userId);
                throw;
            }
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
            => app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
