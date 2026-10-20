using CSPLibraries.Directories.Constants;

namespace CSPLibraries.Directories.Datas;

public sealed class DirectoryServiceOptions
{
    public static DirectoryServiceOptions Default { get; } = new();

    public int MaxRetries { get; init; } = DirectoryConstants.MaxRetries;

    public TimeSpan RetryDelay { get; init; } = DirectoryConstants.RetryDelay;

    public int MaxParallelCopies { get; init; } = DirectoryConstants.MaxParallelCopies;

    public int MaxParallelHashing { get; init; } = Math.Clamp(Environment.ProcessorCount, 2, 8);
}
