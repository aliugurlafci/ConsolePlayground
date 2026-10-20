namespace CSPLibraries.Sap.Constants;

internal static class SapConstants
{
    internal const string UserAgent = "CSPLibraries-SAPService/3.0";

    internal const int TimeoutSeconds = 30;
    internal const int MaxRetries = 2;
    internal const int MaxRetryLimit = 10;
    internal const int MaxConnectionsPerServer = 64;
    internal const int MaxODataPages = 10_000;

    internal const string JsonMediaType = "application/json";
    internal const string XmlMediaType = "application/xml";
    internal const string TextMediaType = "text/plain";

    internal const string CsrfTokenHeader = "X-CSRF-Token";
    internal const string CsrfFetchValue = "Fetch";
    internal const string CsrfRequiredValue = "Required";
    internal const string HttpMethodOverrideHeader = "X-HTTP-Method";

    internal const string SapClientParameter = "sap-client";
    internal const string SapLanguageParameter = "sap-language";

    internal static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);

    internal static bool IsTransientStatus(int statusCode) =>
        statusCode is 408 or 429 or 500 or 502 or 503 or 504;
}

internal static class ODataKeys
{
    internal const string V2Root = "d";
    internal const string V2Results = "results";
    internal const string V2Next = "__next";
    internal const string V2Count = "__count";
    internal const string V4Value = "value";
    internal const string V4NextLink = "@odata.nextLink";
    internal const string V4Count = "@odata.count";
}
