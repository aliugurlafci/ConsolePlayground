using System.Globalization;
using CSPLibraries.Excel.Constants;

namespace CSPLibraries.Excel.Workers;

internal sealed class XlsxCellWriter
{
    private readonly TextWriter _writer;
    private readonly string[] _columnRefs;
    private readonly int[] _columnStyles;
    private readonly DefaultStyles _defaults;
    private readonly char[] _rowDigits = new char[11];
    private readonly char[] _number = new char[64];
    private int _rowDigitCount;

    internal XlsxCellWriter(TextWriter writer, int[] columnStyles, DefaultStyles defaults)
    {
        _writer = writer;
        _columnStyles = columnStyles;
        _defaults = defaults;
        _columnRefs = new string[columnStyles.Length];
        for (int i = 0; i < _columnRefs.Length; i++)
            _columnRefs[i] = ColumnName(i);
    }

    internal int Column { get; set; }

    internal static string ColumnName(int index)
    {
        Span<char> buffer = stackalloc char[4];
        int position = buffer.Length;
        for (int n = index + 1; n > 0; n = (n - 1) / 26)
            buffer[--position] = (char)('A' + (n - 1) % 26);

        return buffer[position..].ToString();
    }

    internal void StartRow(int rowNumber)
    {
        rowNumber.TryFormat(_rowDigits, out _rowDigitCount, provider: CultureInfo.InvariantCulture);
        _writer.Write("<row r=\"");
        _writer.Write(_rowDigits, 0, _rowDigitCount);
        _writer.Write("\">");
    }

    internal void EndRow() => _writer.Write("</row>");

    #region Tipli yazıcılar (derlenmiş sütun delegeleri bunları doğrudan çağırır)

    public void WriteHeader(string text) => WriteText(text, XlsxConstants.HeaderStyleIndex);

    public void WriteString(string? value)
    {
        if (value is null) return;
        WriteText(value, Math.Max(_columnStyles[Column], 0));
    }

    public void WriteInt32(int value)
    {
        value.TryFormat(_number, out int length, provider: CultureInfo.InvariantCulture);
        WriteNumber(_columnStyles[Column], length);
    }

    public void WriteInt64(long value)
    {
        if (value is > XlsxConstants.MaxExactInteger or < -XlsxConstants.MaxExactInteger)
        {
            WriteString(value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        value.TryFormat(_number, out int length, provider: CultureInfo.InvariantCulture);
        WriteNumber(_columnStyles[Column], length);
    }

    public void WriteDouble(double value)
    {
        if (!double.IsFinite(value))
        {
            WriteString(value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        value.TryFormat(_number, out int length, "R", CultureInfo.InvariantCulture);
        WriteNumber(_columnStyles[Column], length);
    }

    public void WriteDecimal(decimal value)
    {
        value.TryFormat(_number, out int length, provider: CultureInfo.InvariantCulture);
        WriteNumber(Resolve(_defaults.Decimal), length);
    }

    public void WriteBoolean(bool value)
    {
        StartCell(Math.Max(_columnStyles[Column], 0), "b");
        _writer.Write(value ? "<v>1</v></c>" : "<v>0</v></c>");
    }

    public void WriteDateTime(DateTime value)
    {
        if (value < XlsxConstants.MinExcelDate)
        {
            WriteString(value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            return;
        }

        value.ToOADate().TryFormat(_number, out int length, "R", CultureInfo.InvariantCulture);
        WriteNumber(Resolve(value.TimeOfDay == TimeSpan.Zero ? _defaults.Date : _defaults.DateTime), length);
    }

    public void WriteDateOnly(DateOnly value) => WriteDateTime(value.ToDateTime(TimeOnly.MinValue));

    public void WriteDateTimeOffset(DateTimeOffset value) => WriteDateTime(value.DateTime);

    public void WriteTimeSpan(TimeSpan value)
    {
        value.TotalDays.TryFormat(_number, out int length, "R", CultureInfo.InvariantCulture);
        WriteNumber(Resolve(_defaults.Time), length);
    }

    public void WriteTimeOnly(TimeOnly value) => WriteTimeSpan(value.ToTimeSpan());

    public void WriteGuid(Guid value) => WriteString(value.ToString("D"));

    public void WriteObject(object? value)
    {
        switch (value)
        {
            case null or DBNull: return;
            case string text: WriteString(text); return;
            case int number: WriteInt32(number); return;
            case long number: WriteInt64(number); return;
            case short number: WriteInt32(number); return;
            case byte number: WriteInt32(number); return;
            case sbyte number: WriteInt32(number); return;
            case ushort number: WriteInt32(number); return;
            case uint number: WriteInt64(number); return;
            case ulong number when number <= XlsxConstants.MaxExactInteger: WriteInt64((long)number); return;
            case double number: WriteDouble(number); return;
            case float number: WriteDouble(number); return;
            case decimal number: WriteDecimal(number); return;
            case bool flag: WriteBoolean(flag); return;
            case DateTime date: WriteDateTime(date); return;
            case DateOnly date: WriteDateOnly(date); return;
            case DateTimeOffset date: WriteDateTimeOffset(date); return;
            case TimeSpan time: WriteTimeSpan(time); return;
            case TimeOnly time: WriteTimeOnly(time); return;
            case Guid guid: WriteGuid(guid); return;
            case byte[] bytes: WriteString(Convert.ToBase64String(bytes)); return;
            case IFormattable formattable: WriteString(formattable.ToString(null, CultureInfo.InvariantCulture)); return;
            default: WriteString(value.ToString()); return;
        }
    }

    #endregion

    private int Resolve(int defaultStyle)
    {
        int explicitStyle = _columnStyles[Column];
        return explicitStyle >= 0 ? explicitStyle : defaultStyle;
    }

    private void WriteNumber(int style, int length)
    {
        StartCell(Math.Max(style, 0), null);
        _writer.Write("<v>");
        _writer.Write(_number, 0, length);
        _writer.Write("</v></c>");
    }

    private void WriteText(string value, int style)
    {
        ReadOnlySpan<char> text = value.Length > XlsxConstants.MaxCellTextLength
            ? value.AsSpan(0, XlsxConstants.MaxCellTextLength)
            : value;

        StartCell(style, "inlineStr");

        bool preserve = text.Length > 0 &&
                        (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains('\n'));
        _writer.Write(preserve ? "<is><t xml:space=\"preserve\">" : "<is><t>");
        WriteEscaped(text);
        _writer.Write("</t></is></c>");
    }

    private void StartCell(int style, string? type)
    {
        _writer.Write("<c r=\"");
        _writer.Write(_columnRefs[Column]);
        _writer.Write(_rowDigits, 0, _rowDigitCount);
        _writer.Write('"');

        if (style > 0)
        {
            _writer.Write(" s=\"");
            style.TryFormat(_number.AsSpan(48), out int length, provider: CultureInfo.InvariantCulture);
            _writer.Write(_number, 48, length);
            _writer.Write('"');
        }

        if (type is not null)
        {
            _writer.Write(" t=\"");
            _writer.Write(type);
            _writer.Write('"');
        }

        _writer.Write('>');
    }

    private void WriteEscaped(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            int index = text.IndexOfAny(XlsxConstants.XmlSpecialChars);
            if (index < 0)
            {
                _writer.Write(text);
                return;
            }

            _writer.Write(text[..index]);
            char c = text[index];

            switch (c)
            {
                case '&': _writer.Write("&amp;"); break;
                case '<': _writer.Write("&lt;"); break;
                case '>': _writer.Write("&gt;"); break;
                case '\r': _writer.Write("&#xD;"); break;
                default:
                    if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                    {
                        _writer.Write(text.Slice(index, 2));
                        index++;
                    }
                    break;
            }

            text = text[(index + 1)..];
        }
    }

    internal readonly record struct DefaultStyles(int Date, int DateTime, int Time, int Decimal);
}
