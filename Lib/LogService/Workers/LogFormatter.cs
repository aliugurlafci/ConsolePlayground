using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSPLibraries.Logging.Models;

namespace CSPLibraries.Logging.Workers;

internal static class LogFormatter
{
    internal static readonly JsonWriterOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        SkipValidation = true
    };

    internal static void WriteText(LogEntry entry, ArrayBufferWriter<byte> buffer, StringBuilder scratch)
    {
        scratch.Clear();
#if NET6_0_OR_GREATER
        scratch.Append(CultureInfo.InvariantCulture, $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.LevelShortName}] {entry.Category}: ")
               .Append(entry.Message);
#else
        scratch.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
               .Append(" [").Append(entry.LevelShortName).Append("] ").Append(entry.Category).Append(": ")
               .Append(entry.Message);
#endif

        if (entry.CorrelationId is not null) scratch.Append(" [").Append(entry.CorrelationId).Append(']');
        if (entry.Exception is not null) scratch.Append('\n').Append(entry.Exception);
        scratch.Append('\n');

        foreach (ReadOnlyMemory<char> chunk in scratch.GetChunks())
        {
            int written = Encoding.UTF8.GetBytes(chunk.Span, buffer.GetSpan(Encoding.UTF8.GetMaxByteCount(chunk.Length)));
            buffer.Advance(written);
        }
    }

    internal static void WriteJson(LogEntry entry, Utf8JsonWriter writer, ArrayBufferWriter<byte> buffer, string application, string machine)
    {
        writer.Reset(buffer);
        writer.WriteStartObject();
        writer.WriteString("@t", entry.Timestamp);
        writer.WriteString("@l", entry.Level.ToString());
        writer.WriteString("@m", entry.Message);
        if (entry.MessageTemplate is not null) writer.WriteString("@mt", entry.MessageTemplate);
        if (entry.Exception is not null) writer.WriteString("@x", entry.Exception);

        writer.WriteString("Category", entry.Category);
        if (entry.EventId.Id != 0) writer.WriteNumber("EventId", entry.EventId.Id);
        if (entry.CorrelationId is not null) writer.WriteString("CorrelationId", entry.CorrelationId);
        writer.WriteString("Application", application);
        writer.WriteString("Machine", machine);

        foreach (var (key, value) in entry.Properties)
        {
            if (key is "CorrelationId" or "Category" or "Application" or "Machine" || key.StartsWith('@')) continue;
            writer.WritePropertyName(key);
            WriteValue(writer, value);
        }

        writer.WriteEndObject();
        writer.Flush();
        buffer.Write("\n"u8);
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string text: writer.WriteStringValue(text); break;
            case bool flag: writer.WriteBooleanValue(flag); break;
            case int number: writer.WriteNumberValue(number); break;
            case long number: writer.WriteNumberValue(number); break;
            case decimal number: writer.WriteNumberValue(number); break;
            case double number when double.IsFinite(number): writer.WriteNumberValue(number); break;
            case DateTime date: writer.WriteStringValue(date); break;
            case DateTimeOffset date: writer.WriteStringValue(date); break;
            case Guid guid: writer.WriteStringValue(guid); break;
            case TimeSpan time: writer.WriteNumberValue(time.TotalMilliseconds); break;
            case Enum: writer.WriteStringValue(value.ToString()); break;
            case IFormattable formattable: writer.WriteStringValue(formattable.ToString(null, CultureInfo.InvariantCulture)); break;
            default: writer.WriteStringValue(value.ToString()); break;
        }
    }
}
