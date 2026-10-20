using System.Collections.Frozen;

namespace CSPLibraries.Database.Constants;

internal static class DatabaseDefaults
{
    internal const int CommandTimeoutSeconds = 30;
    internal const int MaxRetries = 3;
    internal const int MaxRetryLimit = 10;
    internal const char ParameterPrefix = '@';

    internal const int BulkBatchSize = 5_000;
    internal const int RowsPerStatement = 100;
    internal const int MaxParametersPerStatement = 1_000;
    internal const int BulkTimeoutSeconds = 600;

    internal const int StringParameterSize = 4000;

    internal static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(200);
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(5);
}

internal static class SqlServerErrorNumbers
{
    internal const int Deadlock = 1205;

    internal static readonly FrozenSet<int> TransientOnOpen = FrozenSet.ToFrozenSet(
    [
        20, 64, 233, 4060, 4221, 10053, 10054, 10060, 10928, 10929, 11001,
        40143, 40197, 40501, 40540, 40613, 42108, 42109, 49918, 49919, 49920
    ]);

    internal static readonly FrozenSet<int> SafeToRetryCommand = FrozenSet.ToFrozenSet(
    [
        Deadlock, 10928, 10929, 40501, 40613, 49918, 49919, 49920
    ]);
}
