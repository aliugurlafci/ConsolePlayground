using CSPLibraries.DateTimes.Models;

namespace CSPLibraries.DateTimes.Interfaces;

public interface IDateTimeService
{
    TimeZoneInfo TimeZone { get; }

    DateTimeOffset UtcNow { get; }

    DateTime Now { get; }

    DateOnly Today { get; }

    DateTime ToLocal(DateTime utcDateTime);
    DateTime ToLocal(DateTimeOffset dateTimeOffset);
    DateTime ToUtc(DateTime localDateTime);

    bool IsWeekend(DateOnly date);
    bool IsHoliday(DateOnly date);
    Holiday? GetHoliday(DateOnly date);
    IReadOnlyList<Holiday> GetHolidays(int year);

    bool IsBusinessDay(DateOnly date);
    DateOnly NextBusinessDay(DateOnly date);
    DateOnly PreviousBusinessDay(DateOnly date);

    DateOnly AddBusinessDays(DateOnly date, int businessDays);

    int CountBusinessDays(DateOnly start, DateOnly end);

    DateOnly StartOfWeek(DateOnly date);
    DateOnly EndOfWeek(DateOnly date);
    DateOnly StartOfMonth(DateOnly date);
    DateOnly EndOfMonth(DateOnly date);
    int GetQuarter(DateOnly date);
    int GetIsoWeek(DateOnly date);
    int CalculateAge(DateOnly birthDate, DateOnly? onDate = null);

    DateOnly FirstBusinessDayOfMonth(int year, int month);
    DateOnly LastBusinessDayOfMonth(int year, int month);
    IEnumerable<DateOnly> EachDay(DateOnly start, DateOnly end);
    IEnumerable<DateOnly> EachBusinessDay(DateOnly start, DateOnly end);
    bool IsWithinWorkingHours(DateTime localTime, TimeOnly? dayStart = null, TimeOnly? dayEnd = null);
    DateTime AddWorkingTime(DateTime localStart, TimeSpan duration, TimeOnly? dayStart = null, TimeOnly? dayEnd = null);

    string Humanize(DateTime value, DateTime? relativeTo = null);
    bool TryParseTurkish(string? text, out DateTime value);
    string GetMonthName(int month);
    string GetDayName(DayOfWeek day);

    string ToSapDate(DateOnly date);
    string ToSapTime(TimeOnly time);
    DateOnly? ParseSapDate(string? value);
    TimeOnly? ParseSapTime(string? value);
    DateTime? ParseSapDateTime(string? date, string? time);
}
