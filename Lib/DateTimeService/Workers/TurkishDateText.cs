using System.Globalization;

namespace CSPLibraries.DateTimes.Workers;

internal static class TurkishDateText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] Formats =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "d.M.yyyy HH:mm", "d.M.yyyy H:mm",
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss",
        "dd-MM-yyyy", "d-M-yyyy",
        "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ssK",
        "yyyyMMdd", "yyyyMMddHHmmss",
        "d MMMM yyyy", "dd MMMM yyyy", "d MMMM yyyy HH:mm", "d MMM yyyy", "dd MMM yyyy",
        "d MMMM yyyy dddd", "dddd, d MMMM yyyy"
    ];

    internal static bool TryParse(string? text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        return DateTime.TryParseExact(trimmed, Formats, Turkish, DateTimeStyles.AllowWhiteSpaces, out value) ||
               DateTime.TryParseExact(trimmed, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value) ||
               DateTime.TryParse(trimmed, Turkish, DateTimeStyles.AllowWhiteSpaces, out value);
    }

    internal static string MonthName(int month) => Turkish.DateTimeFormat.GetMonthName(month);

    internal static string DayName(DayOfWeek day) => Turkish.DateTimeFormat.GetDayName(day);

    internal static string Humanize(DateTime value, DateTime reference)
    {
        TimeSpan difference = reference - value;
        bool past = difference >= TimeSpan.Zero;
        TimeSpan span = difference.Duration();

        if (span.TotalSeconds < 45) return past ? "az önce" : "birazdan";

        DateOnly valueDay = DateOnly.FromDateTime(value), referenceDay = DateOnly.FromDateTime(reference);
        if (span.TotalHours >= 12 || valueDay != referenceDay)
        {
            int dayDifference = referenceDay.DayNumber - valueDay.DayNumber;
            if (dayDifference == 1) return "dün";
            if (dayDifference == -1) return "yarın";
        }

        string text = span.TotalMinutes < 60 ? $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} dakika"
            : span.TotalHours < 24 ? $"{(int)Math.Round(span.TotalHours)} saat"
            : span.TotalDays < 30 ? $"{(int)Math.Round(span.TotalDays)} gün"
            : span.TotalDays < 365 ? $"{Math.Max(1, (int)Math.Round(span.TotalDays / 30.44))} ay"
            : $"{Math.Max(1, (int)Math.Round(span.TotalDays / 365.25))} yıl";

        return text + (past ? " önce" : " sonra");
    }
}
