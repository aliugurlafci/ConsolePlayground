using System.Globalization;
using CSPLibraries.DateTimes.Constants;

namespace CSPLibraries.DateTimes.Workers;

internal static class SapDateFormatter
{
    internal static string FormatDate(DateOnly date) =>
        date.ToString(DateTimeConstants.SapDateFormat, CultureInfo.InvariantCulture);

    internal static string FormatTime(TimeOnly time) =>
        time.ToString(DateTimeConstants.SapTimeFormat, CultureInfo.InvariantCulture);

    internal static DateOnly? ParseDate(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        if (value.IsEmpty || value.SequenceEqual(DateTimeConstants.SapEmptyDate)) return null;

        return DateOnly.TryParseExact(value, DateTimeConstants.SapDateFormat, CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out DateOnly date)
            ? date
            : throw new FormatException($"Geçersiz SAP tarihi: '{value.ToString()}'. Beklenen biçim: yyyyMMdd.");
    }

    internal static TimeOnly? ParseTime(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        if (value.IsEmpty) return null;

        if (value.SequenceEqual("240000")) return TimeOnly.MaxValue;

        return TimeOnly.TryParseExact(value, DateTimeConstants.SapTimeFormat, CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out TimeOnly time)
            ? time
            : throw new FormatException($"Geçersiz SAP saati: '{value.ToString()}'. Beklenen biçim: HHmmss.");
    }
}
