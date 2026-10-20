using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using CSPLibraries.Mail.Constants;

namespace CSPLibraries.Mail.Workers;

internal static class MailTemplateRenderer
{
    private static readonly ConcurrentDictionary<string, Segment[]> Templates = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Properties = new();

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    internal static string Render(string template, object? model, CultureInfo culture)
    {
        Segment[] segments = Templates.TryGetValue(template, out Segment[]? cached) ? cached : Parse(template);
        var builder = new StringBuilder(template.Length + 256);

        foreach (Segment segment in segments)
        {
            if (segment.Path is null)
            {
                builder.Append(segment.Text);
                continue;
            }

            string value = Format(Resolve(model, segment.Path), culture);
            builder.Append(segment.Raw ? value : Encoder.Encode(value));
        }

        return builder.ToString();
    }

    private static Segment[] Parse(string template)
    {
        var segments = new List<Segment>();
        int position = 0;

        while (position < template.Length)
        {
            int start = template.IndexOf("{{", position, StringComparison.Ordinal);
            if (start < 0) break;

            bool raw = start + 2 < template.Length && template[start + 2] == '{';
            string close = raw ? "}}}" : "}}";
            int contentStart = start + (raw ? 3 : 2);
            int end = template.IndexOf(close, contentStart, StringComparison.Ordinal);
            if (end < 0) break;

            if (start > position) segments.Add(new Segment(template[position..start], null, false));
            segments.Add(new Segment(null, template[contentStart..end].Trim().Split('.'), raw));
            position = end + close.Length;
        }

        if (position < template.Length) segments.Add(new Segment(template[position..], null, false));

        Segment[] result = segments.ToArray();
        if (Templates.Count < MailConstants.MaxCachedTemplates)
            Templates.TryAdd(template, result);

        return result;
    }

    private static object? Resolve(object? model, string[] path)
    {
        object? current = model;
        foreach (string part in path)
        {
            current = current switch
            {
                null => null,
                IReadOnlyDictionary<string, object?> readOnly => readOnly.TryGetValue(part, out object? v) ? v : null,
                IDictionary<string, object?> dictionary => dictionary.TryGetValue(part, out object? v) ? v : null,
                IDictionary<string, string> strings => strings.TryGetValue(part, out string? v) ? v : null,
                _ => Properties.GetOrAdd((current.GetType(), part),
                        static key => key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase))
                    ?.GetValue(current)
            };
        }

        return current;
    }

    private static string Format(object? value, CultureInfo culture) => value switch
    {
        null => string.Empty,
        string text => text,
        DateTime date => date.TimeOfDay == TimeSpan.Zero ? date.ToString("d", culture) : date.ToString("g", culture),
        DateOnly date => date.ToString("d", culture),
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString() ?? string.Empty
    };

    private sealed record Segment(string? Text, string[]? Path, bool Raw);
}
