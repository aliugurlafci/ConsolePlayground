namespace CSPLibraries.DateTimes.Constants;

internal static class DateTimeConstants
{
    internal const string TurkeyTimeZoneId = "Europe/Istanbul";
    internal const string TurkeyWindowsTimeZoneId = "Turkey Standard Time";

    internal const string SapDateFormat = "yyyyMMdd";
    internal const string SapTimeFormat = "HHmmss";
    internal const string SapEmptyDate = "00000000";

    internal const int July15FirstYear = 2017;

    internal const int HijriSupportedMinYear = 1901;
    internal const int HijriSupportedMaxYear = 2076;

    internal const int MaxBusinessDaySearch = 3_660;

    internal static readonly TimeOnly WorkDayStart = new(9, 0);
    internal static readonly TimeOnly WorkDayEnd = new(18, 0);
}
