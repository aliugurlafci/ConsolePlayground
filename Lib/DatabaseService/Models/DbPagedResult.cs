namespace CSPLibraries.Database.Models;

public sealed record DbPagedResult<T>(List<T> Items, int PageNumber, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => PageNumber < TotalPages;
}
