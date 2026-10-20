#if NETCOREAPP
using System.IO.Enumeration;
#endif
using CSPLibraries.Directories.Datas;
using CSPLibraries.Directories.Models;

namespace CSPLibraries.Directories.Workers;

internal static class FileSystemScanner
{
#if NETCOREAPP
    internal static IEnumerable<FileEntry> Scan(string directory, FileQuery query, DateTime utcNow)
    {
        var extensions = new HashSet<string>(
            query.Extensions.Select(e => e.StartsWith('.') ? e : "." + e), StringComparer.OrdinalIgnoreCase);
#if NET9_0_OR_GREATER
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> extensionLookup = extensions.GetAlternateLookup<ReadOnlySpan<char>>();
#endif

        string pattern = string.IsNullOrEmpty(query.SearchPattern) ? "*" : query.SearchPattern;
        DateTime? olderThan = query.OlderThan.HasValue ? utcNow - query.OlderThan.Value : null;
        DateTime? newerThan = query.NewerThan.HasValue ? utcNow - query.NewerThan.Value : null;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = query.Recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = query.IncludeHidden ? FileAttributes.System : FileAttributes.Hidden | FileAttributes.System,
            BufferSize = 64 * 1024
        };

        return new FileSystemEnumerable<FileEntry>(directory,
            static (ref FileSystemEntry entry) => new FileEntry(
                entry.ToFullPath(), entry.FileName.ToString(), entry.Length,
                entry.LastWriteTimeUtc.UtcDateTime, entry.CreationTimeUtc.UtcDateTime),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
            {
                if (entry.IsDirectory) return false;
                if (pattern != "*" && !FileSystemName.MatchesSimpleExpression(pattern, entry.FileName, ignoreCase: true)) return false;
#if NET9_0_OR_GREATER
                if (extensions.Count > 0 && !extensionLookup.Contains(Path.GetExtension(entry.FileName))) return false;
#else
                if (extensions.Count > 0 && !extensions.Contains(Path.GetExtension(entry.FileName).ToString())) return false;
#endif
                if (query.MinSizeBytes.HasValue && entry.Length < query.MinSizeBytes.Value) return false;
                if (query.MaxSizeBytes.HasValue && entry.Length > query.MaxSizeBytes.Value) return false;

                if (olderThan.HasValue || newerThan.HasValue)
                {
                    DateTime written = entry.LastWriteTimeUtc.UtcDateTime;
                    if (olderThan.HasValue && written >= olderThan.Value) return false;
                    if (newerThan.HasValue && written <= newerThan.Value) return false;
                }

                return true;
            }
        };
    }

    internal static long GetSize(string directory, bool recursive)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true, AttributesToSkip = 0 };
        var lengths = new FileSystemEnumerable<long>(directory, static (ref FileSystemEntry entry) => entry.Length, options)
        {
            ShouldIncludePredicate = static (ref FileSystemEntry entry) => !entry.IsDirectory
        };

        long total = 0;
        foreach (long length in lengths)
            total += length;

        return total;
    }
#else
    internal static IEnumerable<FileEntry> Scan(string directory, FileQuery query, DateTime utcNow)
    {
        var extensions = new HashSet<string>(
            query.Extensions.Select(e => e.StartsWith('.') ? e : "." + e), StringComparer.OrdinalIgnoreCase);

        string pattern = string.IsNullOrEmpty(query.SearchPattern) ? "*" : query.SearchPattern;
        DateTime? olderThan = query.OlderThan.HasValue ? utcNow - query.OlderThan.Value : null;
        DateTime? newerThan = query.NewerThan.HasValue ? utcNow - query.NewerThan.Value : null;
        FileAttributes skip = query.IncludeHidden ? FileAttributes.System : FileAttributes.Hidden | FileAttributes.System;

        foreach (FileInfo file in EnumerateEntries(directory, query.Recursive, skip).OfType<FileInfo>())
        {
            if (pattern != "*" && !MatchesSimpleExpression(pattern, file.Name)) continue;
            if (extensions.Count > 0 && !extensions.Contains(file.Extension)) continue;
            if (query.MinSizeBytes.HasValue && file.Length < query.MinSizeBytes.Value) continue;
            if (query.MaxSizeBytes.HasValue && file.Length > query.MaxSizeBytes.Value) continue;

            DateTime written = file.LastWriteTimeUtc;
            if (olderThan.HasValue && written >= olderThan.Value) continue;
            if (newerThan.HasValue && written <= newerThan.Value) continue;

            yield return new FileEntry(file.FullName, file.Name, file.Length, written, file.CreationTimeUtc);
        }
    }

    internal static long GetSize(string directory, bool recursive)
    {
        long total = 0;
        foreach (FileInfo file in EnumerateEntries(directory, recursive, 0).OfType<FileInfo>())
            total += file.Length;

        return total;
    }

    internal static IEnumerable<FileSystemInfo> EnumerateEntries(string directory, bool recursive, FileAttributes attributesToSkip)
    {
        var pending = new Queue<DirectoryInfo>();
        pending.Enqueue(new DirectoryInfo(directory));

        while (pending.Count > 0)
        {
            DirectoryInfo current = pending.Dequeue();
            foreach (FileSystemInfo entry in EnumerateAccessibleEntries(current))
            {
                if ((entry.Attributes & attributesToSkip) != 0) continue;

                if (recursive && entry is DirectoryInfo subdirectory && (subdirectory.Attributes & FileAttributes.ReparsePoint) == 0)
                    pending.Enqueue(subdirectory);

                yield return entry;
            }
        }
    }

    private static IEnumerable<FileSystemInfo> EnumerateAccessibleEntries(DirectoryInfo directory)
    {
        IEnumerator<FileSystemInfo> entries;
        try
        {
            entries = directory.EnumerateFileSystemInfos().GetEnumerator();
        }
        catch (Exception ex) when (IsInaccessible(ex))
        {
            yield break;
        }

        using (entries)
        {
            while (true)
            {
                FileSystemInfo entry;
                try
                {
                    if (!entries.MoveNext()) yield break;
                    entry = entries.Current;
                }
                catch (Exception ex) when (IsInaccessible(ex))
                {
                    yield break;
                }

                yield return entry;
            }
        }
    }

    private static bool IsInaccessible(Exception exception) =>
        exception is UnauthorizedAccessException or System.Security.SecurityException or DirectoryNotFoundException;

    private static bool MatchesSimpleExpression(string expression, string name)
    {
        int e = 0, n = 0, starE = -1, starN = 0;
        while (n < name.Length)
        {
            if (e < expression.Length)
            {
                char c = expression[e];
                if (c == '*')
                {
                    starE = e++;
                    starN = n;
                    continue;
                }

                bool escaped = c == '\\' && e + 1 < expression.Length;
                char expected = escaped ? expression[e + 1] : c;
                if ((!escaped && c == '?') || char.ToUpperInvariant(expected) == char.ToUpperInvariant(name[n]))
                {
                    e += escaped ? 2 : 1;
                    n++;
                    continue;
                }
            }

            if (starE < 0) return false;
            e = starE + 1;
            n = ++starN;
        }

        while (e < expression.Length && expression[e] == '*') e++;
        return e == expression.Length;
    }
#endif
}
