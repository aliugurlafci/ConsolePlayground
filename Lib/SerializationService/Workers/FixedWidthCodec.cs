using CSPLibraries.Serialization.Datas;
using CSPLibraries.Serialization.Models;

namespace CSPLibraries.Serialization.Workers;

internal static class FixedWidthCodec
{
    internal static int GetRecordLength<T>()
    {
        FixedWidthFieldAttribute last = RecordMetadata<T>.FixedWidthFields.Value[^1].FixedWidth!;
        return last.Start - 1 + last.Length;
    }

    internal static void WriteRecord<T>(TextWriter writer, T row, char[] line, FixedWidthOptions options, long lineNumber)
    {
        Array.Fill(line, ' ');

        foreach (RecordField<T> field in RecordMetadata<T>.FixedWidthFields.Value)
        {
            FixedWidthFieldAttribute spec = field.FixedWidth!;
            object? value = row is null ? null : field.Getter?.Invoke(row);
            string text = TextValueConverter.Format(value, options.Culture, spec.Format, null, null) ?? string.Empty;

            if (text.Length > spec.Length)
            {
                if (!options.TruncateOverflow)
                    throw new SerializationServiceException(
                        $"'{text}' değeri {spec.Length} karakterlik alana sığmıyor ({text.Length} karakter).", lineNumber, field.Name);

                text = text[..spec.Length];
            }

            Span<char> target = line.AsSpan(spec.Start - 1, spec.Length);
            bool rightAlign = spec.Alignment == FieldAlignment.Right ||
                              (spec.Alignment == FieldAlignment.Auto && TextValueConverter.IsNumeric(field.Type));

            if (!rightAlign)
            {
                text.AsSpan().CopyTo(target);
                target[text.Length..].Fill(spec.PadChar);
                continue;
            }

            int padding = spec.Length - text.Length;
            if (spec.PadChar == '0' && text.StartsWith('-'))
            {
                target[0] = '-';
                target[1..(padding + 1)].Fill('0');
                text.AsSpan(1).CopyTo(target[(padding + 1)..]);
            }
            else
            {
                target[..padding].Fill(spec.PadChar);
                text.AsSpan().CopyTo(target[padding..]);
            }
        }

        writer.Write(line);
        writer.Write(options.NewLine);
    }

    internal static IEnumerable<T> Read<T>(TextReader reader, FixedWidthOptions options) where T : new()
    {
        RecordField<T>[] fields = RecordMetadata<T>.FixedWidthFields.Value;
        Func<string, object?>[] parsers = fields
            .Select(f => TextValueConverter.CreateParser(f.Type, options.Culture, f.FixedWidth!.Format))
            .ToArray();

        long lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (options.SkipEmptyLines && string.IsNullOrWhiteSpace(line)) continue;

            var item = new T();
            for (int i = 0; i < fields.Length; i++)
            {
                RecordField<T> field = fields[i];
                FixedWidthFieldAttribute spec = field.FixedWidth!;
                if (field.Setter is null || spec.Start > line.Length) continue;

                ReadOnlySpan<char> raw = line.AsSpan(spec.Start - 1, Math.Min(spec.Length, line.Length - spec.Start + 1));
                string text = Unpad(raw, spec, field, options.TrimValues);

                object? value;
                try
                {
                    value = parsers[i](text);
                }
                catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
                {
                    throw new SerializationServiceException($"'{text}' değeri {field.Type.Name} tipine dönüştürülemedi.", lineNumber, field.Name, ex);
                }

                if (value is null && !field.AcceptsNull) continue;
                field.Setter(item, value);
            }

            yield return item;
        }
    }

    private static string Unpad<T>(ReadOnlySpan<char> raw, FixedWidthFieldAttribute spec, RecordField<T> field, bool trim)
    {
        if (spec.PadChar != ' ' && !TextValueConverter.IsNumeric(field.Type))
        {
            bool rightAlign = spec.Alignment == FieldAlignment.Right;
            raw = rightAlign ? raw.TrimStart(spec.PadChar) : raw.TrimEnd(spec.PadChar);
        }

        if (trim) raw = raw.Trim();
        return raw.ToString();
    }
}
