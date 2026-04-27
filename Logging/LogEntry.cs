namespace Logging
{
    public sealed class LogEntry
    {
        public int         Id          { get; set; }
        public AppLogLevel Level       { get; init; }
        public string      Category    { get; init; } = string.Empty;
        public string      Message     { get; init; } = string.Empty;
        public string?     Exception   { get; init; }
        public string?     StackTrace  { get; init; }
        public string?     RequestPath { get; init; }
        public string?     UserId      { get; init; }
        public DateTime    CreatedAt   { get; init; } = DateTime.UtcNow;
    }
}
