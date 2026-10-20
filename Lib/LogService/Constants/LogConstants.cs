namespace CSPLibraries.Logging.Constants;

internal static class LogConstants
{
    internal const int QueueCapacity = 100_000;
    internal const int BatchSize = 1_000;
    internal const long MaxFileSizeBytes = 100L * 1024 * 1024;
    internal const int RetentionDays = 30;
    internal const int FileBufferSize = 64 * 1024;
    internal const string DefaultCategory = "App";
    internal const string DefaultFilePrefix = "log";
    internal const string CorrelationIdKey = "CorrelationId";
    internal const string OriginalFormatKey = "{OriginalFormat}";
    internal const string MaskedValue = "***";

    internal static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);
}
