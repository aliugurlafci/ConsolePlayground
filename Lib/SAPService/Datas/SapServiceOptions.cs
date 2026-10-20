using System.Text;
using System.Text.Json;
using CSPLibraries.Sap.Constants;

namespace CSPLibraries.Sap.Datas;

public enum ContentFormat
{
    Json = 0,
    Xml = 1
}

public sealed class SapServiceOptions
{
    public required string BaseUrl { get; init; }

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public string? SapClient { get; init; }

    public string? SapLanguage { get; init; }

    public ContentFormat DefaultFormat { get; init; } = ContentFormat.Json;

    public int TimeoutSeconds { get; init; } = SapConstants.TimeoutSeconds;

    public int MaxRetries { get; init; } = SapConstants.MaxRetries;

    public bool ValidateResponseFormat { get; init; }

    public bool EnableCsrfToken { get; init; } = true;

    public string? CsrfFetchEndpoint { get; init; }

    public bool AcceptAnyServerCertificate { get; init; }

    public bool FollowRedirects { get; init; }

    public Encoding? ResponseFallbackEncoding { get; init; }

    public JsonSerializerOptions? JsonOptions { get; init; }

    public string? UserAgent { get; init; }
}
