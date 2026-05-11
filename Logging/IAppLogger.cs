namespace Logging
{
    public interface IAppLogger
    {
        void LogInformation(string category, string message,
            string? requestPath = null, string? userId = null);

        void LogWarning(string category, string message,
            string? requestPath = null, string? userId = null);

        void LogError(string category, string message, Exception? exception = null,
            string? requestPath = null, string? userId = null);

        IReadOnlyList<LogEntry> GetRecentLogs(int count = 500);
        IReadOnlyList<LogEntry> GetLogsByLevel(AppLogLevel level, int count = 500);

        IReadOnlyList<LogEntry> GetFilteredLogs(
            DateTime?    date   = null,
            AppLogLevel? level  = null,
            string?      search = null,
            int          count  = 500);

        /// <summary>
        /// Returns a paginated, filtered slice of the in-memory log buffer.
        /// Entries are ordered newest-first.
        /// </summary>
        PagedResult<LogEntry> GetPagedLogs(
            DateTime?    date     = null,
            AppLogLevel? level    = null,
            string?      search   = null,
            int          page     = 1,
            int          pageSize = 50);

        IReadOnlyList<DateOnly> GetAvailableDates();
    }
}
