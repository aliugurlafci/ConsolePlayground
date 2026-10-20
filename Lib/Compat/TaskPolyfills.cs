#if !NET8_0_OR_GREATER
namespace System.Threading.Tasks
{
    internal static class TaskPolyfills
    {
#if !NET6_0_OR_GREATER
        extension(Task task)
        {
            public Task WaitAsync(CancellationToken cancellationToken) =>
                task.WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken);

            public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
            {
                if (task.IsCompleted || (timeout == Timeout.InfiniteTimeSpan && !cancellationToken.CanBeCanceled))
                {
                    await task.ConfigureAwait(false);
                    return;
                }

                using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task completed = await Task.WhenAny(task, Task.Delay(timeout, delayCancellation.Token)).ConfigureAwait(false);
                if (completed != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }

                delayCancellation.Cancel();
                await task.ConfigureAwait(false);
            }
        }

        extension<TResult>(Task<TResult> task)
        {
            public async Task<TResult> WaitAsync(CancellationToken cancellationToken)
            {
                await ((Task)task).WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return await task.ConfigureAwait(false);
            }

            public async Task<TResult> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
            {
                await ((Task)task).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                return await task.ConfigureAwait(false);
            }
        }
#endif

#if !NET5_0_OR_GREATER
        extension(ValueTask)
        {
            public static ValueTask CompletedTask => default;

            public static ValueTask<TResult> FromResult<TResult>(TResult result) => new(result);

            public static ValueTask FromCanceled(CancellationToken cancellationToken) => new(Task.FromCanceled(cancellationToken));

            public static ValueTask<TResult> FromCanceled<TResult>(CancellationToken cancellationToken) =>
                new(Task.FromCanceled<TResult>(cancellationToken));

            public static ValueTask FromException(Exception exception) => new(Task.FromException(exception));

            public static ValueTask<TResult> FromException<TResult>(Exception exception) =>
                new(Task.FromException<TResult>(exception));
        }
#endif

        extension(CancellationTokenSource source)
        {
            public Task CancelAsync()
            {
                source.Cancel();
                return Task.CompletedTask;
            }
        }
    }

#if !NET5_0_OR_GREATER
    internal sealed class TaskCompletionSource
    {
        private readonly TaskCompletionSource<bool> _inner;

        public TaskCompletionSource() => _inner = new TaskCompletionSource<bool>();

        public TaskCompletionSource(TaskCreationOptions creationOptions) => _inner = new TaskCompletionSource<bool>(creationOptions);

        public Task Task => _inner.Task;

        public void SetResult() => _inner.SetResult(true);

        public bool TrySetResult() => _inner.TrySetResult(true);

        public void SetException(Exception exception) => _inner.SetException(exception);

        public bool TrySetException(Exception exception) => _inner.TrySetException(exception);

        public void SetCanceled() => _inner.SetCanceled();

        public bool TrySetCanceled() => _inner.TrySetCanceled();

        public bool TrySetCanceled(CancellationToken cancellationToken) => _inner.TrySetCanceled(cancellationToken);
    }
#endif
}
#endif
