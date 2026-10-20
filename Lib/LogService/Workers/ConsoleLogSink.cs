using System.Text;
using CSPLibraries.Logging.Interfaces;
using CSPLibraries.Logging.Models;
using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Workers;

public sealed class ConsoleLogSink : ILogSink
{
    private readonly StringBuilder _builder = new(4096);
    private readonly bool _useColors = !Console.IsOutputRedirected;

    public ValueTask WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        if (!_useColors)
        {
            _builder.Clear();
            foreach (LogEntry entry in batch)
                Append(entry);

            Console.Out.Write(_builder);
            return ValueTask.CompletedTask;
        }

        foreach (LogEntry entry in batch)
        {
            _builder.Clear();
            Console.ForegroundColor = entry.Level switch
            {
                LogLevel.Critical or LogLevel.Error => ConsoleColor.Red,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Debug or LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.Gray
            };
            Append(entry);
            Console.Out.Write(_builder);
        }

        Console.ResetColor();
        return ValueTask.CompletedTask;
    }

    private void Append(LogEntry entry)
    {
        _builder.Append(entry.Timestamp.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" [").Append(entry.LevelShortName).Append("] ")
                .Append(entry.Category).Append(": ").Append(entry.Message);

        if (entry.CorrelationId is not null) _builder.Append(" [").Append(entry.CorrelationId).Append(']');
        if (entry.Exception is not null) _builder.AppendLine().Append(entry.Exception);
        _builder.AppendLine();
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => new(Console.Out.FlushAsync(cancellationToken));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
