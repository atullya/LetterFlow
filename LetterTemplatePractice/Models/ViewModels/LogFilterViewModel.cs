using Logging;

namespace LetterTemplatePractice.Models.ViewModels
{
    public sealed class LogFilterViewModel
    {
        public DateTime? Date { get; set; }
        public AppLogLevel? Level { get; set; }
        public string? Search { get; set; }
        public bool HasSubmitted { get; set; }

        public IReadOnlyList<LogEntry> Logs { get; set; } = [];
        public IReadOnlyList<DateOnly> AvailableDates { get; set; } = [];

        public int InfoCount => Logs.Count(e => e.Level == AppLogLevel.Information);
        public int WarnCount => Logs.Count(e => e.Level == AppLogLevel.Warning);
        public int ErrorCount => Logs.Count(e => e.Level == AppLogLevel.Error);

        public bool HasFilters =>
            Date.HasValue ||
            Level.HasValue ||
            !string.IsNullOrWhiteSpace(Search);
    }
}
