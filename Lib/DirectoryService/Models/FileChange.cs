namespace CSPLibraries.Directories.Models;

public enum FileChangeKind
{
    Created = 0,
    Changed = 1,
    Deleted = 2,
    Renamed = 3,
    Overflow = 4
}

public sealed record FileChange(FileChangeKind Kind, string FullPath, string? OldFullPath, DateTime TimestampUtc);

public sealed record DuplicateFileGroup(string Hash, long Length, IReadOnlyList<string> Files)
{
    public long WastedBytes => Length * (Files.Count - 1);
}

public sealed record DriveSpaceInfo(string Name, long TotalBytes, long FreeBytes, long AvailableBytes)
{
    public double UsedPercentage => TotalBytes == 0 ? 0 : (TotalBytes - FreeBytes) * 100.0 / TotalBytes;
}
