#if !NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace System
{
    internal static class MemoryPolyfills
    {
        extension(ReadOnlySpan<char> span)
        {
            public int IndexOfAnyInRange(char lowInclusive, char highInclusive)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (char.IsBetween(span[i], lowInclusive, highInclusive)) return i;
                }

                return -1;
            }

            public int IndexOfAnyExceptInRange(char lowInclusive, char highInclusive)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (!char.IsBetween(span[i], lowInclusive, highInclusive)) return i;
                }

                return -1;
            }

            public bool ContainsAnyInRange(char lowInclusive, char highInclusive) =>
                span.IndexOfAnyInRange(lowInclusive, highInclusive) >= 0;

            public bool ContainsAnyExceptInRange(char lowInclusive, char highInclusive) =>
                span.IndexOfAnyExceptInRange(lowInclusive, highInclusive) >= 0;

            public int Count(char value)
            {
                int count = 0;
                foreach (char c in span)
                {
                    if (c == value) count++;
                }

                return count;
            }
        }

        extension(Span<char> span)
        {
            public int IndexOfAnyInRange(char lowInclusive, char highInclusive) =>
                ((ReadOnlySpan<char>)span).IndexOfAnyInRange(lowInclusive, highInclusive);

            public int IndexOfAnyExceptInRange(char lowInclusive, char highInclusive) =>
                ((ReadOnlySpan<char>)span).IndexOfAnyExceptInRange(lowInclusive, highInclusive);

            public int Count(char value) => ((ReadOnlySpan<char>)span).Count(value);
        }

#if !NET7_0_OR_GREATER
        extension<T>(ReadOnlySpan<T> span) where T : IEquatable<T>
        {
            public int IndexOfAnyExcept(T value)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (!span[i].Equals(value)) return i;
                }

                return -1;
            }

            public int IndexOfAnyExcept(ReadOnlySpan<T> values)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (values.IndexOf(span[i]) < 0) return i;
                }

                return -1;
            }
        }

        extension<T>(Span<T> span) where T : IEquatable<T>
        {
            public int IndexOfAnyExcept(T value) => ((ReadOnlySpan<T>)span).IndexOfAnyExcept(value);

            public int IndexOfAnyExcept(ReadOnlySpan<T> values) => ((ReadOnlySpan<T>)span).IndexOfAnyExcept(values);
        }
#endif

#if NETFRAMEWORK
        extension<T>(ReadOnlySpan<T> span) where T : IEquatable<T>
        {
            public bool Contains(T value) => span.IndexOf(value) >= 0;
        }

        extension<T>(Span<T> span) where T : IEquatable<T>
        {
            public bool Contains(T value) => ((ReadOnlySpan<T>)span).IndexOf(value) >= 0;
        }
#endif
    }
}

#if NETFRAMEWORK
namespace System.Text
{
    internal static class EncodingPolyfills
    {
        extension(Encoding encoding)
        {
            public int GetByteCount(ReadOnlySpan<char> chars)
            {
                if (chars.IsEmpty) return 0;

                char[] buffer = ArrayPool<char>.Shared.Rent(chars.Length);
                try
                {
                    chars.CopyTo(buffer);
                    return encoding.GetByteCount(buffer, 0, chars.Length);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer, clearArray: true);
                }
            }

            public int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
            {
                if (chars.IsEmpty) return 0;

                char[] charBuffer = ArrayPool<char>.Shared.Rent(chars.Length);
                byte[] byteBuffer = ArrayPool<byte>.Shared.Rent(encoding.GetMaxByteCount(chars.Length));
                try
                {
                    chars.CopyTo(charBuffer);
                    int written = encoding.GetBytes(charBuffer, 0, chars.Length, byteBuffer, 0);
                    if (written > bytes.Length) throw new ArgumentException("Destination is too short.", nameof(bytes));

                    byteBuffer.AsSpan(0, written).CopyTo(bytes);
                    return written;
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(charBuffer, clearArray: true);
                    ArrayPool<byte>.Shared.Return(byteBuffer, clearArray: true);
                }
            }

            public int GetCharCount(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? 0 : encoding.GetCharCount(bytes.ToArray());

            public int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars)
            {
                if (bytes.IsEmpty) return 0;

                char[] decoded = encoding.GetChars(bytes.ToArray());
                if (decoded.Length > chars.Length) throw new ArgumentException("Destination is too short.", nameof(chars));

                decoded.CopyTo(chars);
                return decoded.Length;
            }

            public string GetString(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? string.Empty : encoding.GetString(bytes.ToArray());
        }
    }
}

namespace System.IO
{
    internal static class TextWriterSpanPolyfills
    {
        extension(TextWriter writer)
        {
            public void Write(ReadOnlySpan<char> buffer)
            {
                if (buffer.IsEmpty) return;

                char[] array = ArrayPool<char>.Shared.Rent(buffer.Length);
                try
                {
                    buffer.CopyTo(array);
                    writer.Write(array, 0, buffer.Length);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(array);
                }
            }

            public void WriteLine(ReadOnlySpan<char> buffer)
            {
                writer.Write(buffer);
                writer.WriteLine();
            }

            public Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

                return MemoryMarshal.TryGetArray(buffer, out ArraySegment<char> segment)
                    ? writer.WriteAsync(segment.Array!, segment.Offset, segment.Count)
                    : writer.WriteAsync(buffer.ToArray());
            }

            public Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

                return MemoryMarshal.TryGetArray(buffer, out ArraySegment<char> segment)
                    ? writer.WriteLineAsync(segment.Array!, segment.Offset, segment.Count)
                    : writer.WriteLineAsync(buffer.ToArray());
            }
        }
    }
}
#endif
#endif
