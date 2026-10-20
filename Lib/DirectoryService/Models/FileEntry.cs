namespace CSPLibraries.Directories.Models;

public sealed record FileEntry(string FullPath, string Name, long Length, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc)
{
    public string Extension => Path.GetExtension(Name);

    public string? DirectoryName => Path.GetDirectoryName(FullPath);
}
