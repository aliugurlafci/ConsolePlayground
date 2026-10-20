using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using CSPLibraries.Serialization.Constants;
using CSPLibraries.Serialization.Datas;

namespace CSPLibraries.Serialization.Workers;

internal sealed class CsvRowWriter<T>
{
    private static readonly Action<T, CsvFieldWriter>[] Writers = RecordMetadata<T>.CsvFields.Select(f => CompileWriter(f.Property)).ToArray();

    private readonly TextWriter _writer;
    private readonly CsvOptions _options;
    private readonly CsvFieldWriter _field;

    internal CsvRowWriter(TextWriter writer, CsvOptions options)
    {
        _writer = writer;
        _options = options;
        _field = new CsvFieldWriter(writer, options);
    }

    internal void WriteHeader()
    {
        RecordField<T>[] fields = RecordMetadata<T>.CsvFields;
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) _writer.Write(_options.Delimiter);
            _field.WriteString(fields[i].Name);
        }

        _writer.Write(_options.NewLine);
    }

    internal void WriteRow(T row)
    {
        for (int i = 0; i < Writers.Length; i++)
        {
            if (i > 0) _writer.Write(_options.Delimiter);
            if (row is not null) Writers[i](row, _field);
        }

        _writer.Write(_options.NewLine);
    }

    private static Action<T, CsvFieldWriter> CompileWriter(PropertyInfo property)
    {
        ParameterExpression row = Expression.Parameter(typeof(T), "row");
        ParameterExpression writer = Expression.Parameter(typeof(CsvFieldWriter), "writer");
        Expression value = Expression.Property(row, property);
        Type? underlying = Nullable.GetUnderlyingType(property.PropertyType);

        Expression body = underlying is null
            ? CallWriter(writer, value, property.PropertyType)
            : Expression.IfThen(Expression.Property(value, "HasValue"), CallWriter(writer, Expression.Property(value, "Value"), underlying));

        return Expression.Lambda<Action<T, CsvFieldWriter>>(body, row, writer).Compile();
    }

    private static Expression CallWriter(ParameterExpression writer, Expression value, Type type)
    {
        string? method = type == typeof(string) ? nameof(CsvFieldWriter.WriteString)
            : type == typeof(bool) ? nameof(CsvFieldWriter.WriteBoolean)
            : type == typeof(DateTime) ? nameof(CsvFieldWriter.WriteDateTime)
            : type == typeof(DateOnly) ? nameof(CsvFieldWriter.WriteDateOnly)
            : null;

        if (method is not null)
            return Expression.Call(writer, typeof(CsvFieldWriter).GetMethod(method)!, value);

        if (!type.IsEnum && typeof(ISpanFormattable).IsAssignableFrom(type) && type.IsValueType)
            return Expression.Call(writer, typeof(CsvFieldWriter).GetMethod(nameof(CsvFieldWriter.WriteFormattable))!.MakeGenericMethod(type), value);

        return Expression.Call(writer, typeof(CsvFieldWriter).GetMethod(nameof(CsvFieldWriter.WriteObject))!, Expression.Convert(value, typeof(object)));
    }
}

internal sealed class CsvFieldWriter(TextWriter writer, CsvOptions options)
{
    private static readonly ConcurrentDictionary<char, System.Buffers.SearchValues<char>> QuoteTriggers = new();

    private readonly char[] _buffer = new char[128];
    private readonly CultureInfo _culture = options.Culture;
    private readonly System.Buffers.SearchValues<char> _quoteTriggers =
        QuoteTriggers.GetOrAdd(options.Delimiter, static d => System.Buffers.SearchValues.Create([d, '"', '\n', '\r']));

    public void WriteString(string? value)
    {
        if (value is not null) WriteField(value, isText: true);
    }

    public void WriteBoolean(bool value) => writer.Write(value ? "true" : "false");

    public void WriteDateTime(DateTime value)
    {
        string format = (value.TimeOfDay == TimeSpan.Zero ? options.DateFormat : options.DateTimeFormat) ?? "yyyy-MM-ddTHH:mm:ss";
        if (value.TryFormat(_buffer, out int written, format, _culture)) WriteField(_buffer.AsSpan(0, written), isText: false);
        else WriteField(value.ToString(format, _culture), isText: false);
    }

    public void WriteDateOnly(DateOnly value)
    {
        string format = options.DateFormat ?? "yyyy-MM-dd";
        if (value.TryFormat(_buffer, out int written, format, _culture)) WriteField(_buffer.AsSpan(0, written), isText: false);
        else WriteField(value.ToString(format, _culture), isText: false);
    }

    public void WriteFormattable<TValue>(TValue value) where TValue : ISpanFormattable
    {
        if (value.TryFormat(_buffer, out int written, default, _culture)) WriteField(_buffer.AsSpan(0, written), isText: false);
        else WriteField(value.ToString(null, _culture), isText: false);
    }

    public void WriteObject(object? value)
    {
        string? text = TextValueConverter.Format(value, _culture, null, options.DateFormat, options.DateTimeFormat);
        if (text is not null) WriteField(text, value is string);
    }

    private void WriteField(ReadOnlySpan<char> text, bool isText)
    {
        bool escapeFormula = options.EscapeFormulas && isText && !text.IsEmpty && SerializationConstants.FormulaPrefixes.Contains(text[0]);
        bool quote = options.QuoteAllFields || escapeFormula || text.IndexOfAny(_quoteTriggers) >= 0 ||
                     (!text.IsEmpty && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])));

        if (!quote)
        {
            writer.Write(text);
            return;
        }

        writer.Write('"');
        if (escapeFormula) writer.Write('\'');

        int index;
        while ((index = text.IndexOf('"')) >= 0)
        {
            writer.Write(text[..(index + 1)]);
            writer.Write('"');
            text = text[(index + 1)..];
        }

        writer.Write(text);
        writer.Write('"');
    }
}
