using Logging;

namespace LetterTemplatePractice.Models.ViewModels
{
    public sealed class LogFilterViewModel
    {
        // ── Filter params ─────────────────────────────────────────────────────
        public DateTime?    Date     { get; set; }
        public AppLogLevel? Level    { get; set; }
        public string?      Search   { get; set; }

        // ── Pagination params ─────────────────────────────────────────────────
        public int Page     { get; set; } = 1;
        public int PageSize { get; set; } = 15;

        // ── Results ───────────────────────────────────────────────────────────
        public PagedResult<LogEntry>    PagedLogs      { get; set; } = new();
        public IReadOnlyList<DateOnly>  AvailableDates { get; set; } = [];
        public string?                  ErrorMessage   { get; set; }

        // ── Convenience ───────────────────────────────────────────────────────
        public IReadOnlyList<LogEntry> Logs       => PagedLogs.Items;
        public int                     TotalCount => PagedLogs.TotalCount;
        public int                     TotalPages => PagedLogs.TotalPages;
        public bool                    HasPrev    => PagedLogs.HasPrev;
        public bool                    HasNext    => PagedLogs.HasNext;

        public int InfoCount  => Logs.Count(e => e.Level == AppLogLevel.Information);
        public int WarnCount  => Logs.Count(e => e.Level == AppLogLevel.Warning);
        public int ErrorCount => Logs.Count(e => e.Level == AppLogLevel.Error);

        public bool HasFilters =>
            Date.HasValue ||
            Level.HasValue ||
            !string.IsNullOrWhiteSpace(Search);
    }
}
