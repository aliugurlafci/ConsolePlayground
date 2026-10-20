#if !NET8_0_OR_GREATER
namespace System.Buffers
{
    internal static class SearchValues
    {
        public static SearchValues<char> Create(ReadOnlySpan<char> values) => new CharSearchValues(values);
    }

    internal abstract class SearchValues<T> where T : IEquatable<T>
    {
        public abstract bool Contains(T value);
    }

    internal sealed class CharSearchValues : SearchValues<char>
    {
        private readonly bool[] _ascii = new bool[128];
        private readonly char[] _others;

        internal CharSearchValues(ReadOnlySpan<char> values)
        {
            var others = new List<char>();
            foreach (char c in values)
            {
                if (c < 128) _ascii[c] = true;
                else if (!others.Contains(c)) others.Add(c);
            }

            _others = others.ToArray();
        }

        public override bool Contains(char value) => value < 128 ? _ascii[value] : Array.IndexOf(_others, value) >= 0;
    }
}

namespace System
{
    using System.Buffers;

    internal static class SearchValuesPolyfills
    {
        extension(ReadOnlySpan<char> span)
        {
            public int IndexOfAny(SearchValues<char> values)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (values.Contains(span[i])) return i;
                }

                return -1;
            }

            public int IndexOfAnyExcept(SearchValues<char> values)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    if (!values.Contains(span[i])) return i;
                }

                return -1;
            }

            public bool ContainsAny(SearchValues<char> values) => span.IndexOfAny(values) >= 0;
        }

        extension(Span<char> span)
        {
            public int IndexOfAny(SearchValues<char> values) => ((ReadOnlySpan<char>)span).IndexOfAny(values);

            public int IndexOfAnyExcept(SearchValues<char> values) => ((ReadOnlySpan<char>)span).IndexOfAnyExcept(values);

            public bool ContainsAny(SearchValues<char> values) => ((ReadOnlySpan<char>)span).IndexOfAny(values) >= 0;
        }
    }
}
#endif
