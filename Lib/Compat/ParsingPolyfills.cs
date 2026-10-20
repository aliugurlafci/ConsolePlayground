#if !NET6_0_OR_GREATER
using System.Globalization;

namespace System
{
    internal static class EnumPolyfills
    {
        extension(Enum)
        {
            public static TEnum Parse<TEnum>(ReadOnlySpan<char> value, bool ignoreCase = false) where TEnum : struct =>
                (TEnum)Enum.Parse(typeof(TEnum), value.ToString(), ignoreCase);

            public static bool TryParse<TEnum>(ReadOnlySpan<char> value, bool ignoreCase, out TEnum result) where TEnum : struct =>
                Enum.TryParse(value.ToString(), ignoreCase, out result);

#if NETFRAMEWORK
            public static TEnum Parse<TEnum>(string value, bool ignoreCase = false) where TEnum : struct =>
                (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase);
#endif
        }
    }

#if NETFRAMEWORK
    internal static class BytePolyfills
    {
        extension(byte number)
        {
            public static byte Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                byte.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out byte result) => byte.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out byte result) =>
                byte.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class SBytePolyfills
    {
        extension(sbyte number)
        {
            public static sbyte Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                sbyte.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out sbyte result) => sbyte.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out sbyte result) =>
                sbyte.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class Int16Polyfills
    {
        extension(short number)
        {
            public static short Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                short.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out short result) => short.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out short result) =>
                short.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class UInt16Polyfills
    {
        extension(ushort number)
        {
            public static ushort Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                ushort.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out ushort result) => ushort.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out ushort result) =>
                ushort.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class Int32Polyfills
    {
        extension(int number)
        {
            public static int Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                int.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out int result) => int.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out int result) =>
                int.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class UInt32Polyfills
    {
        extension(uint number)
        {
            public static uint Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                uint.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out uint result) => uint.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out uint result) =>
                uint.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class Int64Polyfills
    {
        extension(long number)
        {
            public static long Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                long.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out long result) => long.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out long result) =>
                long.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class UInt64Polyfills
    {
        extension(ulong number)
        {
            public static ulong Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null) =>
                ulong.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out ulong result) => ulong.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out ulong result) =>
                ulong.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class SinglePolyfills
    {
        extension(float number)
        {
            public static float Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null) =>
                float.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out float result) => float.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out float result) =>
                float.TryParse(s.ToString(), style, provider, out result);

            public static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class DoublePolyfills
    {
        extension(double number)
        {
            public static double Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null) =>
                double.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out double result) => double.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out double result) =>
                double.TryParse(s.ToString(), style, provider, out result);

            public static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class DecimalPolyfills
    {
        extension(decimal number)
        {
            public static decimal Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null) =>
                decimal.Parse(s.ToString(), style, provider);

            public static bool TryParse(ReadOnlySpan<char> s, out decimal result) => decimal.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out decimal result) =>
                decimal.TryParse(s.ToString(), style, provider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(number.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class BooleanPolyfills
    {
        extension(bool flag)
        {
            public static bool Parse(ReadOnlySpan<char> value) => bool.Parse(value.ToString());

            public static bool TryParse(ReadOnlySpan<char> value, out bool result) => bool.TryParse(value.ToString(), out result);

            public bool TryFormat(Span<char> destination, out int charsWritten) =>
                FormatHelper.TryCopy(flag.ToString(), destination, out charsWritten);
        }
    }

    internal static class GuidPolyfills
    {
        extension(Guid guid)
        {
            public static Guid Parse(ReadOnlySpan<char> input) => Guid.Parse(input.ToString());

            public static bool TryParse(ReadOnlySpan<char> input, out Guid result) => Guid.TryParse(input.ToString(), out result);

            public static Guid ParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format) =>
                Guid.ParseExact(input.ToString(), format.ToString());

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default) =>
                FormatHelper.TryCopy(guid.ToString(FormatHelper.ToFormat(format)), destination, out charsWritten);
        }
    }

    internal static class DateTimePolyfills
    {
        extension(DateTime dateTime)
        {
            public static DateTime Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null, DateTimeStyles styles = DateTimeStyles.None) =>
                DateTime.Parse(s.ToString(), provider, styles);

            public static DateTime ParseExact(ReadOnlySpan<char> s, ReadOnlySpan<char> format, IFormatProvider? provider,
                                              DateTimeStyles style = DateTimeStyles.None) =>
                DateTime.ParseExact(s.ToString(), format.ToString(), provider, style);

            public static bool TryParse(ReadOnlySpan<char> s, out DateTime result) => DateTime.TryParse(s.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, DateTimeStyles styles, out DateTime result) =>
                DateTime.TryParse(s.ToString(), provider, styles, out result);

            public static bool TryParseExact(ReadOnlySpan<char> s, ReadOnlySpan<char> format, IFormatProvider? provider,
                                             DateTimeStyles style, out DateTime result) =>
                DateTime.TryParseExact(s.ToString(), format.ToString(), provider, style, out result);

            public static bool TryParseExact(ReadOnlySpan<char> s, string?[]? formats, IFormatProvider? provider,
                                             DateTimeStyles style, out DateTime result) =>
                DateTime.TryParseExact(s.ToString(), formats, provider, style, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
                FormatHelper.TryCopy(dateTime.ToString(FormatHelper.ToFormat(format), provider), destination, out charsWritten);
        }
    }

    internal static class DateTimeOffsetPolyfills
    {
        extension(DateTimeOffset dateTimeOffset)
        {
            public static DateTimeOffset Parse(ReadOnlySpan<char> input, IFormatProvider? formatProvider = null,
                                               DateTimeStyles styles = DateTimeStyles.None) =>
                DateTimeOffset.Parse(input.ToString(), formatProvider, styles);

            public static DateTimeOffset ParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format, IFormatProvider? formatProvider,
                                                    DateTimeStyles styles = DateTimeStyles.None) =>
                DateTimeOffset.ParseExact(input.ToString(), format.ToString(), formatProvider, styles);

            public static bool TryParse(ReadOnlySpan<char> input, out DateTimeOffset result) =>
                DateTimeOffset.TryParse(input.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? formatProvider, DateTimeStyles styles, out DateTimeOffset result) =>
                DateTimeOffset.TryParse(input.ToString(), formatProvider, styles, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null) =>
                FormatHelper.TryCopy(dateTimeOffset.ToString(FormatHelper.ToFormat(format), formatProvider), destination, out charsWritten);
        }
    }

    internal static class TimeSpanPolyfills
    {
        extension(TimeSpan timeSpan)
        {
            public static TimeSpan Parse(ReadOnlySpan<char> input, IFormatProvider? formatProvider = null) =>
                TimeSpan.Parse(input.ToString(), formatProvider);

            public static TimeSpan ParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format, IFormatProvider? formatProvider,
                                              TimeSpanStyles styles = TimeSpanStyles.None) =>
                TimeSpan.ParseExact(input.ToString(), format.ToString(), formatProvider, styles);

            public static bool TryParse(ReadOnlySpan<char> input, out TimeSpan result) => TimeSpan.TryParse(input.ToString(), out result);

            public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? formatProvider, out TimeSpan result) =>
                TimeSpan.TryParse(input.ToString(), formatProvider, out result);

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null) =>
                FormatHelper.TryCopy(timeSpan.ToString(FormatHelper.ToFormat(format), formatProvider), destination, out charsWritten);
        }
    }

    internal static class FormatHelper
    {
        internal static string? ToFormat(ReadOnlySpan<char> format) => format.IsEmpty ? null : format.ToString();

        internal static bool TryCopy(string text, Span<char> destination, out int charsWritten)
        {
            if (text.Length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }

            text.AsSpan().CopyTo(destination);
            charsWritten = text.Length;
            return true;
        }
    }
#endif
}
#endif
