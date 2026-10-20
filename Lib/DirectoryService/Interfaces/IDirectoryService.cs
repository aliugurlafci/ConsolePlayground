using System.IO.Compression;
using System.Text;
using CSPLibraries.Directories.Datas;
using CSPLibraries.Directories.Models;

namespace CSPLibraries.Directories.Interfaces;

public interface IDirectoryService
{
    DirectoryInfo EnsureDirectory(string path);

    IEnumerable<FileEntry> EnumerateFiles(string directory, FileQuery? query = null);

    List<FileEntry> GetFiles(string directory, FileQuery? query = null);

    long GetDirectorySize(string directory, bool recursive = true);

    Task<DirectoryCopyResult> CopyDirectoryAsync(string sourceDirectory, string destinationDirectory,
                                                 bool overwrite = false, CancellationToken cancellationToken = default);

    Task MoveDirectoryAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default);

    Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default);

    Task DeleteDirectoryAsync(string directory, bool recursive = true, CancellationToken cancellationToken = default);

    Task<int> DeleteFilesAsync(string directory, FileQuery query, CancellationToken cancellationToken = default);

    Task WriteAllTextAtomicAsync(string path, string content, Encoding? encoding = null, CancellationToken cancellationToken = default);

    Task WriteAllBytesAtomicAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    Task<bool> WaitForFileReadyAsync(string path, TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<string> ArchiveFileAsync(string path, string archiveRoot, DateTime? archiveDate = null, CancellationToken cancellationToken = default);

    Task CreateZipAsync(string sourceDirectory, string zipPath, CompressionLevel compressionLevel = CompressionLevel.Optimal,
                        bool includeBaseDirectory = false, CancellationToken cancellationToken = default);

    Task ExtractZipAsync(string zipPath, string destinationDirectory, bool overwrite = false, CancellationToken cancellationToken = default);

    IAsyncEnumerable<FileChange> WatchAsync(string directory, string filter = "*", bool includeSubdirectories = false,
                                            TimeSpan? debounce = null, CancellationToken cancellationToken = default);

    Task<List<DuplicateFileGroup>> FindDuplicateFilesAsync(string directory, FileQuery? query = null, CancellationToken cancellationToken = default);

    DriveSpaceInfo GetDriveSpace(string path);

    string SanitizeFileName(string fileName, char replacement = '_');

    string GetUniqueFilePath(string path);
}
