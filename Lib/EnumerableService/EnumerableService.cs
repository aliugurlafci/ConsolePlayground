using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CSPLibraries.Enumerables.Constants;
using CSPLibraries.Enumerables.Models;
using CSPLibraries.Enumerables.Workers;

namespace CSPLibraries.Enumerables;

public static class EnumerableService
{
    #region Null / boş

    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? source) => source switch
    {
        null => true,
        ICollection<T> collection => collection.Count == 0,
        IReadOnlyCollection<T> collection => collection.Count == 0,
        _ => !source.Any()
    };

    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? source) => source ?? [];

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class
    {
        foreach (T? item in source)
        {
            if (item is not null) yield return item;
        }
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : struct
    {
        foreach (T? item in source)
        {
            if (item.HasValue) yield return item.Value;
        }
    }

    #endregion

    #region Ayrıştırma / arama

    public static (List<T> Matched, List<T> Unmatched) Partition<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var matched = new List<T>();
        var unmatched = new List<T>();

        foreach (T item in source)
            (predicate(item) ? matched : unmatched).Add(item);

        return (matched, unmatched);
    }

    public static IEnumerable<IGrouping<TKey, T>> Duplicates<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector,
                                                                    IEqualityComparer<TKey>? comparer = null) =>
        source.GroupBy(keySelector, comparer).Where(group => group.Skip(1).Any());

    public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        int index = 0;
        foreach (T item in source)
        {
            if (predicate(item)) return index;
            index++;
        }

        return -1;
    }

    public static Dictionary<TKey, TValue> ToDictionarySafe<T, TKey, TValue>(this IEnumerable<T> source,
                                                                            Func<T, TKey> keySelector,
                                                                            Func<T, TValue> valueSelector,
                                                                            IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var result = source is ICollection<T> collection
            ? new Dictionary<TKey, TValue>(collection.Count, comparer)
            : new Dictionary<TKey, TValue>(comparer);

        foreach (T item in source)
            result.TryAdd(keySelector(item), valueSelector(item));

        return result;
    }

    public static Dictionary<TKey, T> ToDictionarySafe<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector,
                                                               IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull =>
        source.ToDictionarySafe(keySelector, static item => item, comparer);

    public static IEnumerable<(TLeft? Left, TRight? Right)> FullOuterJoin<TLeft, TRight, TKey>(
        this IEnumerable<TLeft> left, IEnumerable<TRight> right,
        Func<TLeft, TKey> leftKey, Func<TRight, TKey> rightKey)
        where TKey : notnull
    {
        ILookup<TKey, TRight> rightLookup = right.ToLookup(rightKey);
        var matchedKeys = new HashSet<TKey>();

        foreach (TLeft l in left)
        {
            TKey key = leftKey(l);
            if (rightLookup.Contains(key))
            {
                matchedKeys.Add(key);
                foreach (TRight r in rightLookup[key])
                    yield return (l, r);
            }
            else
            {
                yield return (l, default);
            }
        }

        foreach (IGrouping<TKey, TRight> group in rightLookup)
        {
            if (matchedKeys.Contains(group.Key)) continue;
            foreach (TRight r in group)
                yield return (default, r);
        }
    }

    public static IEnumerable<T> Flatten<T>(this IEnumerable<T> roots, Func<T, IEnumerable<T>?> childrenSelector)
    {
        var stack = new Stack<IEnumerator<T>>();
        stack.Push(roots.GetEnumerator());

        try
        {
            while (stack.Count > 0)
            {
                IEnumerator<T> current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop().Dispose();
                    continue;
                }

                T item = current.Current;
                yield return item;

                IEnumerable<T>? children = childrenSelector(item);
                if (children is not null)
                    stack.Push(children.GetEnumerator());
            }
        }
        finally
        {
            while (stack.Count > 0)
                stack.Pop().Dispose();
        }
    }

    #endregion

    #region Sayfalama

    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);

        IReadOnlyCollection<T> collection = source as IReadOnlyCollection<T> ?? source.ToList();
        List<T> items = collection.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<T>(items, pageNumber, pageSize, collection.Count);
    }

    public static PagedResult<T> ToPagedResult<T>(this IQueryable<T> source, int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);

        int total = source.Count();
        List<T> items = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<T>(items, pageNumber, pageSize, total);
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, EnumerableDefaults.MaxPageSize);
    }

    #endregion

    #region Toplu ve paralel işleme

    public static async Task ProcessInBatchesAsync<T>(this IEnumerable<T> source, int batchSize,
                                                      Func<T[], CancellationToken, Task> processBatch,
                                                      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processBatch);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        foreach (T[] batch in source.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await processBatch(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    public static Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, CancellationToken, ValueTask> body,
                                               int? maxDegreeOfParallelism = null,
                                               CancellationToken cancellationToken = default) =>
        Parallel.ForEachAsync(source, CreateParallelOptions(maxDegreeOfParallelism, cancellationToken), body);

    public static async Task<TResult[]> SelectParallelAsync<T, TResult>(this IEnumerable<T> source,
                                                                        Func<T, CancellationToken, ValueTask<TResult>> selector,
                                                                        int? maxDegreeOfParallelism = null,
                                                                        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);

        IReadOnlyList<T> items = source as IReadOnlyList<T> ?? source.ToList();
        var results = new TResult[items.Count];

        await Parallel.ForAsync(0, items.Count, CreateParallelOptions(maxDegreeOfParallelism, cancellationToken),
            async (index, token) => results[index] = await selector(items[index], token).ConfigureAwait(false))
            .ConfigureAwait(false);

        return results;
    }

    private static ParallelOptions CreateParallelOptions(int? maxDegreeOfParallelism, CancellationToken cancellationToken) => new()
    {
        MaxDegreeOfParallelism = maxDegreeOfParallelism ?? EnumerableDefaults.MaxDegreeOfParallelism,
        CancellationToken = cancellationToken
    };

    #endregion

    #region Seçim, pencere, birikim

    public static List<T> TopBy<T, TKey>(this IEnumerable<T> source, int count, Func<T, TKey> keySelector, IComparer<TKey>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0) return [];

        comparer ??= Comparer<TKey>.Default;
        var heap = new PriorityQueue<T, TKey>(count + 1, comparer);
        foreach (T item in source)
        {
            TKey key = keySelector(item);
            if (heap.Count < count) heap.Enqueue(item, key);
            else if (heap.TryPeek(out _, out TKey? min) && comparer.Compare(key, min) > 0) heap.EnqueueDequeue(item, key);
        }

        var result = new List<T>(heap.Count);
        while (heap.TryDequeue(out T? item, out _)) result.Add(item);
        result.Reverse();
        return result;
    }

    public static List<T> BottomBy<T, TKey>(this IEnumerable<T> source, int count, Func<T, TKey> keySelector, IComparer<TKey>? comparer = null)
    {
        IComparer<TKey> baseComparer = comparer ?? Comparer<TKey>.Default;
        return source.TopBy(count, keySelector, Comparer<TKey>.Create((a, b) => baseComparer.Compare(b, a)));
    }

    public static IEnumerable<T[]> Window<T>(this IEnumerable<T> source, int size)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var queue = new Queue<T>(size);
        foreach (T item in source)
        {
            queue.Enqueue(item);
            if (queue.Count > size) queue.Dequeue();
            if (queue.Count == size) yield return queue.ToArray();
        }
    }

    public static IEnumerable<TAccumulate> Scan<T, TAccumulate>(this IEnumerable<T> source, TAccumulate seed, Func<TAccumulate, T, TAccumulate> accumulator)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accumulator);

        TAccumulate current = seed;
        foreach (T item in source)
        {
            current = accumulator(current, item);
            yield return current;
        }
    }

    public static IEnumerable<List<T>> ChunkByWeight<T>(this IEnumerable<T> source, long maxWeight, Func<T, long> weightSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWeight, 1);

        var chunk = new List<T>();
        long weight = 0;
        foreach (T item in source)
        {
            long itemWeight = weightSelector(item);
            if (chunk.Count > 0 && weight + itemWeight > maxWeight)
            {
                yield return chunk;
                chunk = [];
                weight = 0;
            }

            chunk.Add(item);
            weight += itemWeight;
        }

        if (chunk.Count > 0) yield return chunk;
    }

    #endregion

    #region Asenkron akışlar

    public static async IAsyncEnumerable<T[]> ChunkAsync<T>(this IAsyncEnumerable<T> source, int size,
                                                            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var buffer = new List<T>(size);
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            buffer.Add(item);
            if (buffer.Count == size)
            {
                yield return buffer.ToArray();
                buffer.Clear();
            }
        }

        if (buffer.Count > 0) yield return buffer.ToArray();
    }

    public static Task ParallelForEachAsync<T>(this IAsyncEnumerable<T> source, Func<T, CancellationToken, ValueTask> body,
                                               int? maxDegreeOfParallelism = null, CancellationToken cancellationToken = default) =>
        Parallel.ForEachAsync(source, CreateParallelOptions(maxDegreeOfParallelism, cancellationToken), body);

    public static async Task ProcessInBatchesAsync<T>(this IAsyncEnumerable<T> source, int batchSize, Func<T[], CancellationToken, Task> processBatch,
                                                      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processBatch);

        await foreach (T[] batch in source.ChunkAsync(batchSize, cancellationToken).ConfigureAwait(false))
            await processBatch(batch, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Dönüşümler

    public static string ToDelimitedString<T>(this IEnumerable<T> source, string separator = EnumerableDefaults.Separator,
                                              Func<T, string?>? selector = null)
    {
        if (selector is null) return string.Join(separator, source);

        var builder = new StringBuilder();
        foreach (T item in source)
        {
            if (builder.Length > 0) builder.Append(separator);
            builder.Append(selector(item));
        }

        return builder.ToString();
    }

    public static DataTable ToDataTable<T>(this IEnumerable<T> source, string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return DataTableConverter.Convert(source, tableName);
    }

    #endregion
}
