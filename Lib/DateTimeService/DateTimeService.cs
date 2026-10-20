using System.Globalization;
using CSPLibraries.DateTimes.Constants;
using CSPLibraries.DateTimes.Datas;
using CSPLibraries.DateTimes.Interfaces;
using CSPLibraries.DateTimes.Models;
using CSPLibraries.DateTimes.Workers;

namespace CSPLibraries.DateTimes;

public sealed class DateTimeService : IDateTimeService
{
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeServiceOptions _options;
    private readonly TurkishHolidayCalendar _calendar;

    public DateTimeService(DateTimeServiceOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? DateTimeServiceOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _calendar = new TurkishHolidayCalendar(_options);
        TimeZone = FindTimeZone(_options.TimeZoneId);
    }

    private static TimeZoneInfo FindTimeZone(string id)
    {
#if NET6_0_OR_GREATER
        return TimeZoneInfo.FindSystemTimeZoneById(id);
#else
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (string.Equals(id, DateTimeConstants.TurkeyTimeZoneId, StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DateTimeConstants.TurkeyWindowsTimeZoneId);
        }
#endif
    }

    public TimeZoneInfo TimeZone { get; }

    #region Şimdiki zaman ve saat dilimi

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateTime Now => TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(Now);

    public DateTime ToLocal(DateTime utcDateTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            utcDateTime.Kind == DateTimeKind.Utc ? utcDateTime : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), TimeZone);

    public DateTime ToLocal(DateTimeOffset dateTimeOffset) => TimeZoneInfo.ConvertTime(dateTimeOffset, TimeZone).DateTime;

    public DateTime ToUtc(DateTime localDateTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), TimeZone);

    #endregion

    #region Tatil ve iş günü

    public bool IsWeekend(DateOnly date) => _options.WeekendDays.Contains(date.DayOfWeek);

    public bool IsHoliday(DateOnly date)
    {
        Holiday? holiday = _calendar.Find(date);
        return holiday is not null && (!holiday.IsHalfDay || _options.TreatHalfDaysAsHolidays);
    }

    public Holiday? GetHoliday(DateOnly date) => _calendar.Find(date);

    public IReadOnlyList<Holiday> GetHolidays(int year) =>
        _calendar.GetYear(year).Values.OrderBy(h => h.Date).ToList();

    public bool IsBusinessDay(DateOnly date) => !IsWeekend(date) && !IsHoliday(date);

    public DateOnly NextBusinessDay(DateOnly date) => AddBusinessDays(date, 1);

    public DateOnly PreviousBusinessDay(DateOnly date) => AddBusinessDays(date, -1);

    public DateOnly AddBusinessDays(DateOnly date, int businessDays)
    {
        int step = businessDays >= 0 ? 1 : -1;
        int remaining = Math.Abs(businessDays);
        DateOnly current = date;
        int guard = 0;

        while (remaining > 0)
        {
            current = current.AddDays(step);
            if (IsBusinessDay(current)) remaining--;

            if (++guard > DateTimeConstants.MaxBusinessDaySearch + Math.Abs(businessDays) * 2)
                throw new InvalidOperationException("İş günü bulunamadı; hafta sonu/tatil ayarlarını kontrol edin.");
        }

        return current;
    }

    public int CountBusinessDays(DateOnly start, DateOnly end)
    {
        if (end <= start) return 0;

        int count = 0;
        for (DateOnly day = start; day < end; day = day.AddDays(1))
        {
            if (IsBusinessDay(day)) count++;
        }

        return count;
    }

    #endregion

    #region Takvim yardımcıları

    public DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public DateOnly EndOfWeek(DateOnly date) => StartOfWeek(date).AddDays(6);

    public DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    public DateOnly EndOfMonth(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    public int GetQuarter(DateOnly date) => (date.Month + 2) / 3;

    public int GetIsoWeek(DateOnly date) => ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

    public int CalculateAge(DateOnly birthDate, DateOnly? onDate = null)
    {
        DateOnly reference = onDate ?? Today;
        int age = reference.Year - birthDate.Year;
        if (reference < birthDate.AddYears(age)) age--;
        return age;
    }

    #endregion

    #region Mesai, aralıklar, metin

    public DateOnly FirstBusinessDayOfMonth(int year, int month)
    {
        var day = new DateOnly(year, month, 1);
        return IsBusinessDay(day) ? day : AddBusinessDays(day, 1);
    }

    public DateOnly LastBusinessDayOfMonth(int year, int month)
    {
        var day = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return IsBusinessDay(day) ? day : AddBusinessDays(day, -1);
    }

    public IEnumerable<DateOnly> EachDay(DateOnly start, DateOnly end)
    {
        for (DateOnly day = start; day <= end; day = day.AddDays(1))
            yield return day;
    }

    public IEnumerable<DateOnly> EachBusinessDay(DateOnly start, DateOnly end) => EachDay(start, end).Where(IsBusinessDay);

    public bool IsWithinWorkingHours(DateTime localTime, TimeOnly? dayStart = null, TimeOnly? dayEnd = null)
    {
        TimeOnly start = dayStart ?? DateTimeConstants.WorkDayStart;
        TimeOnly end = dayEnd ?? DateTimeConstants.WorkDayEnd;
        var time = TimeOnly.FromDateTime(localTime);
        return IsBusinessDay(DateOnly.FromDateTime(localTime)) && time >= start && time < end;
    }

    public DateTime AddWorkingTime(DateTime localStart, TimeSpan duration, TimeOnly? dayStart = null, TimeOnly? dayEnd = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        TimeOnly start = dayStart ?? DateTimeConstants.WorkDayStart;
        TimeOnly end = dayEnd ?? DateTimeConstants.WorkDayEnd;
        if (end <= start) throw new ArgumentException("Mesai bitişi başlangıçtan sonra olmalıdır.", nameof(dayEnd));

        DateTime current = localStart;
        TimeSpan remaining = duration;

        for (int guard = 0; guard < DateTimeConstants.MaxBusinessDaySearch; guard++)
        {
            var date = DateOnly.FromDateTime(current);
            var time = TimeOnly.FromDateTime(current);

            if (!IsBusinessDay(date) || time >= end)
            {
                current = NextBusinessDay(date).ToDateTime(start);
                continue;
            }

            if (time < start) current = date.ToDateTime(start);

            TimeSpan available = date.ToDateTime(end) - current;
            if (remaining <= available) return current + remaining;

            remaining -= available;
            current = NextBusinessDay(date).ToDateTime(start);
        }

        throw new InvalidOperationException("Mesai süresi hesaplanamadı; tatil/hafta sonu ayarlarını kontrol edin.");
    }

    public string Humanize(DateTime value, DateTime? relativeTo = null) => TurkishDateText.Humanize(value, relativeTo ?? Now);

    public bool TryParseTurkish(string? text, out DateTime value) => TurkishDateText.TryParse(text, out value);

    public string GetMonthName(int month) => TurkishDateText.MonthName(month);

    public string GetDayName(DayOfWeek day) => TurkishDateText.DayName(day);

    #endregion

    #region SAP biçimleri

    public string ToSapDate(DateOnly date) => SapDateFormatter.FormatDate(date);

    public string ToSapTime(TimeOnly time) => SapDateFormatter.FormatTime(time);

    public DateOnly? ParseSapDate(string? value) => value is null ? null : SapDateFormatter.ParseDate(value);

    public TimeOnly? ParseSapTime(string? value) => value is null ? null : SapDateFormatter.ParseTime(value);

    public DateTime? ParseSapDateTime(string? date, string? time)
    {
        DateOnly? day = ParseSapDate(date);
        return day?.ToDateTime(ParseSapTime(time) ?? TimeOnly.MinValue);
    }

    #endregion
}
