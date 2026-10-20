using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Workers;

internal sealed class OperationTimer(ILogger logger, string operationName, TimeSpan? warningThreshold) : IDisposable
{
    private readonly long _start = Stopwatch.GetTimestamp();
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        TimeSpan elapsed = Stopwatch.GetElapsedTime(_start);
        LogLevel level = warningThreshold is TimeSpan threshold && elapsed >= threshold ? LogLevel.Warning : LogLevel.Information;
        logger.Log(level, "{Operation} tamamlandı ({ElapsedMs} ms)", operationName, (long)elapsed.TotalMilliseconds);
    }
}
