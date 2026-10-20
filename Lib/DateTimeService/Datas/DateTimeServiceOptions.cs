using CSPLibraries.DateTimes.Constants;

namespace CSPLibraries.DateTimes.Datas;

public sealed class DateTimeServiceOptions
{
    public static DateTimeServiceOptions Default { get; } = new();

    public string TimeZoneId { get; init; } = DateTimeConstants.TurkeyTimeZoneId;

#if NET5_0_OR_GREATER
    public IReadOnlySet<DayOfWeek> WeekendDays { get; init; } = new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
#else
    public IReadOnlyCollection<DayOfWeek> WeekendDays { get; init; } = new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
#endif

    public bool IncludeReligiousHolidays { get; init; } = true;

    public bool TreatHalfDaysAsHolidays { get; init; }

    public IReadOnlyCollection<DateOnly> AdditionalHolidays { get; init; } = [];

    public IReadOnlyCollection<DateOnly> ExcludedDates { get; init; } = [];
}
