using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using CSPLibraries.Database.Models;

namespace CSPLibraries.Database.Workers;

internal static class DataRecordMapper
{
    private static readonly ConcurrentDictionary<MapperKey, Delegate> Cache = new();

    private static readonly MethodInfo IsDBNullMethod = typeof(DbDataReader).GetMethod(nameof(DbDataReader.IsDBNull), [typeof(int)])!;
    private static readonly MethodInfo GetValueMethod = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), [typeof(int)])!;
    private static readonly MethodInfo GetFieldValueMethod = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue), [typeof(int)])!;
    private static readonly MethodInfo ConvertMethod = typeof(DbValueConverter).GetMethod(nameof(DbValueConverter.Convert), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly Dictionary<Type, MethodInfo> TypedGetters = new()
    {
        [typeof(bool)] = Getter(nameof(DbDataReader.GetBoolean)),
        [typeof(byte)] = Getter(nameof(DbDataReader.GetByte)),
        [typeof(short)] = Getter(nameof(DbDataReader.GetInt16)),
        [typeof(int)] = Getter(nameof(DbDataReader.GetInt32)),
        [typeof(long)] = Getter(nameof(DbDataReader.GetInt64)),
        [typeof(float)] = Getter(nameof(DbDataReader.GetFloat)),
        [typeof(double)] = Getter(nameof(DbDataReader.GetDouble)),
        [typeof(decimal)] = Getter(nameof(DbDataReader.GetDecimal)),
        [typeof(DateTime)] = Getter(nameof(DbDataReader.GetDateTime)),
        [typeof(Guid)] = Getter(nameof(DbDataReader.GetGuid)),
        [typeof(string)] = Getter(nameof(DbDataReader.GetString))
    };

    internal static Func<DbDataReader, T> GetMapper<T>(DbDataReader reader)
    {
        Type type = typeof(T);

        if (type == typeof(object) || type == typeof(Dictionary<string, object?>) ||
            type == typeof(IDictionary<string, object?>) || type == typeof(IReadOnlyDictionary<string, object?>))
        {
            return CreateDictionaryMapper<T>(reader);
        }

        MapperEntry<T>? last = LastMapper<T>.Entry;
        if (last is not null && last.Matches(reader)) return last.Map;

        var key = new MapperKey(type, BuildSignature(reader));
        var map = (Func<DbDataReader, T>)Cache.GetOrAdd(key, static (_, r) => Build<T>(r), reader);
        LastMapper<T>.Entry = new MapperEntry<T>(reader, map);
        return map;
    }

    private static class LastMapper<T>
    {
        internal static volatile MapperEntry<T>? Entry;
    }

    private sealed class MapperEntry<T>
    {
        private readonly string[] _names;
        private readonly Type[] _types;

        internal MapperEntry(DbDataReader reader, Func<DbDataReader, T> map)
        {
            _names = new string[reader.FieldCount];
            _types = new Type[reader.FieldCount];
            for (int i = 0; i < _names.Length; i++)
            {
                _names[i] = reader.GetName(i);
                _types[i] = reader.GetFieldType(i);
            }

            Map = map;
        }

        internal Func<DbDataReader, T> Map { get; }

        internal bool Matches(DbDataReader reader)
        {
            if (reader.FieldCount != _names.Length) return false;

            for (int i = 0; i < _names.Length; i++)
            {
                if (reader.GetFieldType(i) != _types[i] || !string.Equals(reader.GetName(i), _names[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }

    private static string BuildSignature(DbDataReader reader)
    {
        var builder = new StringBuilder(reader.FieldCount * 16);
        for (int i = 0; i < reader.FieldCount; i++)
            builder.Append(reader.GetName(i)).Append(':').Append(reader.GetFieldType(i).Name).Append('|');

        return builder.ToString();
    }

    private static Func<DbDataReader, T> Build<T>(DbDataReader reader)
    {
        ParameterExpression readerParameter = Expression.Parameter(typeof(DbDataReader), "reader");

        Expression body = TypeMetadata.IsScalar(typeof(T))
            ? ReadColumn(readerParameter, 0, typeof(T), reader.GetFieldType(0))
            : BuildObject(typeof(T), readerParameter, reader);

        return Expression.Lambda<Func<DbDataReader, T>>(body, readerParameter).Compile();
    }

    private static Expression BuildObject(Type type, ParameterExpression readerParameter, DbDataReader reader)
    {
        var columns = new Dictionary<string, int>(reader.FieldCount, StringComparer.Ordinal);
        for (int i = 0; i < reader.FieldCount; i++)
            columns.TryAdd(TypeMetadata.NormalizeName(reader.GetName(i)), i);

        var consumed = new HashSet<int>();
        NewExpression construct = BuildConstructor(type, readerParameter, reader, columns, consumed);

        var bindings = new List<MemberBinding>();
        foreach (MappedProperty property in TypeMetadata.GetProperties(type))
        {
            if (!property.CanWrite) continue;
            if (!columns.TryGetValue(property.NormalizedName, out int ordinal) || consumed.Contains(ordinal)) continue;

            bindings.Add(Expression.Bind(property.Property,
                ReadColumn(readerParameter, ordinal, property.Type, reader.GetFieldType(ordinal))));
        }

        return Expression.MemberInit(construct, bindings);
    }

    private static NewExpression BuildConstructor(Type type, ParameterExpression readerParameter, DbDataReader reader,
                                                  Dictionary<string, int> columns, HashSet<int> consumed)
    {
        if (type.IsValueType || type.GetConstructor(Type.EmptyTypes) is not null)
            return Expression.New(type);

        foreach (ConstructorInfo constructor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            var ordinals = new int[parameters.Length];
            bool allMatched = true;

            for (int i = 0; i < parameters.Length && allMatched; i++)
                allMatched = columns.TryGetValue(TypeMetadata.NormalizeName(parameters[i].Name ?? string.Empty), out ordinals[i]);

            if (!allMatched) continue;

            var arguments = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                arguments[i] = ReadColumn(readerParameter, ordinals[i], parameters[i].ParameterType, reader.GetFieldType(ordinals[i]));
                consumed.Add(ordinals[i]);
            }

            return Expression.New(constructor, arguments);
        }

        throw new DatabaseServiceException(
            $"{type.Name} tipi için parametresiz kurucu veya tüm parametreleri sonuç sütunlarıyla eşleşen bir kurucu bulunamadı.");
    }

    private static Expression ReadColumn(ParameterExpression reader, int ordinal, Type targetType, Type fieldType)
    {
        Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        ConstantExpression index = Expression.Constant(ordinal);
        Expression value;

        if (underlying == fieldType)
        {
            value = TypedGetters.TryGetValue(fieldType, out MethodInfo? getter)
                ? Expression.Call(reader, getter, index)
                : Expression.Call(reader, GetFieldValueMethod.MakeGenericMethod(fieldType), index);
        }
        else if (underlying.IsEnum && Enum.GetUnderlyingType(underlying) == fieldType && TypedGetters.TryGetValue(fieldType, out MethodInfo? enumGetter))
        {
            value = Expression.Convert(Expression.Call(reader, enumGetter, index), underlying);
        }
        else if (TryTypedConversion(reader, index, fieldType, underlying, out Expression? converted))
        {
            value = converted;
        }
        else
        {
            value = Expression.Call(ConvertMethod.MakeGenericMethod(underlying), Expression.Call(reader, GetValueMethod, index));
        }

        if (value.Type != targetType)
            value = Expression.Convert(value, targetType);

        return Expression.Condition(
            Expression.Call(reader, IsDBNullMethod, index),
            Expression.Default(targetType),
            value);
    }

    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)
    ];

    private static bool TryTypedConversion(ParameterExpression reader, ConstantExpression index, Type fieldType, Type target,
                                           out Expression result)
    {
        result = null!;

        if (NumericTypes.Contains(fieldType) && TypedGetters.TryGetValue(fieldType, out MethodInfo? numericGetter))
        {
            Expression source = Expression.Call(reader, numericGetter, index);

            if (NumericTypes.Contains(target))
            {
                result = Expression.ConvertChecked(source, target);
                return true;
            }

            if (target.IsEnum)
            {
                result = Expression.Convert(Expression.ConvertChecked(source, Enum.GetUnderlyingType(target)), target);
                return true;
            }

            if (target == typeof(bool))
            {
                result = Expression.NotEqual(source, Expression.Default(fieldType));
                return true;
            }

            return false;
        }

        if (fieldType == typeof(string))
        {
            string? parser = target == typeof(DateTime) ? nameof(DbValueConverter.ParseDateTime)
                : target == typeof(DateOnly) ? nameof(DbValueConverter.ParseDateOnly)
                : target == typeof(TimeOnly) ? nameof(DbValueConverter.ParseTimeOnly)
                : target == typeof(DateTimeOffset) ? nameof(DbValueConverter.ParseDateTimeOffset)
                : target == typeof(Guid) ? nameof(DbValueConverter.ParseGuid)
                : target == typeof(char) ? nameof(DbValueConverter.ParseChar)
                : null;

            if (parser is null) return false;

            MethodInfo method = typeof(DbValueConverter).GetMethod(parser, BindingFlags.Static | BindingFlags.NonPublic)!;
            result = Expression.Call(method, Expression.Call(reader, TypedGetters[typeof(string)], index));
            return true;
        }

        if (fieldType == typeof(DateTime))
        {
            Expression source = Expression.Call(reader, TypedGetters[typeof(DateTime)], index);
            if (target == typeof(DateOnly))
            {
                result = Expression.Call(typeof(DateOnly).GetMethod(nameof(DateOnly.FromDateTime), [typeof(DateTime)])!, source);
                return true;
            }

            if (target == typeof(DateTimeOffset))
            {
                result = Expression.New(typeof(DateTimeOffset).GetConstructor([typeof(DateTime)])!, source);
                return true;
            }
        }

        return false;
    }

    private static Func<DbDataReader, T> CreateDictionaryMapper<T>(DbDataReader reader)
    {
        var names = new string[reader.FieldCount];
        for (int i = 0; i < names.Length; i++)
            names[i] = reader.GetName(i);

        return r =>
        {
            var row = new Dictionary<string, object?>(names.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < names.Length; i++)
                row[names[i]] = r.IsDBNull(i) ? null : r.GetValue(i);

            return (T)(object)row;
        };
    }

    private static MethodInfo Getter(string name) => typeof(DbDataReader).GetMethod(name, [typeof(int)])!;

    private readonly record struct MapperKey(Type Type, string Signature);
}
