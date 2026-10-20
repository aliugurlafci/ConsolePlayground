using CSPLibraries.Database.Constants;

namespace CSPLibraries.Database.Datas;

public sealed class BulkInsertOptions
{
    public static BulkInsertOptions Default { get; } = new();

    public int BatchSize { get; init; } = DatabaseDefaults.BulkBatchSize;

    public int TimeoutSeconds { get; init; } = DatabaseDefaults.BulkTimeoutSeconds;

    public bool TableLock { get; init; } = true;

    public bool KeepIdentity { get; init; }

    public bool KeepNulls { get; init; }

    public bool CheckConstraints { get; init; }

    public bool FireTriggers { get; init; }

    public IReadOnlyDictionary<string, string>? ColumnMappings { get; init; }

    public int RowsPerStatement { get; init; } = DatabaseDefaults.RowsPerStatement;

    public int MaxParametersPerStatement { get; init; } = DatabaseDefaults.MaxParametersPerStatement;
}
