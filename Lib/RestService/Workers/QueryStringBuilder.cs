using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CSPLibraries.Rest.Workers;

internal static class QueryStringBuilder
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    internal static string? Build(object? source)
    {
        if (source is null) return null;
        if (source is string raw) return raw.Length == 0 ? null : raw.TrimStart('?');

        var builder = new StringBuilder(128);

        foreach (var (key, value) in EnumeratePairs(source))
        {
            if (value is null) continue;

            if (value is IEnumerable items and not string and not byte[])
            {
                foreach (object? item in items)
                {
                    if (item is not null)
                        Append(builder, key, PayloadCodec.FormatScalar(item));
                }
            }
            else
            {
                Append(builder, key, PayloadCodec.FormatScalar(value));
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    internal static string? Combine(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first)) return second;
        if (string.IsNullOrEmpty(second)) return first;
        return first + "&" + second;
    }

    private static void Append(StringBuilder builder, string key, string value)
    {
        if (builder.Length > 0) builder.Append('&');
        builder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumeratePairs(object source)
    {
        switch (source)
        {
            case IEnumerable<KeyValuePair<string, string>> stringPairs:
                foreach (var entry in stringPairs)
                    yield return new(entry.Key, entry.Value);
                yield break;

            case IEnumerable<KeyValuePair<string, object?>> objectPairs:
                foreach (var entry in objectPairs)
                    yield return entry;
                yield break;

            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                        yield return new(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!, entry.Value);
                }
                yield break;
        }

        foreach (PropertyInfo property in GetReadableProperties(source.GetType()))
            yield return new(property.Name, property.GetValue(source));
    }

    private static PropertyInfo[] GetReadableProperties(Type type) =>
        Properties.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray());
}
