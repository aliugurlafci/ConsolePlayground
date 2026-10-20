// Metin yardımcıları: string/char/StringBuilder üzerindeki span ve karakter tabanlı API'ler.
// net48 için .NET Core 2.x/3.x API'leri; net5.0 için char.IsAscii* (.NET 7) ve ReplaceLineEndings (.NET 6).

#if !NET7_0_OR_GREATER
using System.Buffers;
using System.Text;

namespace System
{
    internal static class StringPolyfills
    {
        extension(char)
        {
            public static bool IsAsciiDigit(char c) => (uint)(c - '0') <= '9' - '0';

            public static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';

            public static bool IsAsciiLetterOrDigit(char c) => char.IsAsciiLetter(c) || char.IsAsciiDigit(c);

            public static bool IsAsciiLetterUpper(char c) => (uint)(c - 'A') <= 'Z' - 'A';

            public static bool IsAsciiLetterLower(char c) => (uint)(c - 'a') <= 'z' - 'a';

            public static bool IsAsciiHexDigit(char c) =>
                char.IsAsciiDigit(c) || (uint)((c | 0x20) - 'a') <= 'f' - 'a';

            public static bool IsBetween(char c, char minInclusive, char maxInclusive) =>
                (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);
        }

#if !NET6_0_OR_GREATER
        extension(string text)
        {
            public string ReplaceLineEndings() => text.ReplaceLineEndings(Environment.NewLine);

            public string ReplaceLineEndings(string replacementText)
            {
                if (replacementText is null) throw new ArgumentNullException(nameof(replacementText));

                int first = IndexOfNewLine(text, 0);
                if (first < 0) return text;

                var builder = new StringBuilder(text.Length);
                int copied = 0;
                for (int i = first; i >= 0; i = IndexOfNewLine(text, copied))
                {
                    builder.Append(text, copied, i - copied).Append(replacementText);
                    copied = i + (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1);
                }

                return builder.Append(text, copied, text.Length - copied).ToString();
            }
        }

        private static int IndexOfNewLine(string text, int start) =>
            start >= text.Length ? -1 : text.IndexOfAny(NewLineChars, start);

        private static readonly char[] NewLineChars = ['\r', '\n', '\f', '\u0085', '\u2028', '\u2029'];
#endif

#if NETFRAMEWORK
        extension(string text)
        {
            public bool Contains(char value) => text.IndexOf(value) >= 0;

            public bool Contains(char value, StringComparison comparisonType) => text.IndexOf(value.ToString(), comparisonType) >= 0;

            public bool Contains(string value, StringComparison comparisonType) => text.IndexOf(value, comparisonType) >= 0;

            public bool StartsWith(char value) => text.Length > 0 && text[0] == value;

            public bool EndsWith(char value) => text.Length > 0 && text[text.Length - 1] == value;

            public int IndexOf(char value, StringComparison comparisonType) => text.IndexOf(value.ToString(), comparisonType);

            public string[] Split(char separator, StringSplitOptions options = StringSplitOptions.None) =>
                text.Split([separator], options);

            public string[] Split(char separator, int count, StringSplitOptions options = StringSplitOptions.None) =>
                text.Split([separator], count, options);

            public string[] Split(string? separator, StringSplitOptions options = StringSplitOptions.None)
            {
                if (!string.IsNullOrEmpty(separator)) return text.Split([separator!], options);
                return text.Length == 0 && (options & StringSplitOptions.RemoveEmptyEntries) != 0 ? [] : [text];
            }

            public string Replace(string oldValue, string? newValue, StringComparison comparisonType)
            {
                if (oldValue is null) throw new ArgumentNullException(nameof(oldValue));
                if (oldValue.Length == 0) throw new ArgumentException("String cannot be of zero length.", nameof(oldValue));

                int index = text.IndexOf(oldValue, comparisonType);
                if (index < 0) return text;

                var builder = new StringBuilder(text.Length);
                int copied = 0;
                while (index >= 0)
                {
                    builder.Append(text, copied, index - copied).Append(newValue);
                    copied = index + oldValue.Length;
                    index = copied < text.Length ? text.IndexOf(oldValue, copied, comparisonType) : -1;
                }

                return builder.Append(text, copied, text.Length - copied).ToString();
            }
        }

        extension(string)
        {
            public static string Create<TState>(int length, TState state, SpanAction<char, TState> action)
            {
                if (action is null) throw new ArgumentNullException(nameof(action));
                if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
                if (length == 0) return string.Empty;

                var buffer = new char[length];
                action(buffer, state);
                return new string(buffer);
            }

            public static string Join(char separator, params string?[] value) => string.Join(separator.ToString(), value);

            public static string Join(char separator, params object?[] values) => string.Join(separator.ToString(), values);

            public static string Join<T>(char separator, IEnumerable<T> values) => string.Join(separator.ToString(), values);

            public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
            {
                var buffer = new char[str0.Length + str1.Length];
                str0.CopyTo(buffer);
                str1.CopyTo(buffer.AsSpan(str0.Length));
                return new string(buffer);
            }

            public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1, ReadOnlySpan<char> str2)
            {
                var buffer = new char[str0.Length + str1.Length + str2.Length];
                str0.CopyTo(buffer);
                str1.CopyTo(buffer.AsSpan(str0.Length));
                str2.CopyTo(buffer.AsSpan(str0.Length + str1.Length));
                return new string(buffer);
            }
        }
#endif
    }
}

#if NETFRAMEWORK
namespace System.Buffers
{
    internal delegate void SpanAction<T, in TArg>(Span<T> span, TArg arg);
}

namespace System.Text
{
    internal static class StringBuilderPolyfills
    {
        extension(StringBuilder builder)
        {
            public StringBuilder Append(ReadOnlySpan<char> value)
            {
                if (value.IsEmpty) return builder;

                char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
                try
                {
                    value.CopyTo(buffer);
                    return builder.Append(buffer, 0, value.Length);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }

            public StringBuilder Append(ReadOnlyMemory<char> value) => builder.Append(value.Span);

            public StringBuilder AppendJoin<T>(string? separator, IEnumerable<T> values) =>
                builder.Append(string.Join(separator, values));

            public StringBuilder AppendJoin<T>(char separator, IEnumerable<T> values) =>
                builder.Append(string.Join(separator.ToString(), values));

            public StringBuilder AppendJoin(string? separator, params string?[] values) =>
                builder.Append(string.Join(separator, values));

            public StringBuilder AppendJoin(char separator, params string?[] values) =>
                builder.Append(string.Join(separator.ToString(), values));

            public bool Equals(ReadOnlySpan<char> span)
            {
                if (builder.Length != span.Length) return false;

                for (int i = 0; i < span.Length; i++)
                {
                    if (builder[i] != span[i]) return false;
                }

                return true;
            }

            public IEnumerable<ReadOnlyMemory<char>> GetChunks()
            {
                yield return builder.ToString().AsMemory();
            }
        }
    }
}
#endif
#endif
