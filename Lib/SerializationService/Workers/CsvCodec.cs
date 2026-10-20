using System.Data;
using CSPLibraries.Serialization.Datas;
using CSPLibraries.Serialization.Models;

namespace CSPLibraries.Serialization.Workers;

internal static class CsvCodec
{
    #region Yazma

    internal static void Write<T>(TextWriter writer, IEnumerable<T> rows, CsvOptions options)
    {
        var csv = new CsvRowWriter<T>(writer, options);
        if (options.HasHeader) csv.WriteHeader();
        foreach (T row in rows)
            csv.WriteRow(row);
    }

    #endregion

    #region Okuma

    internal static IEnumerable<T> Read<T>(TextReader reader, CsvOptions options) where T : new()
    {
        using var parser = new CsvParser(reader, options.Delimiter);
        Binding<T>[] bindings;

        if (options.HasHeader)
        {
            if (!parser.TryReadRecord()) yield break;
            bindings = BindByHeader<T>(parser, options);
        }
        else
        {
            bindings = RecordMetadata<T>.CsvFields
                .Where(f => f.Setter is not null)
                .Select((f, i) => new Binding<T>(i, f, CsvAccessors.CreateSetter(f, options.Culture, null)))
                .ToArray();
        }

        while (parser.TryReadRecord())
        {
            if (options.SkipEmptyLines && parser.IsEmptyLine) continue;
            yield return MapRecord(parser, bindings, options.TrimValues);
        }
    }

    internal static IEnumerable<string[]> ReadRecords(TextReader reader, char delimiter, bool skipEmptyLines)
    {
        using var parser = new CsvParser(reader, delimiter);

        while (parser.TryReadRecord())
        {
            if (skipEmptyLines && parser.IsEmptyLine) continue;

            var fields = new string[parser.FieldCount];
            for (int i = 0; i < fields.Length; i++)
                fields[i] = parser.GetString(i);

            yield return fields;
        }
    }

    internal static DataTable ReadDataTable(TextReader reader, CsvOptions options)
    {
        using var parser = new CsvParser(reader, options.Delimiter);
        var table = new DataTable();

        if (!parser.TryReadRecord()) return table;

        if (options.HasHeader)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parser.FieldCount; i++)
            {
                string name = parser[i].Trim().TrimStart('﻿').ToString();
                if (name.Length == 0) name = "Column" + (i + 1);
                string unique = name;
                for (int n = 2; !used.Add(unique); n++) unique = name + "_" + n;
                table.Columns.Add(unique, typeof(string));
            }
        }
        else
        {
            for (int i = 0; i < parser.FieldCount; i++) table.Columns.Add("Column" + (i + 1), typeof(string));
            AddRow(table, parser, options.TrimValues);
        }

        table.BeginLoadData();
        while (parser.TryReadRecord())
        {
            if (options.SkipEmptyLines && parser.IsEmptyLine) continue;
            AddRow(table, parser, options.TrimValues);
        }

        table.EndLoadData();
        return table;
    }

    private static void AddRow(DataTable table, CsvParser parser, bool trim)
    {
        var values = new object?[table.Columns.Count];
        for (int i = 0; i < values.Length && i < parser.FieldCount; i++)
            values[i] = (trim ? parser[i].Trim() : parser[i]).ToString();

        table.LoadDataRow(values, fAcceptChanges: true);
    }

    internal static char DetectDelimiter(ReadOnlySpan<char> sample)
    {
        ReadOnlySpan<char> candidates = [';', ',', '\t', '|'];
        char best = ',';
        int bestScore = 0;

        foreach (char candidate in candidates)
        {
            int minCount = int.MaxValue, lines = 0, current = 0;
            bool inQuotes = false;

            foreach (char c in sample)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (!inQuotes && c == candidate) current++;
                else if (!inQuotes && c == '\n')
                {
                    if (current > 0 || lines == 0) minCount = Math.Min(minCount, current);
                    lines++;
                    current = 0;
                    if (lines >= 20) break;
                }
            }

            if (current > 0) minCount = Math.Min(minCount, current);
            int score = minCount == int.MaxValue ? 0 : minCount;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static Binding<T>[] BindByHeader<T>(CsvParser headers, CsvOptions options)
    {
        var byName = new Dictionary<string, RecordField<T>>(StringComparer.Ordinal);
        foreach (RecordField<T> field in RecordMetadata<T>.CsvFields)
        {
            if (field.Setter is not null)
                byName.TryAdd(field.NormalizedName, field);
        }

        var bindings = new List<Binding<T>>();
        for (int i = 0; i < headers.FieldCount; i++)
        {
            string header = headers[i].Trim().TrimStart('﻿').ToString();
            if (byName.Remove(RecordMetadata<T>.NormalizeName(header), out RecordField<T>? field))
                bindings.Add(new Binding<T>(i, field, CsvAccessors.CreateSetter(field, options.Culture, null)));
        }

        return bindings.ToArray();
    }

    private static T MapRecord<T>(CsvParser parser, Binding<T>[] bindings, bool trim) where T : new()
    {
        var item = new T();

        foreach (Binding<T> binding in bindings)
        {
            if (binding.Index >= parser.FieldCount) continue;

            ReadOnlySpan<char> text = trim ? parser[binding.Index].Trim() : parser[binding.Index];
            try
            {
                binding.Setter(item, text);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
            {
                throw new SerializationServiceException($"'{text.ToString()}' değeri {binding.Field.Type.Name} tipine dönüştürülemedi.",
                                                        parser.LineNumber, binding.Field.Name, ex);
            }
        }

        return item;
    }

    private sealed record Binding<T>(int Index, RecordField<T> Field, SpanSetter<T> Setter);

    #endregion
}
