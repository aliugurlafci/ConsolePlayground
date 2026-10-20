using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CSPLibraries.Directories.Constants;
using CSPLibraries.Directories.Models;

namespace CSPLibraries.Directories.Workers;

internal static class DirectoryWatcher
{
    internal static async IAsyncEnumerable<FileChange> WatchAsync(string directory, string filter, bool includeSubdirectories,
                                                                  TimeSpan debounce, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<FileChange>(new BoundedChannelOptions(DirectoryConstants.WatchQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        var lastSeen = new ConcurrentDictionary<(string, FileChangeKind), long>();
        long debounceTicks = debounce.Ticks;

        void Publish(FileChangeKind kind, string path, string? oldPath = null)
        {
            long now = DateTime.UtcNow.Ticks;
            if (debounceTicks > 0 && kind is FileChangeKind.Changed or FileChangeKind.Created)
            {
                long previous = lastSeen.GetOrAdd((path, kind), 0);
                if (now - previous < debounceTicks) return;
                lastSeen[(path, kind)] = now;
            }

            channel.Writer.TryWrite(new FileChange(kind, path, oldPath, new DateTime(now, DateTimeKind.Utc)));
        }

        using var watcher = new FileSystemWatcher(directory, filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            InternalBufferSize = DirectoryConstants.WatcherBufferSize,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        watcher.Created += (_, e) => Publish(FileChangeKind.Created, e.FullPath);
        watcher.Changed += (_, e) => Publish(FileChangeKind.Changed, e.FullPath);
        watcher.Deleted += (_, e) => Publish(FileChangeKind.Deleted, e.FullPath);
        watcher.Renamed += (_, e) => Publish(FileChangeKind.Renamed, e.FullPath, e.OldFullPath);
        watcher.Error += (_, _) => Publish(FileChangeKind.Overflow, directory);
        watcher.EnableRaisingEvents = true;

        using CancellationTokenRegistration registration = cancellationToken.Register(() => channel.Writer.TryComplete());

        await foreach (FileChange change in channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            yield return change;
            if (lastSeen.Count > DirectoryConstants.WatchQueueCapacity) lastSeen.Clear();
        }
    }
}
