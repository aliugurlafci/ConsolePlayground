namespace CSPLibraries.Mail.Constants;

internal static class MailConstants
{
    internal const int DefaultPort = 587;
    internal const int TimeoutSeconds = 60;
    internal const int MaxConnections = 4;
    internal const int MaxRetries = 3;
    internal const int MaxCachedTemplates = 256;
    internal const int StreamBufferSize = 81_920;

    internal static readonly TimeSpan ConnectionIdleTimeout = TimeSpan.FromSeconds(50);

    internal static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
}
