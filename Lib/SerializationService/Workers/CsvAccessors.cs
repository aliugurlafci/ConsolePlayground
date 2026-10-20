using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace CSPLibraries.Serialization.Workers;

internal delegate void SpanSetter<in T>(T target, ReadOnlySpan<char> text);

internal delegate TValue SpanParser<out TValue>(ReadOnlySpan<char> text);

internal static class CsvAccessors
{
    private const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;

    internal static SpanSetter<T> CreateSetter<T>(RecordField<T> field, CultureInfo culture, string? format)
    {
        Type type = Nullable.GetUnderlyingType(field.Type) ?? field.Type;

        if (type == typeof(string))
        {
            Action<T, string> assign = CompileAssign<T, string>(field.Property);
            return (target, text) => assign(target, text.ToString());
        }

        Delegate? parser = CreateParser(type, culture, format);
        if (parser is null)
        {
            Func<string, object?> fallback = TextValueConverter.CreateParser(field.Type, culture, format);
            Action<T, object?> setter = field.Setter!;
            return (target, text) =>
            {
                if (text.IsEmpty) return;
                object? value = fallback(text.ToString());
                if (value is not null) setter(target, value);
            };
        }

        return (SpanSetter<T>)typeof(CsvAccessors).GetMethod(nameof(CreateTypedSetter), Private)!
            .MakeGenericMethod(typeof(T), type)
            .Invoke(null, [field.Property, parser])!;
    }

    private static SpanSetter<T> CreateTypedSetter<T, TValue>(PropertyInfo property, SpanParser<TValue> parse)
    {
        Action<T, TValue> assign = CompileAssign<T, TValue>(property);
        return (target, text) =>
        {
            if (!text.IsEmpty) assign(target, parse(text));
        };
    }

    private static Action<T, TValue> CompileAssign<T, TValue>(PropertyInfo property)
    {
        ParameterExpression target = Expression.Parameter(typeof(T), "target");
        ParameterExpression value = Expression.Parameter(typeof(TValue), "value");
        Expression converted = property.PropertyType == typeof(TValue) ? value : Expression.Convert(value, property.PropertyType);
        return Expression.Lambda<Action<T, TValue>>(Expression.Assign(Expression.Property(target, property), converted), target, value).Compile();
    }

    private static Delegate? CreateParser(Type type, CultureInfo culture, string? format)
    {
        if (type.IsEnum)
            return (Delegate)typeof(CsvAccessors).GetMethod(nameof(EnumParser), Private)!.MakeGenericMethod(type).Invoke(null, null)!;

        if (type == typeof(bool)) return new SpanParser<bool>(ParseBoolean);
        if (type == typeof(char)) return new SpanParser<char>(static text => text[0]);
        if (type == typeof(Guid)) return new SpanParser<Guid>(static text => Guid.Parse(text));

        if (type == typeof(DateTime))
            return format is null
                ? new SpanParser<DateTime>(text => DateTime.Parse(text, culture, DateTimeStyles.RoundtripKind))
                : new SpanParser<DateTime>(text => DateTime.ParseExact(text, format, culture));
        if (type == typeof(DateOnly))
            return format is null
                ? new SpanParser<DateOnly>(text => DateOnly.Parse(text, culture))
                : new SpanParser<DateOnly>(text => DateOnly.ParseExact(text, format, culture));
        if (type == typeof(TimeOnly))
            return format is null
                ? new SpanParser<TimeOnly>(text => TimeOnly.Parse(text, culture))
                : new SpanParser<TimeOnly>(text => TimeOnly.ParseExact(text, format, culture));
        if (type == typeof(DateTimeOffset)) return new SpanParser<DateTimeOffset>(text => DateTimeOffset.Parse(text, culture));
        if (type == typeof(TimeSpan)) return new SpanParser<TimeSpan>(text => TimeSpan.Parse(text, culture));

        NumberStyles? styles = type == typeof(decimal) ? NumberStyles.Number
            : type == typeof(double) || type == typeof(float) ? NumberStyles.Float | NumberStyles.AllowThousands
            : type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
              type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte)
                ? NumberStyles.Integer | NumberStyles.AllowThousands
                : null;

        return styles is null
            ? null
            : (Delegate)typeof(CsvAccessors).GetMethod(nameof(NumberParser), Private)!.MakeGenericMethod(type).Invoke(null, [styles.Value, culture])!;
    }

#if NET7_0_OR_GREATER
    private static SpanParser<TNumber> NumberParser<TNumber>(NumberStyles styles, CultureInfo culture) where TNumber : INumberBase<TNumber> =>
        text => TNumber.Parse(text, styles, culture);
#else
    private static SpanParser<TNumber> NumberParser<TNumber>(NumberStyles styles, CultureInfo culture)
    {
        Delegate parser = Type.GetTypeCode(typeof(TNumber)) switch
        {
            TypeCode.Byte => new SpanParser<byte>(text => byte.Parse(text, styles, culture)),
            TypeCode.SByte => new SpanParser<sbyte>(text => sbyte.Parse(text, styles, culture)),
            TypeCode.Int16 => new SpanParser<short>(text => short.Parse(text, styles, culture)),
            TypeCode.UInt16 => new SpanParser<ushort>(text => ushort.Parse(text, styles, culture)),
            TypeCode.Int32 => new SpanParser<int>(text => int.Parse(text, styles, culture)),
            TypeCode.UInt32 => new SpanParser<uint>(text => uint.Parse(text, styles, culture)),
            TypeCode.Int64 => new SpanParser<long>(text => long.Parse(text, styles, culture)),
            TypeCode.UInt64 => new SpanParser<ulong>(text => ulong.Parse(text, styles, culture)),
            TypeCode.Single => new SpanParser<float>(text => float.Parse(text, styles, culture)),
            TypeCode.Double => new SpanParser<double>(text => double.Parse(text, styles, culture)),
            TypeCode.Decimal => new SpanParser<decimal>(text => decimal.Parse(text, styles, culture)),
            _ => throw new NotSupportedException(typeof(TNumber).FullName)
        };

        return (SpanParser<TNumber>)parser;
    }
#endif

    private static SpanParser<TEnum> EnumParser<TEnum>() where TEnum : struct, Enum =>
        static text => Enum.Parse<TEnum>(text, ignoreCase: true);

    private static bool ParseBoolean(ReadOnlySpan<char> text)
    {
        if (bool.TryParse(text, out bool result)) return result;
        if (text.Length == 1)
        {
            switch (char.ToUpperInvariant(text[0]))
            {
                case '1' or 'X' or 'Y' or 'E': return true;
                case '0' or 'N' or 'H': return false;
            }
        }

        if (text.Equals("EVET", StringComparison.OrdinalIgnoreCase) || text.Equals("YES", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("HAYIR", StringComparison.OrdinalIgnoreCase) || text.Equals("NO", StringComparison.OrdinalIgnoreCase)) return false;

        throw new FormatException($"'{text.ToString()}' mantıksal (bool) değere çevrilemedi.");
    }
}
