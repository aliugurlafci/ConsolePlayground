using System.Globalization;
using System.Text;

namespace CSPLibraries.Sap.Models;

public sealed class ODataQuery
{
    private readonly List<string> _select = [];
    private readonly List<string> _expand = [];
    private readonly List<string> _orderBy = [];
    private readonly List<KeyValuePair<string, string>> _custom = [];

    public string? Filter { get; private set; }

    public int? Top { get; private set; }

    public int? Skip { get; private set; }

    public bool InlineCount { get; private set; }

    public string? Search { get; private set; }

    public static ODataQuery Create() => new();

    public ODataQuery Where(string filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        Filter = Filter is null ? filter : $"({Filter}) and ({filter})";
        return this;
    }

    public ODataQuery WhereEquals(string property, object? value) => Where($"{property} eq {Literal(value)}");

    public ODataQuery WhereIn(string property, IEnumerable<object?> values)
    {
        string condition = string.Join(" or ", values.Select(v => $"{property} eq {Literal(v)}"));
        return condition.Length == 0 ? Where("1 eq 0") : Where(condition);
    }

    public ODataQuery Select(params string[] properties)
    {
        _select.AddRange(properties);
        return this;
    }

    public ODataQuery Expand(params string[] navigationProperties)
    {
        _expand.AddRange(navigationProperties);
        return this;
    }

    public ODataQuery OrderBy(string property, bool descending = false)
    {
        _orderBy.Add(descending ? property + " desc" : property);
        return this;
    }

    public ODataQuery Take(int top)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        Top = top;
        return this;
    }

    public ODataQuery SkipRows(int skip)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        Skip = skip;
        return this;
    }

    public ODataQuery WithInlineCount(bool enabled = true)
    {
        InlineCount = enabled;
        return this;
    }

    public ODataQuery WithSearch(string search)
    {
        Search = search;
        return this;
    }

    public ODataQuery WithParameter(string name, string value)
    {
        _custom.Add(new(name, value));
        return this;
    }

    public string ToQueryString()
    {
        var builder = new StringBuilder(128);
        Append(builder, "$filter", Filter);
        Append(builder, "$select", _select.Count == 0 ? null : string.Join(',', _select));
        Append(builder, "$expand", _expand.Count == 0 ? null : string.Join(',', _expand));
        Append(builder, "$orderby", _orderBy.Count == 0 ? null : string.Join(',', _orderBy));
        Append(builder, "$top", Top?.ToString(CultureInfo.InvariantCulture));
        Append(builder, "$skip", Skip?.ToString(CultureInfo.InvariantCulture));
        Append(builder, "$inlinecount", InlineCount ? "allpages" : null);
        Append(builder, "search", Search);

        foreach (var (name, value) in _custom)
            Append(builder, name, value);

        return builder.ToString();
    }

    public override string ToString() => ToQueryString();

    public static string Literal(object? value) => value switch
    {
        null => "null",
        string text => "'" + text.Replace("'", "''") + "'",
        bool flag => flag ? "true" : "false",
        DateTime date => "datetime'" + date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "'",
        DateOnly date => "datetime'" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00'",
        DateTimeOffset date => "datetimeoffset'" + date.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture) + "'",
        TimeSpan time => "time'" + System.Xml.XmlConvert.ToString(time) + "'",
        Guid guid => "guid'" + guid.ToString("D") + "'",
        decimal number => number.ToString(CultureInfo.InvariantCulture) + "M",
        long number => number.ToString(CultureInfo.InvariantCulture) + "L",
        double number => number.ToString("R", CultureInfo.InvariantCulture) + "d",
        float number => number.ToString("R", CultureInfo.InvariantCulture) + "f",
        Enum => "'" + value + "'",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => "'" + value.ToString()?.Replace("'", "''") + "'"
    };

    private static void Append(StringBuilder builder, string name, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (builder.Length > 0) builder.Append('&');
        builder.Append(name).Append('=').Append(Uri.EscapeDataString(value));
    }
}
