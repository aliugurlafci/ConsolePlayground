using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace CSPLibraries.Enumerables.Workers;

internal static class DataTableConverter
{
    private static readonly ConcurrentDictionary<Type, Column[]> Columns = new();

    internal static DataTable Convert<T>(IEnumerable<T> source, string? tableName)
    {
        Column[] columns = Columns.GetOrAdd(typeof(T), Discover);
        var table = new DataTable(tableName ?? typeof(T).Name);

        foreach (Column column in columns)
            table.Columns.Add(column.Name, column.DataType);

        var values = new object[columns.Length];
        table.BeginLoadData();

        foreach (T item in source)
        {
            if (item is null) continue;

            for (int i = 0; i < columns.Length; i++)
                values[i] = columns[i].Getter(item) ?? DBNull.Value;

            table.LoadDataRow(values, fAcceptChanges: true);
        }

        table.EndLoadData();
        return table;
    }

    private static Column[] Discover(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !p.IsDefined(typeof(NotMappedAttribute)))
            .Select(p => new Column(
                p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name,
                Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType,
                CompileGetter(type, p)))
            .ToArray();

    private static Func<object, object?> CompileGetter(Type type, PropertyInfo property)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        return Expression.Lambda<Func<object, object?>>(
            Expression.Convert(Expression.Property(Expression.Convert(instance, type), property), typeof(object)),
            instance).Compile();
    }

    private sealed record Column(string Name, Type DataType, Func<object, object?> Getter);
}
