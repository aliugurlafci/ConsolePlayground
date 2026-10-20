using CSPLibraries.Logging.Models;

namespace CSPLibraries.Logging.Interfaces;

public interface ILogSink : IAsyncDisposable
{
    ValueTask WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken);

    ValueTask FlushAsync(CancellationToken cancellationToken);
}
