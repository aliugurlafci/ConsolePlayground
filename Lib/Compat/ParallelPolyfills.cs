#if !NET8_0_OR_GREATER
using System.Collections.Concurrent;

namespace System.Threading.Tasks;

internal static class ParallelPolyfills
{
    extension(Parallel)
    {
#if !NET6_0_OR_GREATER
        public static Task ForEachAsync<TSource>(IEnumerable<TSource> source, Func<TSource, CancellationToken, ValueTask> body) =>
            Parallel.ForEachAsync(source, new ParallelOptions(), body);

        public static Task ForEachAsync<TSource>(IEnumerable<TSource> source, CancellationToken cancellationToken,
                                                 Func<TSource, CancellationToken, ValueTask> body) =>
            Parallel.ForEachAsync(source, new ParallelOptions { CancellationToken = cancellationToken }, body);

        public static Task ForEachAsync<TSource>(IEnumerable<TSource> source, ParallelOptions parallelOptions,
                                                 Func<TSource, CancellationToken, ValueTask> body)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (parallelOptions is null) throw new ArgumentNullException(nameof(parallelOptions));
            if (body is null) throw new ArgumentNullException(nameof(body));

            if (parallelOptions.CancellationToken.IsCancellationRequested)
                return Task.FromCanceled(parallelOptions.CancellationToken);

            object gate = new();
            IEnumerator<TSource>? enumerator = null;

            return RunAsync(parallelOptions, body,
                token =>
                {
                    lock (gate)
                    {
                        enumerator ??= source.GetEnumerator();
                        return new ValueTask<(bool, TSource)>(enumerator.MoveNext() ? (true, enumerator.Current) : (false, default!));
                    }
                },
                () =>
                {
                    enumerator?.Dispose();
                    return default;
                });
        }

        public static Task ForEachAsync<TSource>(IAsyncEnumerable<TSource> source, ParallelOptions parallelOptions,
                                                 Func<TSource, CancellationToken, ValueTask> body)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (parallelOptions is null) throw new ArgumentNullException(nameof(parallelOptions));
            if (body is null) throw new ArgumentNullException(nameof(body));

            if (parallelOptions.CancellationToken.IsCancellationRequested)
                return Task.FromCanceled(parallelOptions.CancellationToken);

            var gate = new SemaphoreSlim(1, 1);
            IAsyncEnumerator<TSource>? enumerator = null;

            return RunAsync(parallelOptions, body,
                async token =>
                {
                    await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        enumerator ??= source.GetAsyncEnumerator(token);
                        return await enumerator.MoveNextAsync().ConfigureAwait(false) ? (true, enumerator.Current) : (false, default!);
                    }
                    finally
                    {
                        gate.Release();
                    }
                },
                async () =>
                {
                    if (enumerator is not null) await enumerator.DisposeAsync().ConfigureAwait(false);
                    gate.Dispose();
                });
        }

        public static Task ForEachAsync<TSource>(IAsyncEnumerable<TSource> source, CancellationToken cancellationToken,
                                                 Func<TSource, CancellationToken, ValueTask> body) =>
            Parallel.ForEachAsync(source, new ParallelOptions { CancellationToken = cancellationToken }, body);
#endif

        public static Task ForAsync(int fromInclusive, int toExclusive, ParallelOptions parallelOptions,
                                    Func<int, CancellationToken, ValueTask> body) =>
            Parallel.ForEachAsync(Enumerable.Range(fromInclusive, Math.Max(0, toExclusive - fromInclusive)), parallelOptions, body);

        public static Task ForAsync(int fromInclusive, int toExclusive, CancellationToken cancellationToken,
                                    Func<int, CancellationToken, ValueTask> body) =>
            Parallel.ForAsync(fromInclusive, toExclusive, new ParallelOptions { CancellationToken = cancellationToken }, body);

        public static Task ForAsync(int fromInclusive, int toExclusive, Func<int, CancellationToken, ValueTask> body) =>
            Parallel.ForAsync(fromInclusive, toExclusive, new ParallelOptions(), body);
    }

#if !NET6_0_OR_GREATER
    private static Task RunAsync<TSource>(ParallelOptions options, Func<TSource, CancellationToken, ValueTask> body,
                                          Func<CancellationToken, ValueTask<(bool HasItem, TSource Item)>> next,
                                          Func<ValueTask> dispose)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = RunCoreAsync();
        return completion.Task;

        async Task RunCoreAsync()
        {
            var errors = new ConcurrentQueue<Exception>();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);

            async Task WorkerAsync()
            {
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        (bool hasItem, TSource item) = await next(cancellation.Token).ConfigureAwait(false);
                        if (!hasItem) return;

                        await body(item, cancellation.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                    cancellation.Cancel();
                }
            }

            try
            {
                int degree = options.MaxDegreeOfParallelism == -1 ? Environment.ProcessorCount : options.MaxDegreeOfParallelism;
                TaskScheduler scheduler = options.TaskScheduler ?? TaskScheduler.Default;

                var workers = new Task[Math.Max(1, degree)];
                for (int i = 0; i < workers.Length; i++)
                    workers[i] = Task.Factory.StartNew(WorkerAsync, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler).Unwrap();

                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
            finally
            {
                try
                {
                    await dispose().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }

            if (!errors.IsEmpty) completion.TrySetException(errors);
            else if (options.CancellationToken.IsCancellationRequested) completion.TrySetCanceled(options.CancellationToken);
            else completion.TrySetResult(true);
        }
    }
#endif
}
#endif
