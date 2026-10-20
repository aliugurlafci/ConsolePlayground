using System.Threading.Channels;
using CSPLibraries.Logging.Constants;
using CSPLibraries.Logging.Datas;
using CSPLibraries.Logging.Interfaces;
using CSPLibraries.Logging.Models;
using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Workers;

internal sealed class LogProcessor : IAsyncDisposable
{
    private readonly Channel<QueueItem> _channel;
    private readonly ILogSink[] _sinks;
    private readonly int _batchSize;
    private readonly Task _worker;
    private readonly CancellationTokenSource _shutdown = new();
    private long _dropped;
    private long _reportedDropped;

    internal LogProcessor(LogServiceOptions options, ILogSink[] sinks)
    {
        _sinks = sinks;
        _batchSize = Math.Max(1, options.BatchSize);
        _channel = Channel.CreateBounded<QueueItem>(
            new BoundedChannelOptions(Math.Max(100, options.QueueCapacity))
            {
                FullMode = options.QueueFullMode,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            },
            dropped =>
            {
                if (dropped.Flush is not null) dropped.Flush.TrySetResult();
                else Interlocked.Increment(ref _dropped);
            });

        _worker = Task.Run(ProcessAsync);
    }

    internal long DroppedCount => Interlocked.Read(ref _dropped);

    internal void Enqueue(LogEntry entry) => _channel.Writer.TryWrite(new QueueItem(entry, null));

    internal Task FlushAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(new QueueItem(null, completion)))
            return Task.CompletedTask;

        return completion.Task.WaitAsync(cancellationToken);
    }

    private async Task ProcessAsync()
    {
        var batch = new List<LogEntry>(_batchSize);
        ChannelReader<QueueItem> reader = _channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                TaskCompletionSource? flushRequest = null;

                while (batch.Count < _batchSize && reader.TryRead(out QueueItem item))
                {
                    if (item.Flush is not null)
                    {
                        flushRequest = item.Flush;
                        break;
                    }

                    batch.Add(item.Entry!);
                }

                ReportDrops(batch);
                if (batch.Count > 0)
                {
                    await WriteToSinksAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                }

                if (flushRequest is not null)
                {
                    foreach (ILogSink sink in _sinks)
                        await SafeAsync(() => sink.FlushAsync(CancellationToken.None)).ConfigureAwait(false);
                    flushRequest.TrySetResult();
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private void ReportDrops(List<LogEntry> batch)
    {
        long dropped = Interlocked.Read(ref _dropped);
        if (dropped == _reportedDropped) return;

        batch.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = LogLevel.Warning,
            Category = "CSPLibraries.Logging",
            Message = $"Log kuyruğu doldu; {dropped - _reportedDropped} kayıt atıldı (toplam {dropped})."
        });
        _reportedDropped = dropped;
    }

    private async Task WriteToSinksAsync(List<LogEntry> batch)
    {
        foreach (ILogSink sink in _sinks)
            await SafeAsync(() => sink.WriteBatchAsync(batch, CancellationToken.None)).ConfigureAwait(false);
    }

    private static async Task SafeAsync(Func<ValueTask> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try { await Console.Error.WriteLineAsync("[CSPLibraries.Logging] Log hedefi hatası: " + ex.Message).ConfigureAwait(false); }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        try
        {
            await _worker.WaitAsync(LogConstants.ShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
        }

        foreach (ILogSink sink in _sinks)
            await SafeAsync(sink.DisposeAsync).ConfigureAwait(false);

        _shutdown.Dispose();
    }

    private readonly record struct QueueItem(LogEntry? Entry, TaskCompletionSource? Flush);
}
