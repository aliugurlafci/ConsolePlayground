namespace CSPLibraries.Rest.Constants;

internal static class RestDefaults
{
    internal const int TimeoutSeconds = 30;
    internal const int MaxRetries = 2;
    internal const int MaxRetryLimit = 10;
    internal const int MaxRedirects = 5;
    internal const int MaxConnectionsPerServer = 64;
    internal const int StreamBufferSize = 81_920;
    internal const string UserAgent = "CSPLibraries-RestService/3.0";

    internal static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);

    internal static bool IsTransientStatus(int statusCode) =>
        statusCode is 408 or 429 or 500 or 502 or 503 or 504;
}
