using CSPLibraries.Logging.Constants;

namespace CSPLibraries.Logging.Workers;

internal sealed class LogScope : IDisposable
{
    private static readonly AsyncLocal<LogScope?> CurrentScope = new();

    private readonly LogScope? _parent;
    private bool _disposed;

    private LogScope(LogScope? parent, IReadOnlyList<KeyValuePair<string, object?>> properties, string? correlationId)
    {
        _parent = parent;
        Properties = properties;
        CorrelationId = correlationId ?? parent?.CorrelationId;
    }

    internal static LogScope? Current => CurrentScope.Value;

    internal IReadOnlyList<KeyValuePair<string, object?>> Properties { get; }

    internal string? CorrelationId { get; }

    internal LogScope? Parent => _parent;

    internal static LogScope Push(object? state)
    {
        IReadOnlyList<KeyValuePair<string, object?>> properties = state switch
        {
            IReadOnlyList<KeyValuePair<string, object?>> list => list.Where(p => p.Key != LogConstants.OriginalFormatKey).ToArray(),
            IEnumerable<KeyValuePair<string, object?>> pairs => pairs.Where(p => p.Key != LogConstants.OriginalFormatKey).ToArray(),
            null => [],
            _ => [new KeyValuePair<string, object?>("Scope", state.ToString())]
        };

        string? correlationId = properties.FirstOrDefault(p => p.Key == LogConstants.CorrelationIdKey).Value?.ToString();
        var scope = new LogScope(CurrentScope.Value, properties, correlationId);
        CurrentScope.Value = scope;
        return scope;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (ReferenceEquals(CurrentScope.Value, this))
            CurrentScope.Value = _parent;
    }
}
