using System.Collections.Concurrent;
using CSPLibraries.Logging.Constants;
using CSPLibraries.Logging.Datas;
using CSPLibraries.Logging.Interfaces;
using CSPLibraries.Logging.Workers;
using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging;

public sealed class LogService : ILogService
{
    private readonly LogServiceOptions _options;
    private readonly LogProcessor _processor;
    private readonly ConcurrentDictionary<string, CspLogger> _loggers = new(StringComparer.Ordinal);
    private readonly ILogger _defaultLogger;
    private int _disposed;

    public LogService(LogServiceOptions? options = null, IEnumerable<ILogSink>? additionalSinks = null)
    {
        _options = options ?? new LogServiceOptions { EnableConsole = true };

        var sinks = new List<ILogSink>();
        if (_options.File is not null) sinks.Add(new FileLogSink(_options.File, _options.ApplicationName));
        if (_options.EnableConsole) sinks.Add(new ConsoleLogSink());
        if (additionalSinks is not null) sinks.AddRange(additionalSinks);

        _processor = new LogProcessor(_options, sinks.ToArray());
        _defaultLogger = CreateLogger(LogConstants.DefaultCategory);
    }

    public long DroppedCount => _processor.DroppedCount;

    public string? CurrentCorrelationId => LogScope.Current?.CorrelationId;

    #region ILoggerProvider

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new CspLogger(name, ResolveLevel(name), _options.MaskSensitiveData, _processor));

    public ILogger<T> CreateLogger<T>() => new Logger<T>(new SingleProviderFactory(this));

    private LogLevel ResolveLevel(string category)
    {
        LogLevel level = _options.MinimumLevel;
        int bestMatch = -1;

        foreach (var (prefix, prefixLevel) in _options.CategoryLevels)
        {
            if (prefix.Length > bestMatch && category.StartsWith(prefix, StringComparison.Ordinal))
            {
                bestMatch = prefix.Length;
                level = prefixLevel;
            }
        }

        return level;
    }

    #endregion

    #region Kısayollar

    public IDisposable BeginCorrelation(string? correlationId = null) =>
        LogScope.Push(new[] { new KeyValuePair<string, object?>(LogConstants.CorrelationIdKey, correlationId ?? Guid.CreateVersion7().ToString("N")) });

    public void Trace(string message, params object?[] args) => _defaultLogger.Log(LogLevel.Trace, message, args);

    public void Debug(string message, params object?[] args) => _defaultLogger.Log(LogLevel.Debug, message, args);

    public void Information(string message, params object?[] args) => _defaultLogger.Log(LogLevel.Information, message, args);

    public void Warning(string message, params object?[] args) => _defaultLogger.Log(LogLevel.Warning, message, args);

    public void Error(Exception? exception, string message, params object?[] args) => _defaultLogger.Log(LogLevel.Error, exception, message, args);

    public void Critical(Exception? exception, string message, params object?[] args) => _defaultLogger.Log(LogLevel.Critical, exception, message, args);

    public Task FlushAsync(CancellationToken cancellationToken = default) => _processor.FlushAsync(cancellationToken);

    public IDisposable BeginScope(IReadOnlyDictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return LogScope.Push(properties.ToArray());
    }

    public IDisposable TimeOperation(string operationName, TimeSpan? warningThreshold = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return new OperationTimer(_defaultLogger, operationName, warningThreshold);
    }

    public async Task<T> TimeAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, TimeSpan? warningThreshold = null,
                                      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            T result = await operation(cancellationToken).ConfigureAwait(false);
            TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
            _defaultLogger.Log(warningThreshold is TimeSpan limit && elapsed >= limit ? LogLevel.Warning : LogLevel.Information,
                               "{Operation} tamamlandı ({ElapsedMs} ms)", operationName, (long)elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _defaultLogger.LogError(ex, "{Operation} başarısız ({ElapsedMs} ms)", operationName,
                                    (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _processor.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private sealed class SingleProviderFactory(LogService owner) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => owner.CreateLogger(categoryName);

        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
