using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using CSPLibraries.Excel.Datas;
using CSPLibraries.Excel.Models;

namespace CSPLibraries.Excel.Workers;

internal static class ExcelObjectMapper<T> where T : class, new()
{
    private static readonly PropertyBinding[] Properties = DiscoverProperties();

    private static readonly Dictionary<string, PropertyBinding> PropertiesByKey = BuildKeyLookup();

    internal static IEnumerable<T> Map(IExcelRowReader reader, ExcelReadOptions options)
    {
        CultureInfo culture = options.Culture ?? CultureInfo.CurrentCulture;
        int rowNumber = 0;
        ColumnBinding[] columns;

        if (options.UseHeaderRow)
        {
            for (int i = 0; i < options.SkipRowsBeforeHeader; i++, rowNumber++)
            {
                if (!reader.Read()) yield break;
            }

            if (!reader.Read()) yield break;
            rowNumber++;
            columns = BindByHeader(reader, culture);
        }
        else
        {
            columns = BindByIndex();
        }

        while (reader.Read())
        {
            rowNumber++;
            if (options.SkipEmptyRows && ExcelRowReaderFactory.IsEmptyRow(reader)) continue;

            yield return MapRow(reader, columns, options.TrimValues, culture, rowNumber);
        }
    }

    private static T MapRow(IExcelRowReader reader, ColumnBinding[] columns, bool trim, CultureInfo culture, int rowNumber)
    {
        var item = new T();
        int fieldCount = reader.FieldCount;

        foreach (ColumnBinding column in columns)
        {
            if (column.ColumnIndex >= fieldCount) continue;

            object? raw = reader.GetValue(column.ColumnIndex);
            if (raw is null) continue;
            if (trim && raw is string text) raw = text.Trim();

            object? value;
            try
            {
                value = ExcelValueConverter.Convert(raw, column.Property.TargetType, culture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                throw new ExcelServiceException(
                    $"Satır {rowNumber}, '{column.Header}' sütunu: '{raw}' değeri " +
                    $"{typeof(T).Name}.{column.Property.Name} ({column.Property.TargetType.Name}) tipine dönüştürülemedi.", ex);
            }

            if (value is null && !column.Property.AcceptsNull) continue;
            column.Property.Setter(item, value);
        }

        return item;
    }

    private static ColumnBinding[] BindByHeader(IExcelRowReader reader, CultureInfo culture)
    {
        var bindings = new List<ColumnBinding>(Math.Min(reader.FieldCount, Properties.Length));
        var used = new HashSet<PropertyBinding>(ReferenceEqualityComparer.Instance);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string header = ExcelRowReaderFactory.HeaderText(reader, i, culture);
            if (header.Length == 0) continue;

            if (PropertiesByKey.TryGetValue(NormalizeKey(header), out PropertyBinding? property) && used.Add(property))
                bindings.Add(new ColumnBinding(i, header, property));
        }

        return bindings.ToArray();
    }

    private static ColumnBinding[] BindByIndex()
    {
        bool anyExplicit = Properties.Any(p => p.ExplicitIndex >= 0);

        return Properties
            .Select((property, order) => new ColumnBinding(
                anyExplicit ? property.ExplicitIndex : order, property.Name, property))
            .Where(binding => binding.ColumnIndex >= 0)
            .ToArray();
    }

    private static PropertyBinding[] DiscoverProperties()
    {
        var result = new List<PropertyBinding>();

        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.SetMethod?.IsPublic != true || property.GetIndexParameters().Length > 0)
                continue;

            var attribute = property.GetCustomAttribute<ExcelColumnAttribute>();
            if (attribute?.Ignore == true) continue;

            Type? underlying = Nullable.GetUnderlyingType(property.PropertyType);
            bool acceptsNull = !property.PropertyType.IsValueType || underlying is not null;

            result.Add(new PropertyBinding(
                property.Name,
                attribute?.Name,
                attribute?.Index ?? -1,
                underlying ?? property.PropertyType,
                acceptsNull,
                CompileSetter(property)));
        }

        return result.ToArray();
    }

    private static Dictionary<string, PropertyBinding> BuildKeyLookup()
    {
        var lookup = new Dictionary<string, PropertyBinding>(StringComparer.Ordinal);

        foreach (PropertyBinding property in Properties)
        {
            if (!string.IsNullOrWhiteSpace(property.ColumnName))
                lookup.TryAdd(NormalizeKey(property.ColumnName), property);
        }

        foreach (PropertyBinding property in Properties)
        {
            if (string.IsNullOrWhiteSpace(property.ColumnName))
                lookup.TryAdd(NormalizeKey(property.Name), property);
        }

        return lookup;
    }

    private static string NormalizeKey(string text)
    {
        Span<char> buffer = text.Length <= 256 ? stackalloc char[text.Length] : new char[text.Length];
        int length = 0;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || c is '_' or '-' or '.') continue;
            buffer[length++] = char.ToUpperInvariant(c);
        }

        return buffer[..length].ToString();
    }

    private static Action<T, object?> CompileSetter(PropertyInfo property)
    {
        ParameterExpression target = Expression.Parameter(typeof(T), "target");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");

        BinaryExpression assign = Expression.Assign(
            Expression.Property(target, property),
            Expression.Convert(value, property.PropertyType));

        return Expression.Lambda<Action<T, object?>>(assign, target, value).Compile();
    }

    private sealed record PropertyBinding(
        string Name,
        string? ColumnName,
        int ExplicitIndex,
        Type TargetType,
        bool AcceptsNull,
        Action<T, object?> Setter);

    private readonly record struct ColumnBinding(int ColumnIndex, string Header, PropertyBinding Property);
}
