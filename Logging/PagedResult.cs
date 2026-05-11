namespace Logging
{
    /// <summary>Generic pagination envelope returned by all paged log/job queries.</summary>
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items      { get; init; } = [];
        public int              TotalCount { get; init; }
        public int              Page       { get; init; }
        public int              PageSize   { get; init; }
        public int              TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public bool             HasPrev    => Page > 1;
        public bool             HasNext    => Page < TotalPages;
    }
}
