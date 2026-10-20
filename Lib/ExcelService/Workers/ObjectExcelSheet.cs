using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using CSPLibraries.Excel.Models;

namespace CSPLibraries.Excel.Workers;

internal sealed class ObjectExcelSheet<T> : ExcelSheet
{
    private static readonly Lazy<Metadata> TypeMetadata = new(BuildMetadata);

    private readonly IEnumerable<T> _rows;

    internal ObjectExcelSheet(string name, IEnumerable<T> rows)
        : base(name)
    {
        _rows = rows;
    }

    internal override IReadOnlyList<ExcelColumnDefinition> Columns => TypeMetadata.Value.Columns;

    internal override IExcelRowCursor OpenCursor() => new Cursor(_rows.GetEnumerator(), TypeMetadata.Value);

    private static Metadata BuildMetadata()
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod!.IsPublic && p.GetIndexParameters().Length == 0)
            .Select((p, declarationOrder) => (Property: p, Attribute: p.GetCustomAttribute<ExcelColumnAttribute>(), declarationOrder))
            .Where(x => x.Attribute?.Ignore != true)
            .OrderBy(x => x.Attribute?.Order ?? int.MaxValue)
            .ThenBy(x => x.declarationOrder)
            .ToArray();

        var columns = new ExcelColumnDefinition[properties.Length];
        var writers = new Action<T, XlsxCellWriter>[properties.Length];
        var getters = new Func<T, object?>[properties.Length];

        for (int i = 0; i < properties.Length; i++)
        {
            (PropertyInfo property, ExcelColumnAttribute? attribute, _) = properties[i];
            string header = attribute?.Name ?? property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name;

            columns[i] = new ExcelColumnDefinition(header, attribute?.Format, attribute is { Width: > 0 } ? attribute.Width : null);
            writers[i] = CompileWriter(property);
            getters[i] = CompileGetter(property);
        }

        return new Metadata(columns, writers, getters);
    }

    private static Action<T, XlsxCellWriter> CompileWriter(PropertyInfo property)
    {
        ParameterExpression row = Expression.Parameter(typeof(T), "row");
        ParameterExpression writer = Expression.Parameter(typeof(XlsxCellWriter), "writer");
        Expression value = Expression.Property(row, property);

        Type type = property.PropertyType;
        Type? underlying = Nullable.GetUnderlyingType(type);

        Expression body;
        if (underlying is not null)
        {
            Expression inner = CallWriter(writer, Expression.Property(value, "Value"), underlying);
            body = Expression.IfThen(Expression.Property(value, "HasValue"), inner);
        }
        else
        {
            body = CallWriter(writer, value, type);
        }

        return Expression.Lambda<Action<T, XlsxCellWriter>>(body, row, writer).Compile();
    }

    private static Expression CallWriter(ParameterExpression writer, Expression value, Type type)
    {
        (string Method, Type Parameter) target = type switch
        {
            _ when type == typeof(string) => (nameof(XlsxCellWriter.WriteString), typeof(string)),
            _ when type == typeof(int) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(sbyte) || type == typeof(ushort) => (nameof(XlsxCellWriter.WriteInt32), typeof(int)),
            _ when type == typeof(long) || type == typeof(uint) => (nameof(XlsxCellWriter.WriteInt64), typeof(long)),
            _ when type == typeof(double) || type == typeof(float) => (nameof(XlsxCellWriter.WriteDouble), typeof(double)),
            _ when type == typeof(decimal) => (nameof(XlsxCellWriter.WriteDecimal), typeof(decimal)),
            _ when type == typeof(bool) => (nameof(XlsxCellWriter.WriteBoolean), typeof(bool)),
            _ when type == typeof(DateTime) => (nameof(XlsxCellWriter.WriteDateTime), typeof(DateTime)),
            _ when type == typeof(DateOnly) => (nameof(XlsxCellWriter.WriteDateOnly), typeof(DateOnly)),
            _ when type == typeof(DateTimeOffset) => (nameof(XlsxCellWriter.WriteDateTimeOffset), typeof(DateTimeOffset)),
            _ when type == typeof(TimeSpan) => (nameof(XlsxCellWriter.WriteTimeSpan), typeof(TimeSpan)),
            _ when type == typeof(TimeOnly) => (nameof(XlsxCellWriter.WriteTimeOnly), typeof(TimeOnly)),
            _ when type == typeof(Guid) => (nameof(XlsxCellWriter.WriteGuid), typeof(Guid)),
            _ => (nameof(XlsxCellWriter.WriteObject), typeof(object))
        };

        MethodInfo method = typeof(XlsxCellWriter).GetMethod(target.Method, [target.Parameter])!;
        Expression argument = value.Type == target.Parameter ? value : Expression.Convert(value, target.Parameter);
        return Expression.Call(writer, method, argument);
    }

    private static Func<T, object?> CompileGetter(PropertyInfo property)
    {
        ParameterExpression row = Expression.Parameter(typeof(T), "row");
        return Expression.Lambda<Func<T, object?>>(
            Expression.Convert(Expression.Property(row, property), typeof(object)), row).Compile();
    }

    private sealed record Metadata(ExcelColumnDefinition[] Columns, Action<T, XlsxCellWriter>[] Writers, Func<T, object?>[] Getters);

    private sealed class Cursor(IEnumerator<T> source, Metadata metadata) : IExcelRowCursor
    {
        private readonly Queue<T> _buffer = new();
        private T? _current;

        public bool MoveNext()
        {
            if (_buffer.TryDequeue(out _current)) return true;
            if (!source.MoveNext()) return false;

            _current = source.Current;
            return true;
        }

        public void WriteCell(int column, XlsxCellWriter writer)
        {
            if (_current is not null)
                metadata.Writers[column](_current, writer);
        }

        public IReadOnlyList<object?[]> Sample(int rowCount)
        {
            var sample = new List<object?[]>(rowCount);
            while (sample.Count < rowCount && source.MoveNext())
            {
                T row = source.Current;
                _buffer.Enqueue(row);
                if (row is null) continue;

                var values = new object?[metadata.Getters.Length];
                for (int i = 0; i < values.Length; i++)
                    values[i] = metadata.Getters[i](row);
                sample.Add(values);
            }

            return sample;
        }

        public void Dispose() => source.Dispose();
    }
}
