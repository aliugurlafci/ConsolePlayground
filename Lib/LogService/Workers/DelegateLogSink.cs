using CSPLibraries.Logging.Interfaces;
using CSPLibraries.Logging.Models;

namespace CSPLibraries.Logging.Workers;

public sealed class DelegateLogSink(Func<IReadOnlyList<LogEntry>, CancellationToken, ValueTask> writeBatch) : ILogSink
{
    private readonly Func<IReadOnlyList<LogEntry>, CancellationToken, ValueTask> _writeBatch =
        writeBatch ?? throw new ArgumentNullException(nameof(writeBatch));

    public ValueTask WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken) =>
        _writeBatch(batch, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
