using CSPLibraries.DateTimes.Datas;

namespace CSPLibraries.DateTimes.Models;

public sealed record Holiday(DateOnly Date, string Name, HolidayType Type, bool IsHalfDay = false);
