namespace Pakar.Application.Common;

/// <summary>One page of a list plus the total count, so clients can build pagination.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Normalizes paging input so a client cannot request unbounded pages.</summary>
public readonly record struct PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest From(int? page, int? pageSize) => new(
        Math.Max(page ?? 1, 1),
        Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
