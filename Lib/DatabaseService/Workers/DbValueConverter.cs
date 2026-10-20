using System.Globalization;

namespace CSPLibraries.Database.Workers;

internal static class DbValueConverter
{
    internal static T Convert<T>(object value)
    {
        if (value is T typed) return typed;
        if (value is DBNull) return default!;
        return (T)ConvertTo(value, typeof(T))!;
    }

    internal static DateTime ParseDateTime(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    internal static DateOnly ParseDateOnly(string value) => DateOnly.FromDateTime(ParseDateTime(value));

    internal static TimeOnly ParseTimeOnly(string value) => TimeOnly.Parse(value, CultureInfo.InvariantCulture);

    internal static DateTimeOffset ParseDateTimeOffset(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    internal static Guid ParseGuid(string value) => Guid.Parse(value);

    internal static char ParseChar(string value) =>
        value.Length > 0 ? value[0] : throw new InvalidCastException("Boş metin char tipine dönüştürülemez.");

    internal static object? ConvertTo(object value, Type targetType)
    {
        if (value is DBNull) return null;

        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type.IsInstanceOfType(value)) return value;

        if (type.IsEnum)
        {
            return value is string name
                ? Enum.Parse(type, name.Trim(), ignoreCase: true)
                : Enum.ToObject(type, System.Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture));
        }

        if (type == typeof(string))
            return value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();

        if (type == typeof(Guid))
        {
            return value switch
            {
                string text => Guid.Parse(text),
                byte[] bytes => new Guid(bytes),
                _ => throw InvalidCast(value, type)
            };
        }

        if (type == typeof(bool) && value is string flag)
        {
            return flag.Trim().ToUpperInvariant() switch
            {
                "1" or "X" or "Y" or "E" or "TRUE" => true,
                "0" or "" or "N" or "H" or "FALSE" => false,
                _ => throw InvalidCast(value, type)
            };
        }

        if (type == typeof(char) && value is string chars && chars.Length > 0) return chars[0];

        if (type == typeof(DateOnly))
        {
            return value switch
            {
                DateTime date => DateOnly.FromDateTime(date),
                DateTimeOffset date => DateOnly.FromDateTime(date.DateTime),
                string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
                _ => throw InvalidCast(value, type)
            };
        }

        if (type == typeof(TimeOnly))
        {
            return value switch
            {
                TimeSpan time => TimeOnly.FromTimeSpan(time),
                DateTime date => TimeOnly.FromDateTime(date),
                string text => TimeOnly.Parse(text, CultureInfo.InvariantCulture),
                _ => throw InvalidCast(value, type)
            };
        }

        if (type == typeof(DateTimeOffset))
        {
            return value switch
            {
                DateTime date => new DateTimeOffset(date),
                string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture),
                _ => throw InvalidCast(value, type)
            };
        }

        if (type == typeof(DateTime) && value is string dateText)
            return DateTime.Parse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        if (type == typeof(TimeSpan) && value is string timeText)
            return TimeSpan.Parse(timeText, CultureInfo.InvariantCulture);

        return System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static InvalidCastException InvalidCast(object value, Type type) =>
        new($"{value.GetType().Name} değeri '{value}' {type.Name} tipine dönüştürülemedi.");
}
