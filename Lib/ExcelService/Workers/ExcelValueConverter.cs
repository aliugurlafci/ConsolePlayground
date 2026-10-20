using System.Globalization;

namespace CSPLibraries.Excel.Workers;

internal static class ExcelValueConverter
{
    internal static object? Convert(object value, Type targetType, CultureInfo culture)
    {
        if (targetType.IsInstanceOfType(value)) return value;

        if (targetType == typeof(string))
            return value is IFormattable formattable ? formattable.ToString(null, culture) : value.ToString();

        return value switch
        {
            string text => FromString(text, targetType, culture),
            double number => FromDouble(number, targetType, culture),
            DateTime date => FromDateTime(date, targetType, culture),
            _ => System.Convert.ChangeType(value, targetType, culture)
        };
    }

    private static object? FromString(string text, Type targetType, CultureInfo culture)
    {
        text = text.Trim();
        if (text.Length == 0) return null;

        if (targetType.IsEnum) return Enum.Parse(targetType, text, ignoreCase: true);
        if (targetType == typeof(bool)) return ParseBoolean(text);
        if (targetType == typeof(Guid)) return Guid.Parse(text);
        if (targetType == typeof(DateTime)) return DateTime.Parse(text, culture);
        if (targetType == typeof(DateOnly)) return DateOnly.FromDateTime(DateTime.Parse(text, culture));
        if (targetType == typeof(TimeOnly)) return TimeOnly.Parse(text, culture);
        if (targetType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(text, culture);
        if (targetType == typeof(TimeSpan)) return TimeSpan.Parse(text, culture);

        return System.Convert.ChangeType(text, targetType, culture);
    }

    private static object FromDouble(double number, Type targetType, CultureInfo culture)
    {
        if (targetType == typeof(DateTime)) return DateTime.FromOADate(number);
        if (targetType == typeof(DateOnly)) return DateOnly.FromDateTime(DateTime.FromOADate(number));
        if (targetType == typeof(TimeOnly)) return TimeOnly.FromTimeSpan(TimeSpan.FromDays(number % 1));
        if (targetType == typeof(TimeSpan)) return TimeSpan.FromDays(number);
        if (targetType == typeof(DateTimeOffset)) return new DateTimeOffset(DateTime.FromOADate(number));
        if (targetType == typeof(bool)) return number != 0;
        if (targetType.IsEnum) return Enum.ToObject(targetType, (long)number);

        return System.Convert.ChangeType(number, targetType, culture);
    }

    private static object FromDateTime(DateTime date, Type targetType, CultureInfo culture)
    {
        if (targetType == typeof(DateOnly)) return DateOnly.FromDateTime(date);
        if (targetType == typeof(TimeOnly)) return TimeOnly.FromDateTime(date);
        if (targetType == typeof(TimeSpan)) return date.TimeOfDay;
        if (targetType == typeof(DateTimeOffset)) return new DateTimeOffset(date);
        if (targetType == typeof(double)) return date.ToOADate();

        return System.Convert.ChangeType(date, targetType, culture);
    }

    private static bool ParseBoolean(string text)
    {
        if (bool.TryParse(text, out bool result)) return result;

        return text.ToUpperInvariant() switch
        {
            "1" or "X" or "E" or "Y" or "EVET" or "YES" or "DOĞRU" => true,
            "0" or "H" or "N" or "HAYIR" or "HAYİR" or "NO" or "YANLIŞ" or "YANLIS" => false,
            _ => throw new FormatException($"'{text}' mantıksal (bool) değere çevrilemedi.")
        };
    }
}
