using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using CSPLibraries.Serialization.Models;

namespace CSPLibraries.Serialization.Workers;

internal static class RecordMetadata<T>
{
    internal static readonly RecordField<T>[] CsvFields = BuildCsvFields();

    internal static readonly Lazy<RecordField<T>[]> FixedWidthFields = new(BuildFixedWidthFields);

    internal static string NormalizeName(string name) =>
        string.Concat(name.Where(c => !char.IsWhiteSpace(c) && c is not '_' and not '-')).ToUpperInvariant();

    private static RecordField<T>[] BuildCsvFields() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && !p.IsDefined(typeof(NotMappedAttribute), true))
            .Select((p, index) => (Property: p, Column: p.GetCustomAttribute<ColumnAttribute>(), index))
            .OrderBy(x => x.Column is { Order: >= 0 } ? x.Column.Order : int.MaxValue)
            .ThenBy(x => x.index)
            .Select(x => Create(x.Property, x.Column?.Name ?? x.Property.Name, null))
            .ToArray();

    private static RecordField<T>[] BuildFixedWidthFields()
    {
        RecordField<T>[] fields = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Field: p.GetCustomAttribute<FixedWidthFieldAttribute>()))
            .Where(x => x.Field is not null)
            .OrderBy(x => x.Field!.Start)
            .Select(x => Create(x.Property, x.Property.Name, x.Field))
            .ToArray();

        if (fields.Length == 0)
            throw new SerializationServiceException($"{typeof(T).Name} tipinde [FixedWidthField] ile işaretlenmiş özellik yok.");

        for (int i = 0; i < fields.Length; i++)
        {
            FixedWidthFieldAttribute field = fields[i].FixedWidth!;
            if (field.Start < 1 || field.Length < 1)
                throw new SerializationServiceException($"{fields[i].Name}: Start 1'den, Length 0'dan büyük olmalıdır.");

            if (i > 0 && fields[i - 1].FixedWidth!.Start + fields[i - 1].FixedWidth!.Length > field.Start)
                throw new SerializationServiceException($"{fields[i - 1].Name} ve {fields[i].Name} alanları çakışıyor.");
        }

        return fields;
    }

    private static RecordField<T> Create(PropertyInfo property, string name, FixedWidthFieldAttribute? fixedWidth)
    {
        ParameterExpression instance = Expression.Parameter(typeof(T), "instance");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");

        Func<T, object?>? getter = property.GetMethod?.IsPublic == true
            ? Expression.Lambda<Func<T, object?>>(Expression.Convert(Expression.Property(instance, property), typeof(object)), instance).Compile()
            : null;

        Action<T, object?>? setter = property.SetMethod?.IsPublic == true && !typeof(T).IsValueType
            ? Expression.Lambda<Action<T, object?>>(
                Expression.Assign(Expression.Property(instance, property), Expression.Convert(value, property.PropertyType)),
                instance, value).Compile()
            : null;

        Type underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        bool acceptsNull = !property.PropertyType.IsValueType || underlying != property.PropertyType;

        return new RecordField<T>(name, NormalizeName(name), property, property.PropertyType, acceptsNull, getter, setter, fixedWidth);
    }
}

internal sealed record RecordField<T>(
    string Name,
    string NormalizedName,
    PropertyInfo Property,
    Type Type,
    bool AcceptsNull,
    Func<T, object?>? Getter,
    Action<T, object?>? Setter,
    FixedWidthFieldAttribute? FixedWidth);
