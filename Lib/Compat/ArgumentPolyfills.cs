#if !NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal static class ArgumentPolyfills
{
#if !NET6_0_OR_GREATER
    extension(ArgumentNullException)
    {
        public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null) throw new ArgumentNullException(paramName);
        }
    }
#endif

    extension(ArgumentException)
    {
#if !NET7_0_OR_GREATER
        public static void ThrowIfNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null) throw new ArgumentNullException(paramName);
            if (argument.Length == 0) throw new ArgumentException("The value cannot be an empty string.", paramName);
        }
#endif

        public static void ThrowIfNullOrWhiteSpace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null) throw new ArgumentNullException(paramName);
            if (string.IsNullOrWhiteSpace(argument))
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
        }
    }

    extension(ArgumentOutOfRangeException)
    {
        public static void ThrowIfNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default) < 0)
                throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be a non-negative value.");
        }

        public static void ThrowIfNegativeOrZero<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default) <= 0)
                throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be a non-negative and non-zero value.");
        }

        public static void ThrowIfLessThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0)
                throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be greater than or equal to '{other}'.");
        }

        public static void ThrowIfGreaterThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) > 0)
                throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be less than or equal to '{other}'.");
        }
    }

#if !NET7_0_OR_GREATER
    extension(ObjectDisposedException)
    {
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
        {
            if (condition) throw new ObjectDisposedException(instance?.GetType().FullName);
        }
    }
#endif
}
#endif
