namespace CSPLibraries.Security.Models;

public sealed record JwtValidationResult(bool IsValid, string? Error, IReadOnlyDictionary<string, object?> Claims)
{
    internal static JwtValidationResult Fail(string error) => new(false, error, new Dictionary<string, object?>());

    public string? Subject => Claims.TryGetValue("sub", out object? value) ? value?.ToString() : null;
}
