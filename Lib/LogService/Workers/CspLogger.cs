using CSPLibraries.Logging.Constants;
using CSPLibraries.Logging.Models;
using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Workers;

internal sealed class CspLogger(string category, LogLevel minimumLevel, bool maskSensitiveData, LogProcessor processor) : ILogger
{
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => LogScope.Push(state);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                            Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = formatter(state, exception);
        string? exceptionText = exception?.ToString();
        string? template = null;
        IReadOnlyList<KeyValuePair<string, object?>> properties = [];

        LogScope? scope = LogScope.Current;

        if (state is IReadOnlyList<KeyValuePair<string, object?>> values && values.Count > 0)
        {
            var array = new KeyValuePair<string, object?>[values.Count];
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                KeyValuePair<string, object?> pair = values[i];
                if (pair.Key == LogConstants.OriginalFormatKey) template = pair.Value as string;
                else array[count++] = maskSensitiveData ? MaskProperty(pair) : pair;
            }

            properties = count == array.Length ? array : new ArraySegment<KeyValuePair<string, object?>>(array, 0, count);
        }

        if (scope is not null)
            properties = MergeScopes(scope, properties, maskSensitiveData);

        if (maskSensitiveData)
        {
            message = SensitiveDataMasker.Mask(message);
            if (exceptionText is not null) exceptionText = SensitiveDataMasker.Mask(exceptionText);
            if (template is not null) template = SensitiveDataMasker.MaskTemplate(template);
        }

        processor.Enqueue(new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = logLevel,
            Category = category,
            EventId = eventId,
            Message = message,
            MessageTemplate = template,
            Exception = exceptionText,
            CorrelationId = scope?.CorrelationId,
            Properties = properties
        });
    }

    private static KeyValuePair<string, object?> MaskProperty(KeyValuePair<string, object?> pair) =>
        pair.Value is null ? pair
        : SensitiveDataMasker.IsSensitiveKey(pair.Key) ? new(pair.Key, LogConstants.MaskedValue)
        : pair.Value is string text ? new(pair.Key, SensitiveDataMasker.Mask(text))
        : pair;

    private static IReadOnlyList<KeyValuePair<string, object?>> MergeScopes(LogScope scope, IReadOnlyList<KeyValuePair<string, object?>> own, bool mask)
    {
        List<KeyValuePair<string, object?>>? merged = null;
        for (LogScope? current = scope; current is not null; current = current.Parent)
        {
            foreach (KeyValuePair<string, object?> pair in current.Properties)
            {
                if (pair.Key == LogConstants.CorrelationIdKey) continue;

                merged ??= new List<KeyValuePair<string, object?>>(own);
                if (!merged.Exists(p => p.Key == pair.Key))
                    merged.Add(mask ? MaskProperty(pair) : pair);
            }
        }

        return merged ?? own;
    }
}
