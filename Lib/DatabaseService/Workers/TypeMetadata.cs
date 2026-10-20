using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace CSPLibraries.Database.Workers;

internal static class TypeMetadata
{
    private static readonly ConcurrentDictionary<Type, MappedProperty[]> Cache = new();

    internal static MappedProperty[] GetProperties(Type type) => Cache.GetOrAdd(type, Discover);

    internal static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum ||
               type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid) ||
               type == typeof(byte[]) || type == typeof(DateOnly) || type == typeof(TimeOnly);
    }

    internal static string NormalizeName(string name)
    {
        Span<char> buffer = name.Length <= 256 ? stackalloc char[name.Length] : new char[name.Length];
        int length = 0;

        foreach (char c in name)
        {
            if (c is '_' or ' ' or '-') continue;
            buffer[length++] = char.ToUpperInvariant(c);
        }

        return buffer[..length].ToString();
    }

    private static MappedProperty[] Discover(Type type)
    {
        var result = new List<MappedProperty>();

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (property.IsDefined(typeof(NotMappedAttribute), inherit: true)) continue;

            string columnName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
            bool canWrite = property.SetMethod?.IsPublic == true;
            bool canRead = property.GetMethod?.IsPublic == true;

            result.Add(new MappedProperty(property, columnName, NormalizeName(columnName), canRead, canWrite,
                                          canRead ? CompileGetter(type, property) : null));
        }

        return result.ToArray();
    }

    private static Func<object, object?> CompileGetter(Type type, PropertyInfo property)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        Expression body = Expression.Convert(
            Expression.Property(Expression.Convert(instance, type), property), typeof(object));

        return Expression.Lambda<Func<object, object?>>(body, instance).Compile();
    }
}

internal sealed record MappedProperty(
    PropertyInfo Property,
    string ColumnName,
    string NormalizedName,
    bool CanRead,
    bool CanWrite,
    Func<object, object?>? Getter)
{
    public Type Type => Property.PropertyType;
}
