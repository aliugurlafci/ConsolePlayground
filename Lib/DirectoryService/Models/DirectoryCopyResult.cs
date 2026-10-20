namespace CSPLibraries.Directories.Models;

public sealed record DirectoryCopyResult(int FilesCopied, int FilesSkipped, long BytesCopied, TimeSpan Elapsed);
