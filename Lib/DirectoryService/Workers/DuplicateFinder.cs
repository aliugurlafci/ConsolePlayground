using System.Buffers;
using System.Security.Cryptography;
using CSPLibraries.Directories.Constants;
using CSPLibraries.Directories.Models;

namespace CSPLibraries.Directories.Workers;

internal static class DuplicateFinder
{
    internal static async Task<List<DuplicateFileGroup>> FindAsync(IEnumerable<FileEntry> files, int maxParallelism, CancellationToken cancellationToken)
    {
        List<FileEntry> candidates = files.Where(f => f.Length > 0)
                                          .GroupBy(f => f.Length)
                                          .Where(g => g.Skip(1).Any())
                                          .SelectMany(g => g)
                                          .ToList();

        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxParallelism), CancellationToken = cancellationToken };

        List<FileEntry> large = candidates.Where(f => f.Length > DirectoryConstants.PartialHashSize).ToList();
        if (large.Count > 0)
        {
            var prefixes = new string[large.Count];
            await Parallel.ForAsync(0, large.Count, parallel, async (i, token) =>
                prefixes[i] = await HashAsync(large[i].FullPath, DirectoryConstants.PartialHashSize, token).ConfigureAwait(false))
                .ConfigureAwait(false);

            var survivors = new HashSet<FileEntry>(large.Select((file, i) => (file, key: (file.Length, prefixes[i])))
                                                        .GroupBy(x => x.key)
                                                        .Where(g => g.Skip(1).Any())
                                                        .SelectMany(g => g.Select(x => x.file)));

            candidates = candidates.Where(f => f.Length <= DirectoryConstants.PartialHashSize || survivors.Contains(f))
                                   .GroupBy(f => f.Length)
                                   .Where(g => g.Skip(1).Any())
                                   .SelectMany(g => g)
                                   .ToList();
        }

        var hashes = new string[candidates.Count];
        await Parallel.ForAsync(0, candidates.Count, parallel, async (i, token) =>
            hashes[i] = await HashAsync(candidates[i].FullPath, long.MaxValue, token).ConfigureAwait(false))
            .ConfigureAwait(false);

        return candidates.Select((file, i) => (file, hash: hashes[i]))
                         .GroupBy(x => (x.file.Length, x.hash))
                         .Where(g => g.Skip(1).Any())
                         .Select(g => new DuplicateFileGroup(g.Key.hash, g.Key.Length,
                                                             g.Select(x => x.file.FullPath).Order(StringComparer.OrdinalIgnoreCase).ToList()))
                         .ToList();
    }

    private static async Task<string> HashAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(DirectoryConstants.FileBufferSize);
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                        DirectoryConstants.FileBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (stream.ConfigureAwait(false))
            {
                long remaining = maxBytes;
                int read;
                while (remaining > 0 &&
                       (read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    remaining -= read;
                }

                return Convert.ToHexStringLower(hash.GetHashAndReset());
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
