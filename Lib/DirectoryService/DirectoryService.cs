using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using CSPLibraries.Directories.Constants;
using CSPLibraries.Directories.Datas;
using CSPLibraries.Directories.Interfaces;
using CSPLibraries.Directories.Models;
using CSPLibraries.Directories.Workers;

namespace CSPLibraries.Directories;

public sealed class DirectoryService : IDirectoryService
{
    private readonly DirectoryServiceOptions _options;
    private readonly TimeProvider _timeProvider;

    public DirectoryService(DirectoryServiceOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? DirectoryServiceOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #region Tarama

    public DirectoryInfo EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Directory.CreateDirectory(path);
    }

    public IEnumerable<FileEntry> EnumerateFiles(string directory, FileQuery? query = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return Directory.Exists(directory)
            ? FileSystemScanner.Scan(directory, query ?? new FileQuery(), _timeProvider.GetUtcNow().UtcDateTime)
            : [];
    }

    public List<FileEntry> GetFiles(string directory, FileQuery? query = null) => EnumerateFiles(directory, query).ToList();

    public long GetDirectorySize(string directory, bool recursive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return Directory.Exists(directory) ? FileSystemScanner.GetSize(directory, recursive) : 0;
    }

    #endregion

    #region Kopyalama / taşıma / silme

    public async Task<DirectoryCopyResult> CopyDirectoryAsync(string sourceDirectory, string destinationDirectory,
                                                              bool overwrite = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Kaynak klasör bulunamadı: {sourceDirectory}");

        long start = Stopwatch.GetTimestamp();
        string sourceRoot = Path.GetFullPath(sourceDirectory);
        string destinationRoot = Path.GetFullPath(destinationDirectory);

        Directory.CreateDirectory(destinationRoot);
#if NETCOREAPP
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
#else
        foreach (DirectoryInfo directory in FileSystemScanner.EnumerateEntries(sourceRoot, recursive: true, FileAttributes.Hidden | FileAttributes.System)
                                                             .OfType<DirectoryInfo>())
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory.FullName)));
#endif

        int copied = 0, skipped = 0;
        long bytes = 0;
        IEnumerable<FileEntry> files = FileSystemScanner.Scan(sourceRoot, new FileQuery { Recursive = true, IncludeHidden = true },
                                                              _timeProvider.GetUtcNow().UtcDateTime);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _options.MaxParallelCopies),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
        {
            string target = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file.FullPath));
            if (!overwrite && File.Exists(target))
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            await IoRetry.ExecuteAsync(() => File.Copy(file.FullPath, target, overwrite), _options.MaxRetries, _options.RetryDelay, token)
                         .ConfigureAwait(false);
            Interlocked.Increment(ref copied);
            Interlocked.Add(ref bytes, file.Length);
        }).ConfigureAwait(false);

        return new DirectoryCopyResult(copied, skipped, bytes, Stopwatch.GetElapsedTime(start));
    }

    public async Task MoveDirectoryAsync(string sourceDirectory, string destinationDirectory,
                                         CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        string source = Path.GetFullPath(sourceDirectory);
        string destination = Path.GetFullPath(destinationDirectory);

        if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
        {
            await IoRetry.ExecuteAsync(() => Directory.Move(source, destination), _options.MaxRetries, _options.RetryDelay, cancellationToken)
                         .ConfigureAwait(false);
            return;
        }

        await CopyDirectoryAsync(source, destination, overwrite: true, cancellationToken).ConfigureAwait(false);
        await DeleteDirectoryAsync(source, recursive: true, cancellationToken).ConfigureAwait(false);
    }

    public Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false,
                              CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string? targetDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (targetDirectory is not null) Directory.CreateDirectory(targetDirectory);

        return IoRetry.ExecuteAsync(() => File.Move(sourcePath, destinationPath, overwrite),
                                    _options.MaxRetries, _options.RetryDelay, cancellationToken);
    }

    public Task DeleteDirectoryAsync(string directory, bool recursive = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) return Task.CompletedTask;

        return IoRetry.ExecuteAsync(() =>
        {
            try
            {
                Directory.Delete(directory, recursive);
            }
            catch (UnauthorizedAccessException) when (recursive)
            {
                ClearReadOnlyAttributes(directory);
                Directory.Delete(directory, recursive);
            }
        }, _options.MaxRetries, _options.RetryDelay, cancellationToken);
    }

    public async Task<int> DeleteFilesAsync(string directory, FileQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        int deleted = 0;
        foreach (FileEntry file in EnumerateFiles(directory, query).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await IoRetry.ExecuteAsync(() => File.Delete(file.FullPath), _options.MaxRetries, _options.RetryDelay, cancellationToken)
                         .ConfigureAwait(false);
            deleted++;
        }

        return deleted;
    }

    private static void ClearReadOnlyAttributes(string directory)
    {
#if NETCOREAPP
        foreach (string path in Directory.EnumerateFileSystemEntries(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
#else
        foreach (string path in FileSystemScanner.EnumerateEntries(directory, recursive: true, 0).Select(static entry => entry.FullName))
#endif
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    #endregion

    #region Yazma / bekleme / arşiv

    public Task WriteAllTextAtomicAsync(string path, string content, Encoding? encoding = null,
                                        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        byte[] bytes = (encoding ?? new UTF8Encoding(false)).GetBytes(content);
        return WriteAllBytesAtomicAsync(path, bytes, cancellationToken);
    }

    public async Task WriteAllBytesAtomicAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                        DirectoryConstants.FileBufferSize, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            }

            await IoRetry.ExecuteAsync(() => File.Move(tempPath, fullPath, overwrite: true), _options.MaxRetries, _options.RetryDelay, cancellationToken)
                         .ConfigureAwait(false);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public async Task<bool> WaitForFileReadyAsync(string path, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        long deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);

        while (true)
        {
            if (File.Exists(path))
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 1);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (Stopwatch.GetTimestamp() >= deadline) return false;
            await Task.Delay(DirectoryConstants.FileReadyPollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> ArchiveFileAsync(string path, string archiveRoot, DateTime? archiveDate = null,
                                               CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRoot);
        if (!File.Exists(path)) throw new FileNotFoundException("Arşivlenecek dosya bulunamadı.", path);

        DateTime date = archiveDate ?? _timeProvider.GetLocalNow().DateTime;
        string folder = Path.Combine(archiveRoot,
            date.ToString("yyyy", CultureInfo.InvariantCulture),
            date.ToString("MM", CultureInfo.InvariantCulture),
            date.ToString("dd", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(folder);
        string target = FileNameSanitizer.GetUniquePath(Path.Combine(folder, Path.GetFileName(path)));

        await MoveFileAsync(path, target, overwrite: false, cancellationToken).ConfigureAwait(false);
        return target;
    }

    public async Task CreateZipAsync(string sourceDirectory, string zipPath, CompressionLevel compressionLevel = CompressionLevel.Optimal,
                                     bool includeBaseDirectory = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(zipPath));
        if (directory is not null) Directory.CreateDirectory(directory);

        await ZipFile.CreateFromDirectoryAsync(sourceDirectory, zipPath, compressionLevel, includeBaseDirectory, cancellationToken)
                     .ConfigureAwait(false);
    }

    public Task ExtractZipAsync(string zipPath, string destinationDirectory, bool overwrite = false,
                                CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        return ZipFile.ExtractToDirectoryAsync(zipPath, destinationDirectory, overwrite, cancellationToken);
    }

    #endregion

    #region İzleme, yinelenenler, disk alanı

    public IAsyncEnumerable<FileChange> WatchAsync(string directory, string filter = "*", bool includeSubdirectories = false,
                                                   TimeSpan? debounce = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"İzlenecek klasör bulunamadı: {directory}");

        return DirectoryWatcher.WatchAsync(directory, filter, includeSubdirectories, debounce ?? DirectoryConstants.WatchDebounce, cancellationToken);
    }

    public Task<List<DuplicateFileGroup>> FindDuplicateFilesAsync(string directory, FileQuery? query = null, CancellationToken cancellationToken = default) =>
        DuplicateFinder.FindAsync(EnumerateFiles(directory, query ?? new FileQuery { Recursive = true }), _options.MaxParallelHashing, cancellationToken);

    public DriveSpaceInfo GetDriveSpace(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        return new DriveSpaceInfo(drive.Name, drive.TotalSize, drive.TotalFreeSpace, drive.AvailableFreeSpace);
    }

    #endregion

    #region Adlandırma

    public string SanitizeFileName(string fileName, char replacement = DirectoryConstants.FileNameReplacement) =>
        FileNameSanitizer.Sanitize(fileName, replacement);

    public string GetUniqueFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FileNameSanitizer.GetUniquePath(path);
    }

    #endregion

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
