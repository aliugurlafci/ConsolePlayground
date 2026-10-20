namespace CSPLibraries.Directories.Workers;

internal static class IoRetry
{
    internal static async Task<T> ExecuteAsync<T>(Func<T> operation, int maxRetries, TimeSpan delay,
                                                  CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
            {
                await Task.Delay(TimeSpan.FromTicks(delay.Ticks * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static Task ExecuteAsync(Action operation, int maxRetries, TimeSpan delay, CancellationToken cancellationToken) =>
        ExecuteAsync(() =>
        {
            operation();
            return true;
        }, maxRetries, delay, cancellationToken);

    private static bool IsTransient(Exception exception) =>
        exception is IOException and not FileNotFoundException and not DirectoryNotFoundException and not PathTooLongException
        || exception is UnauthorizedAccessException;
}
