using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Models;

public sealed class LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required LogLevel Level { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public EventId EventId { get; init; }

    public string? MessageTemplate { get; init; }

    public string? Exception { get; init; }

    public string? CorrelationId { get; init; }

    public IReadOnlyList<KeyValuePair<string, object?>> Properties { get; init; } = [];

    public string LevelShortName => Level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "---"
    };
}
