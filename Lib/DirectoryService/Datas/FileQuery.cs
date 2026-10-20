namespace CSPLibraries.Directories.Datas;

public sealed class FileQuery
{
    public string SearchPattern { get; init; } = "*";

    public bool Recursive { get; init; }

    public IReadOnlyCollection<string> Extensions { get; init; } = [];

    public TimeSpan? OlderThan { get; init; }

    public TimeSpan? NewerThan { get; init; }

    public long? MinSizeBytes { get; init; }

    public long? MaxSizeBytes { get; init; }

    public bool IncludeHidden { get; init; }
}
