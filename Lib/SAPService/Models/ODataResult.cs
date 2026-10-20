namespace CSPLibraries.Sap.Models;

public sealed record ODataResult<T>(List<T> Items, string? NextLink, long? TotalCount)
{
    public bool HasMore => NextLink is not null;
}
