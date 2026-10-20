using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Interfaces;

public interface ILogService : ILoggerProvider, IAsyncDisposable
{
    ILogger<T> CreateLogger<T>();

    IDisposable BeginCorrelation(string? correlationId = null);

    IDisposable BeginScope(IReadOnlyDictionary<string, object?> properties);

    string? CurrentCorrelationId { get; }

    void Trace(string message, params object?[] args);
    void Debug(string message, params object?[] args);
    void Information(string message, params object?[] args);
    void Warning(string message, params object?[] args);
    void Error(Exception? exception, string message, params object?[] args);
    void Critical(Exception? exception, string message, params object?[] args);

    IDisposable TimeOperation(string operationName, TimeSpan? warningThreshold = null);

    Task<T> TimeAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, TimeSpan? warningThreshold = null,
                         CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);

    long DroppedCount { get; }
}
