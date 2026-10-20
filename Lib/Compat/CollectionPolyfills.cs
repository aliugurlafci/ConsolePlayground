// Koleksiyon yardımcıları: Enumerable.Chunk ve PriorityQueue (.NET 6), Enumerable.Order (.NET 7);
// net48 için KeyValuePair.Deconstruct, Dictionary/Queue Try* metotları ve ReferenceEqualityComparer.

#if !NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Linq
{
    internal static class EnumerablePolyfills
    {
        extension<TSource>(IEnumerable<TSource> source)
        {
#if !NET6_0_OR_GREATER
            public IEnumerable<TSource[]> Chunk(int size)
            {
                if (source is null) throw new ArgumentNullException(nameof(source));
                if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));

                return ChunkIterator(source, size);
            }
#endif

            public IOrderedEnumerable<TSource> Order(IComparer<TSource>? comparer = null) =>
                source.OrderBy(static item => item, comparer);

            public IOrderedEnumerable<TSource> OrderDescending(IComparer<TSource>? comparer = null) =>
                source.OrderByDescending(static item => item, comparer);
        }

#if !NET6_0_OR_GREATER
        private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source, int size)
        {
            using IEnumerator<TSource> enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var chunk = new List<TSource>(size) { enumerator.Current };
                while (chunk.Count < size && enumerator.MoveNext())
                    chunk.Add(enumerator.Current);

                yield return chunk.ToArray();
            }
        }
#endif
    }
}

namespace System.Collections.Generic
{
#if NETFRAMEWORK
    internal static class CollectionPolyfills
    {
        extension<TKey, TValue>(KeyValuePair<TKey, TValue> pair)
        {
            public void Deconstruct(out TKey key, out TValue value)
            {
                key = pair.Key;
                value = pair.Value;
            }
        }

        extension<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
        {
            public bool TryAdd(TKey key, TValue value)
            {
                if (dictionary.ContainsKey(key)) return false;

                dictionary.Add(key, value);
                return true;
            }

            public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
            {
                if (!dictionary.TryGetValue(key, out value)) return false;

                dictionary.Remove(key);
                return true;
            }
        }

        extension<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            public TValue? GetValueOrDefault(TKey key) => dictionary.TryGetValue(key, out TValue? value) ? value : default;

            public TValue GetValueOrDefault(TKey key, TValue defaultValue) =>
                dictionary.TryGetValue(key, out TValue? value) ? value : defaultValue;
        }

        extension<T>(Queue<T> queue)
        {
            public bool TryDequeue([MaybeNullWhen(false)] out T result)
            {
                if (queue.Count == 0)
                {
                    result = default;
                    return false;
                }

                result = queue.Dequeue();
                return true;
            }

            public bool TryPeek([MaybeNullWhen(false)] out T result)
            {
                if (queue.Count == 0)
                {
                    result = default;
                    return false;
                }

                result = queue.Peek();
                return true;
            }
        }

        extension<T>(Stack<T> stack)
        {
            public bool TryPop([MaybeNullWhen(false)] out T result)
            {
                if (stack.Count == 0)
                {
                    result = default;
                    return false;
                }

                result = stack.Pop();
                return true;
            }

            public bool TryPeek([MaybeNullWhen(false)] out T result)
            {
                if (stack.Count == 0)
                {
                    result = default;
                    return false;
                }

                result = stack.Peek();
                return true;
            }
        }
    }

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object?>, System.Collections.IEqualityComparer
    {
        private ReferenceEqualityComparer()
        {
        }

        public static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object? obj) => RuntimeHelpers.GetHashCode(obj!);
    }
#endif

#if !NET6_0_OR_GREATER
    internal sealed class PriorityQueue<TElement, TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _nodes;
        private readonly IComparer<TPriority> _comparer;

        public PriorityQueue() : this(0, null)
        {
        }

        public PriorityQueue(IComparer<TPriority>? comparer) : this(0, comparer)
        {
        }

        public PriorityQueue(int initialCapacity, IComparer<TPriority>? comparer = null)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _nodes = new List<(TElement, TPriority)>(initialCapacity);
            _comparer = comparer ?? Comparer<TPriority>.Default;
        }

        public int Count => _nodes.Count;

        public IComparer<TPriority> Comparer => _comparer;

        public void Enqueue(TElement element, TPriority priority)
        {
            _nodes.Add((element, priority));
            MoveUp(_nodes.Count - 1);
        }

        public TElement Peek()
        {
            if (_nodes.Count == 0) throw new InvalidOperationException("Queue empty.");
            return _nodes[0].Element;
        }

        public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_nodes.Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            (element, priority) = _nodes[0];
            return true;
        }

        public TElement Dequeue()
        {
            if (!TryDequeue(out TElement? element, out _)) throw new InvalidOperationException("Queue empty.");
            return element;
        }

        public bool TryDequeue([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_nodes.Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            (element, priority) = _nodes[0];
            RemoveRoot();
            return true;
        }

        public TElement EnqueueDequeue(TElement element, TPriority priority)
        {
            if (_nodes.Count == 0 || _comparer.Compare(priority, _nodes[0].Priority) <= 0)
                return element;

            TElement root = _nodes[0].Element;
            _nodes[0] = (element, priority);
            MoveDown(0);
            return root;
        }

        public void Clear() => _nodes.Clear();

        private void RemoveRoot()
        {
            int last = _nodes.Count - 1;
            _nodes[0] = _nodes[last];
            _nodes.RemoveAt(last);
            if (_nodes.Count > 0) MoveDown(0);
        }

        private void MoveUp(int index)
        {
            (TElement, TPriority Priority) node = _nodes[index];
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (_comparer.Compare(node.Priority, _nodes[parent].Priority) >= 0) break;

                _nodes[index] = _nodes[parent];
                index = parent;
            }

            _nodes[index] = node;
        }

        private void MoveDown(int index)
        {
            (TElement, TPriority Priority) node = _nodes[index];
            int count = _nodes.Count;
            while (true)
            {
                int child = (index << 1) + 1;
                if (child >= count) break;

                if (child + 1 < count && _comparer.Compare(_nodes[child + 1].Priority, _nodes[child].Priority) < 0)
                    child++;

                if (_comparer.Compare(node.Priority, _nodes[child].Priority) <= 0) break;

                _nodes[index] = _nodes[child];
                index = child;
            }

            _nodes[index] = node;
        }
    }
#endif
}
#endif
