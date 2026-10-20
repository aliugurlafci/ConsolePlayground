using System.Globalization;

namespace CSPLibraries.Serialization.Workers;

internal static class TextValueConverter
{
    internal static string? Format(object? value, CultureInfo culture, string? format, string? dateFormat, string? dateTimeFormat) =>
        value switch
        {
            null or DBNull => null,
            string text => text,
            bool flag => flag ? "true" : "false",
            DateTime date => date.ToString(format ?? (date.TimeOfDay == TimeSpan.Zero ? dateFormat : dateTimeFormat) ?? "yyyy-MM-ddTHH:mm:ss", culture),
            DateOnly date => date.ToString(format ?? dateFormat ?? "yyyy-MM-dd", culture),
            DateTimeOffset date => date.ToString(format ?? "O", culture),
            TimeOnly time => time.ToString(format ?? "HH:mm:ss", culture),
            Enum => value.ToString(),
            IFormattable formattable => formattable.ToString(format, culture),
            _ => value.ToString()
        };

    internal static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double) ||
               type == typeof(float) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) ||
               type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
    }

    internal static Func<string, object?> CreateParser(Type targetType, CultureInfo culture, string? format)
    {
        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (type == typeof(string)) return static text => text;
        if (type.IsEnum)
            return text => text.Length == 0 ? null : Enum.Parse(type, text, ignoreCase: true);
        if (type == typeof(bool)) return static text => text.Length == 0 ? null : ParseBoolean(text);
        if (type == typeof(int))
            return text => text.Length == 0 ? null : int.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands, culture);
        if (type == typeof(long))
            return text => text.Length == 0 ? null : long.Parse(text, NumberStyles.Integer | NumberStyles.AllowThousands, culture);
        if (type == typeof(decimal))
            return text => text.Length == 0 ? null : decimal.Parse(text, NumberStyles.Number, culture);
        if (type == typeof(double))
            return text => text.Length == 0 ? null : double.Parse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture);
        if (type == typeof(DateTime))
            return format is null
                ? text => text.Length == 0 ? null : DateTime.Parse(text, culture, DateTimeStyles.RoundtripKind)
                : text => text.Length == 0 ? null : DateTime.ParseExact(text, format, culture);
        if (type == typeof(DateOnly))
            return format is null
                ? text => text.Length == 0 ? null : DateOnly.Parse(text, culture)
                : text => text.Length == 0 ? null : DateOnly.ParseExact(text, format, culture);
        if (type == typeof(TimeOnly))
            return format is null
                ? text => text.Length == 0 ? null : TimeOnly.Parse(text, culture)
                : text => text.Length == 0 ? null : TimeOnly.ParseExact(text, format, culture);
        if (type == typeof(DateTimeOffset))
            return text => text.Length == 0 ? null : DateTimeOffset.Parse(text, culture);
        if (type == typeof(TimeSpan))
            return text => text.Length == 0 ? null : TimeSpan.Parse(text, culture);
        if (type == typeof(Guid)) return static text => text.Length == 0 ? null : Guid.Parse(text);
        if (type == typeof(char)) return static text => text.Length == 0 ? null : text[0];

        return text => text.Length == 0 ? null : Convert.ChangeType(text, type, culture);
    }

    private static bool ParseBoolean(string text)
    {
        if (bool.TryParse(text, out bool result)) return result;

        return text.ToUpperInvariant() switch
        {
            "1" or "X" or "Y" or "E" or "EVET" or "YES" => true,
            "0" or "N" or "H" or "HAYIR" or "NO" => false,
            _ => throw new FormatException($"'{text}' mantıksal (bool) değere çevrilemedi.")
        };
    }
}
