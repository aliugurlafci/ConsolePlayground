using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using CSPLibraries.DateTimes.Constants;
using CSPLibraries.DateTimes.Datas;
using CSPLibraries.DateTimes.Models;

namespace CSPLibraries.DateTimes.Workers;

internal sealed class TurkishHolidayCalendar
{
    private static readonly UmAlQuraCalendar Hijri = new();

    private readonly DateTimeServiceOptions _options;
    private readonly ConcurrentDictionary<int, FrozenDictionary<DateOnly, Holiday>> _years = new();
    private readonly FrozenSet<DateOnly> _excluded;

    internal TurkishHolidayCalendar(DateTimeServiceOptions options)
    {
        _options = options;
        _excluded = options.ExcludedDates.ToFrozenSet();
    }

    internal FrozenDictionary<DateOnly, Holiday> GetYear(int year) => _years.GetOrAdd(year, Build);

    internal Holiday? Find(DateOnly date) =>
        GetYear(date.Year).TryGetValue(date, out Holiday? holiday) ? holiday : null;

    private FrozenDictionary<DateOnly, Holiday> Build(int year)
    {
        var holidays = new Dictionary<DateOnly, Holiday>();

        void Add(DateOnly date, string name, HolidayType type, bool halfDay = false)
        {
            if (date.Year != year || _excluded.Contains(date)) return;

            if (!holidays.TryGetValue(date, out Holiday? existing) || (existing.IsHalfDay && !halfDay))
                holidays[date] = new Holiday(date, name, type, halfDay);
        }

        Add(new DateOnly(year, 1, 1), "Yılbaşı", HolidayType.National);
        Add(new DateOnly(year, 4, 23), "Ulusal Egemenlik ve Çocuk Bayramı", HolidayType.National);
        Add(new DateOnly(year, 5, 1), "Emek ve Dayanışma Günü", HolidayType.National);
        Add(new DateOnly(year, 5, 19), "Atatürk'ü Anma, Gençlik ve Spor Bayramı", HolidayType.National);
        if (year >= DateTimeConstants.July15FirstYear)
            Add(new DateOnly(year, 7, 15), "Demokrasi ve Milli Birlik Günü", HolidayType.National);
        Add(new DateOnly(year, 8, 30), "Zafer Bayramı", HolidayType.National);
        Add(new DateOnly(year, 10, 28), "Cumhuriyet Bayramı Arifesi", HolidayType.National, halfDay: true);
        Add(new DateOnly(year, 10, 29), "Cumhuriyet Bayramı", HolidayType.National);

        if (_options.IncludeReligiousHolidays &&
            year is >= DateTimeConstants.HijriSupportedMinYear and <= DateTimeConstants.HijriSupportedMaxYear)
        {
            int hijriYear = Hijri.GetYear(new DateTime(year, 1, 1));
            for (int hy = hijriYear; hy <= hijriYear + 1; hy++)
            {
                AddFeast(hy, month: 10, day: 1, length: 3, "Ramazan Bayramı", Add);
                AddFeast(hy, month: 12, day: 10, length: 4, "Kurban Bayramı", Add);
            }
        }

        foreach (DateOnly date in _options.AdditionalHolidays)
            Add(date, "Özel Tatil", HolidayType.Custom);

        return holidays.ToFrozenDictionary();
    }

    private static void AddFeast(int hijriYear, int month, int day, int length, string name,
                                 Action<DateOnly, string, HolidayType, bool> add)
    {
        DateOnly start;
        try
        {
            start = DateOnly.FromDateTime(Hijri.ToDateTime(hijriYear, month, day, 0, 0, 0, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        add(start.AddDays(-1), name + " Arifesi", HolidayType.Religious, true);
        for (int i = 0; i < length; i++)
            add(start.AddDays(i), $"{name} {i + 1}. Gün", HolidayType.Religious, false);
    }
}
