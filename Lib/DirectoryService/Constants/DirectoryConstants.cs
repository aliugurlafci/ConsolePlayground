using System.Buffers;
using System.Collections.Frozen;

namespace CSPLibraries.Directories.Constants;

internal static class DirectoryConstants
{
    internal const int FileBufferSize = 81_920;
    internal const int MaxRetries = 3;
    internal const int MaxParallelCopies = 4;
    internal const char FileNameReplacement = '_';
    internal const int MaxFileNameLength = 200;
    internal const int WatchQueueCapacity = 10_000;
    internal const int WatcherBufferSize = 64 * 1024;
    internal const int PartialHashSize = 16 * 1024;

    internal static readonly TimeSpan WatchDebounce = TimeSpan.FromMilliseconds(300);

    internal static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    internal static readonly TimeSpan FileReadyPollInterval = TimeSpan.FromMilliseconds(250);

    internal static readonly SearchValues<char> InvalidFileNameChars =
        SearchValues.Create(Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).Distinct().ToArray());

    internal static readonly FrozenSet<string> ReservedNames = FrozenSet.ToFrozenSet(
        ["CON", "PRN", "AUX", "NUL",
         "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"],
        StringComparer.OrdinalIgnoreCase);
}
