using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Json;

namespace Logging
{
    /// <summary>
    /// Singleton logger. Writes to:
    ///   1. In-memory ring buffer  — powers the /Logs UI.
    ///   2. Serilog → rolling JSON file under /Logs/.
    ///   3. Microsoft.Extensions.Logging — console / host output.
    /// </summary>
    public sealed class AppLogger : IAppLogger, IHostedService, IDisposable
    {
        private const int MaxBufferSize = 1_000;

        private readonly ConcurrentQueue<LogEntry>                _buffer = new();
        private readonly Serilog.ILogger                          _serilog;
        private readonly ILogger<AppLogger>                       _msLogger;
        private readonly string                                   _logDirectory;

        public AppLogger(ILogger<AppLogger> msLogger)
        {
            _msLogger = msLogger;
            _logDirectory = ResolveLogDirectory();
            Directory.CreateDirectory(_logDirectory);

            _serilog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    formatter: new JsonFormatter(),
                    path: Path.Combine(_logDirectory, "app-.json"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true)
                .CreateLogger();

            LoadTodaysLogsFromFile();
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            (_serilog as IDisposable)?.Dispose();
            return Task.CompletedTask;
        }

        // ── IAppLogger ────────────────────────────────────────────────────────
        public void LogInformation(string category, string message,
            string? requestPath = null, string? userId = null)
            => Enqueue(AppLogLevel.Information, category, message, null, requestPath, userId);

        public void LogWarning(string category, string message,
            string? requestPath = null, string? userId = null)
            => Enqueue(AppLogLevel.Warning, category, message, null, requestPath, userId);

        public void LogError(string category, string message, Exception? exception = null,
            string? requestPath = null, string? userId = null)
            => Enqueue(AppLogLevel.Error, category, message, exception, requestPath, userId);

        public IReadOnlyList<LogEntry> GetRecentLogs(int count = 500)
            => _buffer.TakeLast(count).Reverse().ToList();

        public IReadOnlyList<LogEntry> GetLogsByLevel(AppLogLevel level, int count = 500)
            => _buffer.Where(e => e.Level == level).TakeLast(count).Reverse().ToList();

        public IReadOnlyList<LogEntry> GetFilteredLogs(
            DateTime?    date   = null,
            AppLogLevel? level  = null,
            string?      search = null,
            int          count  = 500)
        {
            IEnumerable<LogEntry> query = _buffer;

            if (date.HasValue)
            {
                var day = date.Value.Date;
                query = query.Where(e => e.CreatedAt.ToLocalTime().Date == day);
            }

            if (level.HasValue)
                query = query.Where(e => e.Level == level.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    e.Message.Contains(term, StringComparison.OrdinalIgnoreCase)     ||
                    e.Category.Contains(term, StringComparison.OrdinalIgnoreCase)    ||
                    (e.RequestPath != null && e.RequestPath.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (e.Exception   != null && e.Exception.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return query.TakeLast(count).Reverse().ToList();
        }

        public PagedResult<LogEntry> GetPagedLogs(
            DateTime?    date     = null,
            AppLogLevel? level    = null,
            string?      search   = null,
            int          page     = 1,
            int          pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 10, 200);
            page     = Math.Max(1, page);

            IEnumerable<LogEntry> query = _buffer;

            if (date.HasValue)
            {
                var day = date.Value.Date;
                query = query.Where(e => e.CreatedAt.ToLocalTime().Date == day);
            }

            if (level.HasValue)
                query = query.Where(e => e.Level == level.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    e.Message.Contains(term, StringComparison.OrdinalIgnoreCase)     ||
                    e.Category.Contains(term, StringComparison.OrdinalIgnoreCase)    ||
                    (e.RequestPath != null && e.RequestPath.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (e.Exception   != null && e.Exception.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            // Newest first
            var ordered = query.OrderByDescending(e => e.CreatedAt).ToList();
            var total   = ordered.Count;
            var items   = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<LogEntry>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public IReadOnlyList<DateOnly> GetAvailableDates()
            => _buffer
                .Select(e => DateOnly.FromDateTime(e.CreatedAt.ToLocalTime()))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

        // ── Private ───────────────────────────────────────────────────────────
        private void Enqueue(AppLogLevel level, string category, string message,
            Exception? ex, string? requestPath, string? userId)
        {
            var entry = new LogEntry
            {
                Level       = level,
                Category    = category,
                Message     = message,
                Exception   = ex?.Message,
                StackTrace  = ex?.StackTrace,
                RequestPath = requestPath,
                UserId      = userId,
                CreatedAt   = DateTime.UtcNow
            };

            // 1. Serilog → JSON file
            using (LogContext.PushProperty("Category",    category))
            using (LogContext.PushProperty("RequestPath", requestPath ?? string.Empty))
            using (LogContext.PushProperty("UserId",      userId      ?? string.Empty))
            {
                switch (level)
                {
                    case AppLogLevel.Information:
                        _serilog.Information("[{Category}] {Message}", category, message);
                        break;
                    case AppLogLevel.Warning:
                        _serilog.Warning("[{Category}] {Message}", category, message);
                        break;
                    case AppLogLevel.Error:
                        if (ex is not null)
                            _serilog.Error(ex, "[{Category}] {Message}", category, message);
                        else
                            _serilog.Error("[{Category}] {Message}", category, message);
                        break;
                }
            }

            // 2. Console via Microsoft.Extensions.Logging
            switch (level)
            {
                case AppLogLevel.Information:
                    _msLogger.LogInformation("[{Category}] {Message}", category, message);
                    break;
                case AppLogLevel.Warning:
                    _msLogger.LogWarning("[{Category}] {Message}", category, message);
                    break;
                case AppLogLevel.Error:
                    _msLogger.LogError(ex, "[{Category}] {Message}", category, message);
                    break;
            }

            // 3. In-memory ring buffer
            _buffer.Enqueue(entry);
            while (_buffer.Count > MaxBufferSize)
                _buffer.TryDequeue(out _);
        }

        public void Dispose() => (_serilog as IDisposable)?.Dispose();

        // ── Startup: reload today's log file into the buffer ──────────────────
        private void LoadTodaysLogsFromFile()
        {
            try
            {
                var today    = DateTime.Now.ToString("yyyyMMdd");
                var filePath = Path.Combine(_logDirectory, $"app-{today}.json");

                if (!File.Exists(filePath)) return;

                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using var doc  = JsonDocument.Parse(line);
                        var root       = doc.RootElement;

                        var timestamp  = root.GetProperty("Timestamp").GetDateTimeOffset().UtcDateTime;
                        var levelStr   = root.GetProperty("Level").GetString() ?? "Information";
                        var props      = root.GetProperty("Properties");

                        var level = levelStr switch
                        {
                            "Warning" => AppLogLevel.Warning,
                            "Error"   => AppLogLevel.Error,
                            _         => AppLogLevel.Information
                        };

                        var entry = new LogEntry
                        {
                            Level       = level,
                            Category    = props.TryGetProperty("Category",    out var cat)  ? cat.GetString()  ?? "" : "",
                            Message     = props.TryGetProperty("Message",     out var msg)  ? msg.GetString()  ?? "" : "",
                            RequestPath = props.TryGetProperty("RequestPath", out var rp)   ? rp.GetString()        : null,
                            UserId      = props.TryGetProperty("UserId",      out var uid)  && uid.GetString() != "" ? uid.GetString() : null,
                            CreatedAt   = timestamp
                        };

                        _buffer.Enqueue(entry);
                    }
                    catch { /* skip malformed lines */ }
                }

                // trim to max buffer size
                while (_buffer.Count > MaxBufferSize)
                    _buffer.TryDequeue(out _);
            }
            catch { /* if file is locked or missing, just start fresh */ }
        }

        private static string ResolveLogDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("LETTERFLOW_LOG_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            var appLogs = Path.Combine(AppContext.BaseDirectory, "Logs");
            return Directory.Exists(appLogs)
                ? appLogs
                : Path.Combine(Path.GetTempPath(), "letterflow-logs");
        }
    }
}
