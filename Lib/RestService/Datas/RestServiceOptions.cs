using System.Text;
using System.Text.Json;
using CSPLibraries.Rest.Constants;

namespace CSPLibraries.Rest.Datas;

public enum ContentFormat
{
    Json = 0,
    Xml = 1
}

public sealed class RestServiceOptions
{
    public required string BaseUrl { get; init; }

    public int TimeoutSeconds { get; init; } = RestDefaults.TimeoutSeconds;

    public int MaxRetries { get; init; } = RestDefaults.MaxRetries;

    public ContentFormat DefaultFormat { get; init; } = ContentFormat.Json;

    public bool AcceptAnyServerCertificate { get; init; }

    public bool FollowRedirects { get; init; }

    public Encoding? ResponseFallbackEncoding { get; init; }

    public JsonSerializerOptions? JsonOptions { get; init; }

    public string? UserAgent { get; init; }
}
